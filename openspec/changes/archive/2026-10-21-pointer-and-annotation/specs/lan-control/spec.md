## ADDED Requirements

### Requirement: Web settings include pointer and annotation
The web settings page SHALL offer the pointer, click highlight and annotation switches, and `PATCH /api/v1/settings` SHALL accept `screenAids` with `showCursor`, `highlightClicks` and `annotation`.

#### Scenario: Turn on click highlight from the web
- **WHEN** a client patches `{"screenAids":{"highlightClicks":true}}`
- **THEN** the desktop setting turns on and the next screen recording highlights clicks
