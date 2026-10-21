# desktop-app Specification

## Purpose

提供无需账号的 Windows 本地桌面壳，让用户在三页之间完成录制、片库和设置，并持久化热键、托盘与外观偏好。

## Requirements

### Requirement: Local-only desktop application
The application SHALL run as a local Windows 10 version 1809 or later desktop app without requiring sign-in, account creation, or a membership check. The window title and taskbar name MUST be Luma.

#### Scenario: Launch without account
- **WHEN** the user starts the application on a supported Windows version
- **THEN** the main window opens and recording features are available without login

#### Scenario: Reject unsupported OS
- **WHEN** the application is launched on a Windows version below Windows 10 1809
- **THEN** the application MUST refuse to start recording and MUST tell the user that Windows 10 1809 or later is required

### Requirement: Three-page main shell
The main window SHALL expose exactly three primary surfaces — 录制, 我的视频, and 设置 — and MUST keep the same compact title-bar navigation as the existing Luma product.

#### Scenario: Default home is record
- **WHEN** the application launches with no deep-link
- **THEN** the 录制 page is visible

#### Scenario: Switch to library and settings
- **WHEN** the user clicks 我的视频 or 设置 in the title bar
- **THEN** that surface replaces the previous page without opening a second main window

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

### Requirement: Light or dark appearance
The application SHALL offer light and dark themes, default to dark, and MUST NOT expose a follow-system theme option.

#### Scenario: Factory default is dark
- **WHEN** settings have never been saved or have been reset
- **THEN** the application uses dark appearance

### Requirement: Supported UI languages
The application SHALL offer follow system, English, Simplified Chinese, Traditional Chinese, Japanese, and Korean. WHEN the preference is follow system, the effective language SHALL come from the OS UI language, with English as the fallback for unsupported OS languages.

#### Scenario: Settings lists the locales
- **WHEN** the user opens the language control
- **THEN** the choices are follow system plus the five supported locales

#### Scenario: Unsupported system language
- **WHEN** language is follow system and the OS UI language is not one of the five supported locales
- **THEN** the application UI is English

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

### Requirement: Single instance and launch-to-tray
The application SHALL run as a single instance. WHEN launch-to-tray is enabled, a later sign-in start MUST open in the tray without showing the main window first.

#### Scenario: Second launch focuses the existing instance
- **WHEN** the application is already running and the user starts it again
- **THEN** no second process takes over recording and the existing instance is focused or signaled

#### Scenario: Launch to tray
- **WHEN** launch-to-tray is enabled and the user signs in
- **THEN** the application is present in the tray and the main window is not shown unless the user opens it

### Requirement: Restore factory defaults
Settings SHALL provide an explicit restore-defaults action that rewrites factory settings after confirmation.

#### Scenario: Reset appearance and chrome
- **WHEN** the user confirms restore defaults
- **THEN** theme returns to dark, the recording bar is shown again, language returns to follow system, the LAN listen port returns to `12345`, the screenshot hotkey returns to Ctrl+Shift+S, and other factory settings replace the previous file

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
