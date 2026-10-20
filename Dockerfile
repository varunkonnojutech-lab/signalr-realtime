FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY signalr-realtime.sln ./
COPY src/SignalRRealtime.Api/SignalRRealtime.Api.csproj src/SignalRRealtime.Api/
RUN dotnet restore src/SignalRRealtime.Api/SignalRRealtime.Api.csproj

COPY . .
RUN dotnet publish src/SignalRRealtime.Api/SignalRRealtime.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "SignalRRealtime.Api.dll"]
