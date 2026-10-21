## ADDED Requirements

### Requirement: Partial network binding
WHEN LAN access is enabled, the server SHALL always listen on the loopback address and SHALL try each local network address on its own. Addresses that bind SHALL stay reachable even when other addresses fail. WHEN some network addresses fail, the desktop MUST tell the user which addresses other devices cannot reach. WHEN every network address fails, the desktop MUST tell the user that only this PC can connect. WHEN the loopback address cannot bind, the server MUST NOT start and MUST report that the port is taken or cannot be bound.

#### Scenario: One adapter fails
- **WHEN** LAN access is turned on and one network address is already taken by another program
- **THEN** the server listens on loopback and on the remaining network addresses, and the desktop names the address that was skipped

#### Scenario: No network address binds
- **WHEN** LAN access is turned on and no network address can be bound
- **THEN** the server listens on loopback only and the desktop says that only this PC can connect

#### Scenario: Loopback taken
- **WHEN** another program already listens on the loopback address and port
- **THEN** the server does not start and the desktop reports that the port is taken
