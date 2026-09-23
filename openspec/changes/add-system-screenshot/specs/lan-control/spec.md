## MODIFIED Requirements

### Requirement: Web and API drive the same session
The 录制 page and the control API SHALL start, pause, resume, and stop the same recording session as the desktop, using the same target and quality rules. Region mode MUST require a confirmed rectangle. Window mode MUST require a listed target id. The API MUST NOT offer game mode and MUST reject a start request that asks for it.

#### Scenario: Start after picking a window
- **WHEN** the user selects a window from the labeled preview list and starts recording
- **THEN** the session becomes active and later status shows it as recording

#### Scenario: API start without a required target fails
- **WHEN** the mode is region or window and no target is confirmed
- **THEN** the start call fails with an explanation and no output file is created

#### Scenario: Game mode is rejected
- **WHEN** a client asks the API to start recording in game mode
- **THEN** the start call fails with an explanation and no output file is created

### Requirement: Targets include labels and previews
The control API SHALL list selectable displays, windows, cameras, and microphones with a stable id and visible label. Display and window items SHALL include a preview image when a snapshot can be taken; snapshot failure MUST NOT omit the item. The target list MUST NOT include games.

#### Scenario: Window list carries title and picture
- **WHEN** the client asks for window targets
- **THEN** each item includes an id, a title, and either a preview image or an explicit missing-preview mark

#### Scenario: Game targets are absent
- **WHEN** the client asks for selectable targets
- **THEN** the response contains no game entries
