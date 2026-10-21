## MODIFIED Requirements

### Requirement: Persistent settings
The application SHALL persist user settings locally, including save folder, hotkeys, quality defaults, audio device choices, overlay defaults, segmented recording rules, appearance theme, UI language, recording-bar visibility, LAN enabled state, LAN listen port, LAN access key, silent mode, launch-to-tray, hide-tray-icon, and close-to-tray, and restore them on the next launch. The last capture mode and display MUST NOT be restored.

#### Scenario: Settings survive restart
- **WHEN** the user changes the save folder and restarts the application
- **THEN** the save folder is the previously chosen path

#### Scenario: Theme and language survive restart
- **WHEN** the user sets dark theme and Japanese UI and restarts
- **THEN** the application opens in dark appearance with Japanese UI strings

#### Scenario: LAN port survives restart
- **WHEN** the user sets the LAN listen port to a valid unused port and restarts
- **THEN** LAN access, if enabled, binds that same port
