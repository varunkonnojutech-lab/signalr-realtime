# SignalR Realtime — group chat + live dashboard in .NET 8

[![Build](https://github.com/varunkonnojutech-lab/signalr-realtime/actions/workflows/ci.yml/badge.svg)](https://github.com/varunkonnojutech-lab/signalr-realtime/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/varunkonnojutech-lab/signalr-realtime)

Realtime is easy to demo badly: one broadcast method, no groups, no client contract, and no answer for what happens when a second server joins. This repo builds the two patterns real products need — **room-based chat** and a **server-pushed live dashboard** — with a strongly-typed hub, presence tracking, reconnect-aware client code, and an honest scale-out story.

## Who is this for

.NET developers who know REST and want to learn where SignalR fits: live dashboards, collaboration, notifications, trading/operations screens, and chat — without treating WebSockets as magic.

## What you'll learn

- Designing a hub around use cases instead of exposing one generic `Send` method
- Strongly-typed hubs with `Hub<IRealtimeClient>`, so server-to-client method names are compiler-checked
- SignalR groups for chat rooms and a dashboard room — plus what groups do **not** persist or guarantee
- Validating and normalizing hub inputs in one place (`InputSanitizer`)
- Tracking room presence separately from message routing, including cleanup on disconnect
- Publishing from outside a hub with `IHubContext<RealtimeHub, IRealtimeClient>` (background service + REST broadcast endpoint)
- Browser client behavior that matters: automatic reconnect, re-joining groups, snapshot reload through HTTP, and text-safe DOM rendering
- Scale-out with a Redis backplane, when to choose Azure SignalR Service instead, and why fan-out math beats slogans

## Quick start

### Run it locally

```bash
dotnet restore
dotnet run --project src/SignalRRealtime.Api
```

Open <http://localhost:5000> (or the URL printed by the app):

1. The dashboard starts moving within a few seconds.
2. Open a second browser tab.
3. Join the same room in both tabs and send a message.
4. Stop the server, restart it, and watch the client reconnect, reload its snapshot, and re-join.

### Run it with Docker + Redis backplane

```bash
docker compose up --build
```

Open <http://localhost:8081>. Compose starts Redis, enables `AddStackExchangeRedis` through configuration, and runs the API in a non-root multi-stage container. Redis is **not** needed for the single-server local run — it becomes relevant when messages must cross app servers.

## Try the endpoints

```bash
# Current dashboard snapshot (the HTTP repair path after reconnect)
curl http://localhost:5000/api/quotes

# Send a notification to every connected client through IHubContext
curl -X POST http://localhost:5000/api/broadcast \
  -H "Content-Type: application/json" \
  -d '{"message":"Deployment finished — refresh your dashboard."}'
```

Hub endpoint: `/hubs/realtime`

| Client → server | Purpose |
|---|---|
| `JoinRoom(room, userName)` | Join a chat room or the built-in `dashboard` group and announce presence |
| `LeaveRoom(room, userName)` | Leave a group and announce the new presence count |
| `SendMessage(room, userName, message)` | Send a validated message to room members only |

| Server → client | Purpose |
|---|---|
| `ReceiveMessage(ChatMessage)` | A chat message for the current room |
| `ReceiveQuote(StockQuote)` | One updated dashboard quote |
| `UserJoined` / `UserLeft` | Presence changes with the current room count |
| `ReceiveNotification(string)` | Broadcasts and connection notices |

## Project layout

```text
src/SignalRRealtime.Api/
  Hubs/
    IRealtimeClient.cs       Strongly-typed server-to-client contract
    RealtimeHub.cs           Join/leave/chat + disconnect cleanup
  Models/                    ChatMessage, StockQuote, broadcast + presence records
  Services/
    InputSanitizer.cs        Room/user/message validation and normalization
    RoomPresenceTracker.cs   Thread-safe in-memory room membership
    QuoteStore.cs            Thread-safe dashboard state + random-walk updates
    MarketTickerService.cs   BackgroundService publishing every 3 seconds
  wwwroot/                   Dependency-light HTML/CSS/JS client
  Program.cs                 Hub, CORS, static files, REST endpoints, Redis switch
tests/SignalRRealtime.Tests/
  RealtimeUnitTests.cs       Sanitizer, presence, and quote-store tests
  SignalRIntegrationTests.cs Real TestServer hub: two clients, one room, one message
docs/scaling.md              Redis vs Azure SignalR, sticky sessions, presence, observability
docker-compose.yml           API + Redis backplane topology
Dockerfile                   Multi-stage, non-root API image
```

## Design decisions worth stealing

### Groups route messages; they are not your database

SignalR does not store chat history or remember that a disconnected connection was in a room. This app tracks presence for display, reloads quote state over HTTP after reconnect, and says plainly in [`docs/scaling.md`](docs/scaling.md) where durable history/outbox patterns belong in a production system.

### The dashboard uses push *and* pull

The ticker pushes small quote deltas to the `dashboard` group. The client still fetches `/api/quotes` on load/reconnect. That pair is deliberate: push for liveness, HTTP snapshot for correctness and recovery.

### Hub inputs are hostile until proven otherwise

Room names, user names, and message text come from clients. They are trimmed, bounded, and normalized before use; a sender must join a room before posting to it. Authentication is intentionally out of scope for this lab — add it before internet exposure, and authorize room joins server-side based on the authenticated user, not a display name.

### Typed clients prevent silent failures

With a non-typed hub, `Clients.Group(room).SendAsync("RecieveMessage", ...)` can compile and fail silently at runtime. `Hub<IRealtimeClient>` turns the callback surface into an interface the compiler checks on both the hub and the background publisher.

## Tests

```bash
dotnet test
```

The integration test boots the real app with `WebApplicationFactory`, connects **two** SignalR clients through TestServer, joins both to a generated room, and proves a message crosses the hub. CI runs restore, Release build, and the full test suite.

## Next steps

1. Add JWT authentication and derive the display name from claims instead of a hub argument.
2. Persist chat history and load the last 50 messages when a client joins a room.
3. Run two API instances behind a load balancer with the Redis backplane and repeat the two-tab test.
4. Replace the simulated ticker with a queue consumer and compare the code that changes — the hub contract should not.
5. Read [`docs/scaling.md`](docs/scaling.md), then continue with [`dotnet-microservices`](https://github.com/varunkonnojutech-lab/dotnet-microservices) for service-to-service communication.
