# NetScope Network Monitor

NetScope is a vendor-neutral network monitoring and operations portfolio project. It combines continuous reachability monitoring, read-only SNMP visibility, incident and notification workflows, operational tools, and configuration history in one responsive web application. Vendor-specific behavior is isolated behind extension points; the current configuration-backup implementation supports Cisco IOS/IOS-XE, but NetScope itself is not a Cisco-only product.

> Use NetScope only on systems and networks you own or are explicitly authorized to test. The repository contains no production IP addresses, communities, SSH credentials, or SMTP credentials.

## Current features

The following capabilities are confirmed by the current API, services, UI routes, and tests:

- Device management with SQLite persistence
- Background ICMP monitoring, monitoring history, and 24-hour summaries
- SignalR realtime monitoring updates with REST refresh fallback
- Bounded IPv4 CIDR IP Scanner with reverse-DNS lookup
- Read-only SNMP v2c Explorer for system information, interfaces, GET, and WALK
- Saved-interface traffic monitoring and bandwidth history charts
- Inbound/outbound bandwidth thresholds and alerts
- Confirmed Interface Down incident creation and recovery tracking
- Incident Tracking and Notification Center with read/unread actions
- Configurable email notifications and test-email action
- On-demand LLDP topology discovery
- Bounded TCP Port Scanner and Wake-on-LAN tool
- On-demand Configuration Backup over SSH
- Configuration History, content deduplication, and line diff
- Cookie-based authentication with an environment-bootstrapped admin account
- Encrypted saved SNMP and SSH network credentials
- Device Intelligence panels on the Device Detail screen

## Technology

**Backend:** .NET 10, ASP.NET Core Web API, ASP.NET Core Identity, Entity Framework Core 10, SQLite, SignalR, ASP.NET Core Data Protection, SharpSnmpLib, SSH.NET, MailKit, xUnit.

**Frontend:** React 19, TypeScript 6, Vite 8, React Router 7, Recharts, Lucide React, Vitest 4, React Testing Library, jest-dom, user-event, jsdom, oxlint.

## Local setup

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 24 LTS (Node 22.12 or later is also compatible with the current Vite version)
- npm
- PowerShell examples below; equivalent environment-variable syntax works in other shells

### 1. Restore and migrate the backend

From the repository root:

```powershell
dotnet tool restore
dotnet restore
dotnet ef database update --project backend/NetworkMonitor.Api --startup-project backend/NetworkMonitor.Api
```

The default SQLite database is created as `networkmonitor.db` in the API working directory.

### 2. Create the first admin account and configure the key ring

The bootstrap account is created only when the username does not already exist. Identity enforces a minimum 10-character password.

```powershell
$env:NETSCOPE_ADMIN_USERNAME = "local-admin"
$env:NETSCOPE_ADMIN_PASSWORD = "replace-with-a-unique-long-password"
$env:NETSCOPE_KEY_RING_PATH = "C:\secure\netscope-keys"
```

For local development, omitting `NETSCOPE_KEY_RING_PATH` stores keys under `backend/NetworkMonitor.Api/.keys`, which is git-ignored. Use a persistent, access-controlled, backed-up directory outside the deployment folder in production.

### 3. Start the backend

```powershell
dotnet run --project backend/NetworkMonitor.Api --launch-profile http
```

The development API listens at `http://localhost:5107`.

### 4. Start the frontend

In a second terminal:

```powershell
cd frontend/network-monitor-ui
npm ci
npm run dev
```

Open `http://localhost:5173`. Copy `.env.example` to `.env` only if the API URL must be overridden.

## Security model

- User passwords are hashed and managed by ASP.NET Core Identity; they are not stored in plain text.
- Saved network credentials and the SMTP password are encrypted with ASP.NET Core Data Protection.
- Data Protection keys are required to decrypt stored credentials. Losing the key ring makes those secrets unrecoverable.
- The database and key ring must be backed up and protected separately. Disclosure of both can allow secret decryption.
- Never commit real device, SNMP, SSH, or SMTP credentials. Use local environment variables and test-only fake values.
- Production deployments should terminate HTTPS and restrict database/key-ring filesystem access.

See [docs/SECURITY.md](docs/SECURITY.md) for the operational checklist.

## Tests and quality checks

Backend:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test NetworkMonitor.slnx --no-restore
```

Frontend:

```powershell
cd frontend/network-monitor-ui
npm ci
npm run lint
npm run test:run
npm run build
```

`npm test` starts Vitest in watch mode for local development. CI uses `npm run test:run`, so it exits after one run. The tests use stubs/mocks and do not require a device, SMTP server, or running backend.

## Screenshots

Real screenshots have not been committed yet; no generated or fake product images are used. Capture the following views with sample-only data:

- **Overview** — placeholder: dashboard status and recent monitoring summary
- **Device Detail** — placeholder: Device Intelligence panels
- **Topology** — placeholder: LLDP graph built from authorized lab devices
- **Incidents** — placeholder: open and resolved incident list
- **Interface Traffic** — placeholder: baseline, chart, and threshold state
- **Notification Center** — placeholder: unread badge and notification drawer

See [docs/screenshots/README.md](docs/screenshots/README.md) for safe capture guidance.

## Architecture

The API owns persistence, authentication, monitoring jobs, and external protocol operations. The React client calls authenticated REST endpoints and receives monitoring events over SignalR. Configuration backup follows this extension path:

```text
ConfigBackupService
  -> ConfigBackupProviderResolver
      -> CiscoIosConfigBackupProvider (implemented)
      -> Fortinet provider (not implemented)
      -> future vendor/platform providers
  -> ISshCommandTransport
  -> ConfigBackupStorageService (history and diff)
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the main data flows.

## Known limitations

- SNMP support is read-only v2c; SNMPv3 is not implemented, and v2c community data is not encrypted on the wire.
- Configuration retrieval currently has only a Cisco IOS/IOS-XE provider. Selecting Fortinet returns a clear not-implemented result and sends no guessed command.
- Configuration backups are user-triggered; scheduled backup is not implemented.
- LLDP topology discovery is on-demand and depends on accessible SNMP/LLDP data. It is not a continuously reconciled topology database.
- SQLite and in-process background workers target a single-instance portfolio deployment, not horizontal multi-node operation.
- Current reachability status counters are held in process memory and reset when the API restarts; persisted check history remains available.
- Email delivery requires user-supplied SMTP settings. Delivery depends on the chosen SMTP provider and is not exercised by CI.
- The application has one authenticated-user access level; RBAC, multi-tenancy, and maintenance windows are outside the current scope.
- ICMP, SNMP, SSH, port scanning, Wake-on-LAN, and reverse DNS behavior can vary with operating-system permissions, routing, firewalls, and device policy.

## Responsible real-device testing

Do not test against company or real network devices without explicit authorization from the responsible network owner. Begin with one approved lab device, read-only access, and conservative polling. Never run Port Scanner, Wake-on-LAN, Configuration Backup, broad network discovery, or any state-changing action without separate permission. Fortinet-specific guidance is in [docs/FORTINET_TEST_PLAN.md](docs/FORTINET_TEST_PLAN.md).
