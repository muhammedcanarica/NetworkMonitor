# Network Monitor

A web-based network monitoring platform built with ASP.NET Core and React. The MVP includes a responsive dashboard backed entirely by the live API.

## Tech Stack

- ASP.NET Core Web API
- C#
- React
- TypeScript
- Vite

## Current Capabilities

- SQLite persistence
- Device management and REST CRUD API
- Background ICMP monitoring
- Persistent ping check history and 24-hour monitoring summaries
- Responsive Overview, Devices, and Device Detail screens
- Realtime monitoring updates via SignalR with REST fallback
- Bounded IPv4 CIDR discovery with ICMP ping and reverse DNS
- Read-only SNMP v2c explorer for system information, interfaces, and custom GET/WALK queries
- Latency history charts and recent check results

Monitoring status counters are kept in process memory for the MVP and reset when the API restarts.
SNMP community credentials are request-only and are not persisted or returned by the API.

## Local Development

The API development profile runs at `http://localhost:5107`. The Vite app uses that address by default; copy `frontend/network-monitor-ui/.env.example` to `.env` only when you need to override it.

Start the backend:

```powershell
dotnet run --project backend/NetworkMonitor.Api --launch-profile http
```

Start the frontend in another terminal:

```powershell
cd frontend/network-monitor-ui
npm install
npm run dev
```

Open `http://localhost:5173`.

## Planned Features

- Incident tracking
- Interface and bandwidth monitoring
- Network topology
