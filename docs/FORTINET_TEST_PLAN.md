# Safe Fortinet test plan

This plan is for a beginner running the first approved compatibility checks. It does not mean Fortinet Configuration Backup is implemented. NetScope currently stops that selection with `Fortinet provider not implemented yet` and sends no guessed command.

## Before testing

- Get clear written permission from the responsible network owner.
- Use a dedicated test device, or exactly one device named in the permission.
- Use a read-only SNMP credential.
- Set polling to at least 60 seconds.
- Use only one device during the first test.
- Record the approved time window, responsible engineer, and stop condition outside this repository.

Do not put an IP address, username, password, or community in this document, an issue, a screenshot, or a commit.

## Safe first tests

Perform one step at a time. Confirm the result with the responsible engineer before continuing.

1. Ping the single approved device.
2. Read SNMP system information.
3. Read the interface list.
4. Read interface traffic counters and wait at least 60 seconds before the next poll.
5. Read LLDP information only if the permission explicitly includes it.

These checks should remain read-only. Watch device CPU, response time, application errors, and authentication failures.

## Never run without separate permission

- Port Scanner
- Wake-on-LAN
- Configuration Backup
- Enabling or disabling an interface
- Restart or reboot
- Firmware operations
- Any command that changes settings
- Network-wide scanning or discovery

## If something goes wrong

1. Stop the test immediately.
2. Disable monitoring for the device.
3. Tell the responsible engineer what step ran and what happened.
4. Do not retry repeatedly or create an automatic retry loop.

## Future provider validation

Before implementing a Fortinet backup provider, obtain vendor documentation for the exact approved platform and firmware. Review the command with the network owner, add a fake-transport unit test first, and run the real command only in the separately authorized lab. Do not promote a provider to production support until output, pagination, privilege level, timeout, redaction, and failure behavior are verified.
