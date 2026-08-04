# Security and responsible-use checklist

## Secrets

- Never commit real IP addresses, SNMP communities, SSH credentials, SMTP credentials, cookies, databases, or Data Protection keys.
- Use fake values in tests and documentation.
- Store the production database and Data Protection key ring in separate, access-controlled, backed-up locations.
- Treat a lost key ring as permanent loss of access to encrypted network and SMTP credentials.
- Rotate affected credentials if both the database and key ring may have been disclosed.

## Deployment

- Use HTTPS in production and do not expose the development HTTP profile publicly.
- Restrict CORS origins and filesystem permissions.
- Set a unique bootstrap admin password through environment variables, then remove those variables when operationally appropriate.
- Protect backups and logs; configuration output can itself contain sensitive information.

## Network access

- Obtain explicit permission before connecting to company or real devices.
- Prefer a dedicated lab device and read-only credentials.
- SNMP v2c is not encrypted on the wire; use only on trusted, isolated networks until an approved SNMPv3 design exists.
- Start with one device and conservative polling. Stop when unexpected latency, load, authentication failures, or network behavior appears.
- Port scanning, Wake-on-LAN, configuration retrieval, and broad discovery need separate authorization.

## Development and CI

Automated tests must use mocks, stubs, in-memory/test databases, and documentation-only example addresses. CI must not require repository secrets or reach real devices, SMTP servers, or private services.
