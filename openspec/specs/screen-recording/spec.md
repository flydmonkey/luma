# screen-recording Specification

## Purpose

让用户按场景选择显示器、区域、窗口、游戏或纯音频，稳定录下屏幕或声音，并在开录、暂停、停止时保持现有 Luma 的交互习惯。

## Requirements

### Requirement: Five capture modes
The application SHALL provide display, region, window, game, and audio-only capture modes, and MUST require the user to choose a mode before the first recording in a session can start.

#### Scenario: Fullscreen desktop capture
- **WHEN** the user selects display mode and starts recording
- **THEN** the output video contains the selected display's visible desktop content

#### Scenario: Region capture
- **WHEN** the user selects region mode, draws a rectangle, and starts recording
- **THEN** the output video contains only pixels inside that rectangle

#### Scenario: Window capture while occluded
- **WHEN** the user selects a target window in window mode and another window later covers it
- **THEN** the recording MUST continue to capture the selected window's content rather than the covering window, unless the target window is minimized

#### Scenario: Game capture avoids black frames
- **WHEN** the user selects game mode, picks a running DirectX or OpenGL game that is windowed or borderless, and starts recording
- **THEN** the output video shows the game scene instead of a black or flickering frame

#### Scenario: Exclusive fullscreen fallback
- **WHEN** the selected game is in exclusive fullscreen and capture cannot attach
- **THEN** the application MUST not silently produce a black video; it MUST fail with a clear message or offer a borderless/windowed workaround

### Requirement: Target is required before start
The application SHALL require a concrete capture target before a user-initiated recording can start in region, window, or game mode, and MUST NOT silently substitute the full display or an empty window handle. Switching to those modes while idle SHALL open the corresponding picker.

#### Scenario: Region without a rectangle
- **WHEN** the current mode is region and no capture rectangle has been confirmed
- **THEN** the start action MUST stay disabled or fail with a visible explanation, and no recording file is created

#### Scenario: Window or game without a target
- **WHEN** the current mode is window or game and no window has been chosen
- **THEN** the start action MUST stay disabled or fail with a visible explanation, and no recording file is created

### Requirement: Countdown then leave the picture
A user-initiated start SHALL show a 3-second countdown, then hide the main window before frames are written, and MUST keep the application window out of the recorded picture by default. Automated or API starts MAY skip the countdown but MUST still keep the application out of the picture.

#### Scenario: Manual start countdown
- **WHEN** the user starts recording from the home start button
- **THEN** a visible 3-second countdown runs first, the main window is hidden before capture begins, and the application window does not appear in the output

#### Scenario: Cancel during countdown
- **WHEN** the user cancels during the countdown
- **THEN** no recording session starts and the home surface returns to idle

### Requirement: Recording session control
The application SHALL let the user start, pause, resume, and stop a single active recording session, MUST display elapsed time while a session is active or paused, and MUST keep the home surface responsive after stop is requested. The elapsed clock SHALL freeze at the moment stop is requested.

#### Scenario: Pause and resume
- **WHEN** the user pauses an active recording and later resumes
- **THEN** the final file is one continuous recording that omits the paused interval

#### Scenario: Only one session
- **WHEN** a recording session is already active
- **THEN** starting another session MUST be rejected until the current session is stopped

#### Scenario: Stop stays on the record page
- **WHEN** the user stops a recording that produced a playable file
- **THEN** the record page remains visible, a processing then saved overlay offers preview and dismiss, and the application MUST NOT automatically navigate to the library

### Requirement: Home quick audio and quality controls
The idle record page SHALL let the user toggle system audio, toggle the microphone, and change the quality preset without opening settings.

#### Scenario: Toggle microphone on the home page
- **WHEN** the user turns the home microphone control off and starts a recording
- **THEN** the session is created with microphone capture disabled

#### Scenario: Audio-only is a visible mode
- **WHEN** the user chooses audio-only from the home mode control
- **THEN** the upcoming-target summary states that only audio will be captured and the output is an audio file

### Requirement: Always-on-top recording bar
While a session is recording or paused and the user has not hidden the recording bar, the application SHALL show a compact always-on-top bar with elapsed time, pause or resume, stop, and a microphone mute toggle. The bar MUST be excluded from capture. WHEN silent mode is on, a session started through the LAN control API MUST NOT show the bar.

#### Scenario: Controls after start
- **WHEN** a recording session becomes active and showing the bar is enabled
- **THEN** the floating bar is visible above other windows and can pause, resume, stop, and mute the microphone without opening the main window

#### Scenario: Silent web sessions hide the bar
- **WHEN** silent mode is on and the web client starts a recording
- **THEN** the floating recording bar does not appear

### Requirement: System and microphone capture
The application SHALL let the user independently enable system playback audio and microphone capture, mix enabled sources onto the same timeline, and persist the last valid microphone device.

#### Scenario: Mixed sources
- **WHEN** both system playback and microphone are enabled
- **THEN** the output contains both sources aligned to the same timeline

#### Scenario: Missing microphone
- **WHEN** the previously selected microphone is disconnected
- **THEN** the application MUST disable microphone capture or fall back to the default device and tell the user

### Requirement: Audio-only recording
The application SHALL provide an audio-only mode that records enabled audio sources without screen frames and saves an audio file.

#### Scenario: Audio-only output
- **WHEN** the user enables audio-only mode and completes a recording
- **THEN** the application saves an audio file in the library instead of a video file

### Requirement: Webcam and custom watermarks
The application SHALL let the user enable a camera overlay and add text, image, and at most one timestamp watermark, each with position, size, and opacity. The application MUST NOT burn a product brand watermark the user cannot remove.

#### Scenario: Overlay appears in output
- **WHEN** the camera overlay is enabled and a recording completes
- **THEN** the output video contains the live camera image at the configured size and position

#### Scenario: Clean output by default
- **WHEN** the user records with no custom watermarks configured
- **THEN** the output contains no application brand watermark

### Requirement: Quality presets
The application SHALL let the user choose resolution presets from 720p to 4K and frame rates of 10, 15, 30, or 60 fps, clamped to the source display. Higher quality presets MUST NOT be time-limited or paywalled.

#### Scenario: Unrestricted high quality
- **WHEN** the user records with a high-quality or 60 fps preset for longer than 30 seconds
- **THEN** the recording continues until the user stops it or a configured automation limit is reached

### Requirement: Automation
The application SHALL support start-at-logon, a scheduled start and stop, and splitting a long recording into sequential playable files by elapsed time or file size.

#### Scenario: Split by duration
- **WHEN** segmented recording is enabled with a 10-minute interval and recording continues past 10 minutes
- **THEN** the first segment is saved as a playable file and a new segment continues without a user stop action

#### Scenario: Overlap rejected
- **WHEN** a schedule would start while another recording session is already active
- **THEN** the application MUST skip or queue the schedule and notify the user instead of starting a second session

### Requirement: Crash-safe output
The application SHALL write recordings so that an unexpected process exit leaves a file that is either immediately playable or repairable by the library repair action.

#### Scenario: Abnormal stop
- **WHEN** the application terminates while a recording is in progress
- **THEN** a partial file exists in the save folder and can be opened after repair if it is not already playable
