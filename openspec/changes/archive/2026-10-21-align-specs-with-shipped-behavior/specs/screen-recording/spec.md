## REMOVED Requirements

### Requirement: Recording session control
**Reason**: 停录后留在录制页并显示「已保存」浮层的做法已在提交 `7ffbc5c` 中改为直接进入预览。
**Migration**: 使用下面新增的「Session control and stop preview」。

### Requirement: Automation
**Reason**: 登录后自动开录和定时录制已在提交 `361e258` 中去掉，只保留分段录制。
**Migration**: 使用下面新增的「Segmented recording」。需要定时录制时，可以用 Windows 任务计划配合控制接口。

## ADDED Requirements

### Requirement: Session control and stop preview
The application SHALL let the user start, pause, resume, and stop a single active recording session, MUST display elapsed time while a session is active or paused, and MUST keep the home surface responsive after stop is requested. The elapsed clock SHALL freeze at the moment stop is requested. WHEN stop completes with a playable file and the main window is visible or is brought back, the application SHALL show a processing indicator and then open that file in the library preview. WHEN the main window stays hidden for the session, or silent mode is on and the stop came from the LAN control API, the application MUST NOT change pages or show the main window.

#### Scenario: Pause and resume
- **WHEN** the user pauses an active recording and later resumes
- **THEN** the final file is one continuous recording that omits the paused interval

#### Scenario: Only one session
- **WHEN** a recording session is already active
- **THEN** starting another session MUST be rejected until the current session is stopped

#### Scenario: Stop opens the new file
- **WHEN** the user stops a recording from the desktop and the stop produced a playable file
- **THEN** a processing indicator appears and the library preview then plays the new file

#### Scenario: Silent web stop stays hidden
- **WHEN** silent mode is on and a web client stops a recording
- **THEN** the main window stays hidden and no page changes

### Requirement: Segmented recording
The application SHALL support splitting a long recording into sequential playable files by elapsed time or file size. The application MUST NOT offer start-at-logon recording or scheduled start and stop.

#### Scenario: Split by duration
- **WHEN** segmented recording is enabled with a 10-minute interval and recording continues past 10 minutes
- **THEN** the first segment is saved as a playable file and a new segment continues without a user stop action

#### Scenario: No logon or schedule controls
- **WHEN** the user opens desktop or web settings
- **THEN** there is no control to start recording at sign-in or on a schedule

## MODIFIED Requirements

### Requirement: Four capture modes
The application SHALL provide display, region, window, and audio-only capture modes. Each launch SHALL start in display mode on the default display and MUST NOT restore the previous launch's mode or display. The home mode row SHALL show those four modes plus a screenshot tile. The screenshot tile MUST NOT be a capture mode.

#### Scenario: Launch starts in display mode
- **WHEN** the user last recorded in window mode on a second display and restarts the application
- **THEN** the home page selects display mode on the default display

#### Scenario: Fullscreen desktop capture
- **WHEN** the user selects display mode and starts recording
- **THEN** the output video contains the selected display's visible desktop content

#### Scenario: Region capture
- **WHEN** the user selects region mode, draws a rectangle, and starts recording
- **THEN** the output video contains only pixels inside that rectangle

#### Scenario: Window capture while occluded
- **WHEN** the user selects a target window in window mode and another window later covers it
- **THEN** the recording MUST continue to capture the selected window's content rather than the covering window, unless the target window is minimized

#### Scenario: Game mode is gone
- **WHEN** the user looks at the home mode row and at settings
- **THEN** there is no game capture mode to select
