# NetScope architecture

NetScope has an ASP.NET Core API, a React client, and a SQLite database. Authentication uses an HTTP-only Identity cookie. State-changing requests also require the antiforgery token obtained by the frontend API client.

## Device monitoring

1. A user creates a device through the authenticated Devices API.
2. `DeviceMonitoringService` periodically selects enabled devices.
3. `IPingService` performs bounded ICMP checks.
4. Results and history are stored by Entity Framework Core.
5. `DeviceStatusTracker` applies failure/recovery thresholds.
6. `SignalRMonitoringUpdatePublisher` sends status updates to connected clients.

## SNMP monitoring

The SNMP Explorer performs user-triggered, read-only v2c queries. Background bandwidth monitoring uses a saved encrypted SNMP credential, selected interfaces, and 64-bit interface counters. The first valid sample creates a baseline; the next sample can produce a rate. Samples are retained for the configured number of days.

## Incidents

Bandwidth threshold evaluators compare consecutive samples with saved policies. Interface status evaluation requires configured confirmation samples rather than treating one displayed `Down` value as an incident. `IncidentService` opens, updates, and resolves incidents in the database.

## Notifications and email

When an incident opens, `IncidentNotificationPublisher` creates an in-app notification. The Notification Center reads and marks those records through the API. `NotificationDeliveryPlanner` schedules email delivery when the channel is enabled. A background processor uses MailKit through `IEmailSenderTransport`; backend tests replace external delivery with stubs.

## Credential encryption

Identity hashes login passwords. Network and SMTP secrets follow a different path: the service passes a secret to `ISecretProtector`, which uses ASP.NET Core Data Protection and a persistent key ring. API responses expose metadata such as `hasSecret` or `hasPassword`, never the stored value.

## Configuration backup history

1. The user selects a platform and supplies a one-time or saved SSH credential.
2. `ConfigBackupProviderResolver` selects a registered provider.
3. The Cisco provider supplies its read-only running-configuration command.
4. `ISshCommandTransport` executes with bounded connection and command timeouts.
5. The result remains in the browser until the user explicitly saves it.
6. `ConfigBackupStorageService` stores a content hash and configuration, avoids duplicate content, lists history, and produces bounded line diffs.

Fortinet is intentionally an extension point only. No provider or guessed production command exists yet.
