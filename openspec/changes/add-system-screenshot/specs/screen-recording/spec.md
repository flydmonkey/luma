## REMOVED Requirements

### Requirement: Five capture modes
**Reason**: 游戏录制从主页和设置中移除，原来的格子改为截图动作。
**Migration**: 要录游戏窗口时改用窗口模式。主页模式行变为显示器、区域、窗口、截图、录音；截图不开始录制。

### Requirement: Target is required before start
**Reason**: 该要求把游戏和窗口绑在同一条目标门禁里。游戏模式去掉后，门禁只保留区域和窗口。
**Migration**: 使用下面新增的「Region and window need a target」。

## ADDED Requirements

### Requirement: Four capture modes
The application SHALL provide display, region, window, and audio-only capture modes, and MUST require the user to choose one of those modes before the first recording in a session can start. The home mode row SHALL show those four modes plus a screenshot tile. The screenshot tile MUST NOT be a capture mode.

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

### Requirement: Region and window need a target
The application SHALL require a concrete capture target before a user-initiated recording can start in region or window mode, and MUST NOT silently substitute the full display or an empty window handle. Switching to those modes while idle SHALL open the corresponding picker.

#### Scenario: Region without a rectangle
- **WHEN** the current mode is region and no capture rectangle has been confirmed
- **THEN** the start action MUST stay disabled or fail with a visible explanation, and no recording file is created

#### Scenario: Window without a target
- **WHEN** the current mode is window and no window has been chosen
- **THEN** the start action MUST stay disabled or fail with a visible explanation, and no recording file is created
