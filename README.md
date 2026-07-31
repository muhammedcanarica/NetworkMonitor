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

### Authentication bootstrap and encryption keys

Set `NETSCOPE_ADMIN_USERNAME` and `NETSCOPE_ADMIN_PASSWORD` before the first API start. The account is created once through ASP.NET Core Identity; the password is hashed by Identity and is never stored in configuration.

Network credentials are encrypted with ASP.NET Core Data Protection. Development keys default to `backend/NetworkMonitor.Api/.keys` (git-ignored). In production, set `NETSCOPE_KEY_RING_PATH` to a persistent directory protected by operating-system permissions and backups. Do not place the key ring in the SQLite database or publish it with the application. Losing these keys makes stored network credentials unrecoverable; disclosure of both the database and key ring permits decryption.

```powershell
$env:NETSCOPE_ADMIN_USERNAME = "admin"
$env:NETSCOPE_ADMIN_PASSWORD = "use-a-unique-long-password"
$env:NETSCOPE_KEY_RING_PATH = "C:\secure\netscope-keys"
```

## Planned Features

- Incident tracking
- Interface and bandwidth monitoring
- Network topology
