## MODIFIED Requirements

### Requirement: Global hotkeys and tray
The application SHALL let the user enable global hotkeys for start, pause/resume, stop, and screenshot. The factory screenshot hotkey SHALL be Ctrl+Shift+S. It MUST NOT be Win+Shift+S and MUST NOT unregister that system shortcut. The application SHALL provide a notification-area icon that can show recording state, open the main window or library, and exit. Closing the main window SHALL go to the tray when close-to-tray is enabled. The tray menu MUST NOT include a screenshot action.

#### Scenario: Stop from another window
- **WHEN** hotkeys are enabled and a recording is in progress
- **THEN** pressing the configured stop hotkey ends the recording even if the application window is not focused

#### Scenario: Tray continues recording
- **WHEN** a recording is in progress and the user closes the main window to the tray
- **THEN** recording continues and the tray icon indicates that recording is active

#### Scenario: Screenshot hotkey leaves the system snip
- **WHEN** the user enables global hotkeys with factory settings
- **THEN** Ctrl+Shift+S opens the application screenshot overlay and Win+Shift+S still belongs to Windows

### Requirement: Restore factory defaults
Settings SHALL provide an explicit restore-defaults action that rewrites factory settings after confirmation.

#### Scenario: Reset appearance and chrome
- **WHEN** the user confirms restore defaults
- **THEN** theme returns to dark, the recording bar is shown again, language returns to follow system, the LAN listen port returns to `12345`, the screenshot hotkey returns to Ctrl+Shift+S, and other factory settings replace the previous file
