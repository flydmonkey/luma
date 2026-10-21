## ADDED Requirements

### Requirement: Web settings keep the pen style
`PATCH /api/v1/settings` SHALL accept `screenAids.inkColor` and `screenAids.inkWidth`. Because a patch replaces the whole `screenAids` object, the web settings page MUST send back the pen style it loaded.

#### Scenario: Save from the web
- **WHEN** the desktop pen is blue and the user turns on click highlight from the web page
- **THEN** the pen stays blue
