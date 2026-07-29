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
- Live status polling every five seconds
- Latency history charts and recent check results

Monitoring status counters are kept in process memory for the MVP and reset when the API restarts.

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
- Network discovery
- SNMP monitoring
- Interface and bandwidth monitoring
- Network topology
