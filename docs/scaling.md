# Scaling SignalR without fooling yourself

A single instance of this app is easy: connections, groups, and messages all live in one process. Scale-out changes the problem. If Ada is connected to server A and Grace is connected to server B, a message sent to their shared group must cross from A to B. That transport is the **backplane**.

## What SignalR groups do — and do not do

- A group is a routing label for connections, not a database table and not a durable room.
- Group membership is connection-scoped. A reconnect is a new connection and must join again; the sample client does this in `onreconnected`.
- Messages are not stored by SignalR. If a client is offline, it misses group messages unless your application adds an outbox/history store.

## Option 1 — Redis backplane (used by this repo's Compose file)

```text
Browser ── WebSocket ── API server A ─┐
                                      ├── Redis pub/sub ── broadcasts/groups
Browser ── WebSocket ── API server B ─┘
```

Enable it by setting:

```text
SignalR__RedisConnectionString=redis:6379
```

The API calls `AddStackExchangeRedis`, and the Compose stack starts Redis beside the API. This is a good fit when you already operate Redis and need a small number of app servers.

Production notes:

- Give each application its own Redis channel prefix so unrelated apps cannot cross-talk.
- Size Redis for fan-out, not just message count: one group message to 10,000 connected users is 10,000 deliveries.
- Monitor Redis latency and blocked clients; backplane delay becomes user-visible chat delay.
- Do not store authoritative presence only in one server's memory. This repo's `RoomPresenceTracker` is a local teaching aid. At scale, keep presence in Redis/database storage with connection heartbeats and expiry.

## Option 2 — Azure SignalR Service

Azure SignalR moves connection management and fan-out to a managed service. The app servers keep hub logic, while clients connect to the service endpoint.

Use it when:

- Connection count is the bottleneck.
- You do not want to operate Redis as a realtime dependency.
- You need managed scale units, metrics, and a service-level operational boundary.

The code change is a different package/registration (`AddAzureSignalR`) and a connection string from Key Vault; hub classes and client method names stay the same. Plan capacity by concurrent connections and message units, and test the pricing tier against your real fan-out pattern.

## Sticky sessions

- With a Redis backplane, SignalR still negotiates a connection and then transports subsequent requests for that connection. Load-balancer behavior matters most for Long Polling / Server-Sent Events.
- WebSockets are naturally pinned by the open socket.
- Do not assume "Redis means no affinity concerns." Test negotiate, reconnect, and transport fallback behind your actual ingress.

## Reconnect is part of the protocol design

Clients should:

1. Reconnect with bounded backoff (the sample uses automatic reconnect delays).
2. Re-join the dashboard and active chat room.
3. Reload an HTTP snapshot (`/api/quotes`, or a chat-history endpoint in a real product) to repair any gap.
4. Treat hub messages as live events, not as the system of record.

## Observability checklist

Track, at minimum:

- Concurrent connections and connection churn
- Negotiate failures and transport distribution (WebSockets vs fallback)
- Hub method latency and exception rate
- Messages sent vs. estimated fan-out deliveries
- Redis/Azure SignalR latency and throttling
- Background publisher health — in this repo, whether `MarketTickerService` ticks are flowing

## The honest scale test

Do not benchmark by opening ten browser tabs. Generate realistic concurrent connections, join patterns, group sizes, and message rates; then watch CPU, sockets, Redis/service metrics, and end-to-end delivery latency. SignalR scales the transport — your group design and fan-out decide whether the math works.
