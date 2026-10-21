## ADDED Requirements

### Requirement: Settings apply at once and save without stalling
A settings change SHALL take effect immediately. Rapid changes such as dragging a slider SHALL be written to disk once after a short pause, and any pending write MUST be flushed when the window closes. Global hotkeys SHALL be registered again only when hotkey settings change or the last registration failed. The logon task SHALL be updated only when launch-to-tray changes, and MUST NOT block the UI. Changes made from the web app and restoring defaults SHALL apply hotkeys and the logon task the same way.

#### Scenario: Dragging a slider
- **WHEN** the user drags an overlay slider across many steps
- **THEN** the preview follows the slider and the settings file is written once after the drag

#### Scenario: Hotkey changed from the web app
- **WHEN** a web client changes the start hotkey
- **THEN** the desktop registers the new hotkey without a restart

### Requirement: Messages follow the UI language
Common errors and recording warnings shown by the desktop SHALL use the current UI language, including messages that come from the recording engine. Text the app does not recognize SHALL be shown unchanged. Warnings raised when recording starts, such as unavailable system audio, microphone or camera, SHALL be shown at start. The LAN API error strings documented in the OpenAPI description are not covered by this requirement.

#### Scenario: English UI and missing microphone
- **WHEN** the UI language is English and recording starts without an available microphone
- **THEN** the desktop shows an English warning at start
