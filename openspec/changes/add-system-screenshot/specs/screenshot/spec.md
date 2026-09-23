## Purpose

让用户像使用系统截图一样，框选区域、点选窗口或截取整屏，然后在选区旁选择保存成文件或复制到剪贴板，而不必开启一段录制。

## ADDED Requirements

### Requirement: Snip overlay
The application SHALL open a full-screen dimmed overlay for a still screenshot. The overlay MUST offer rectangular region, window, and entire display. The overlay MUST cover the virtual desktop so a selection can start on any connected monitor.

#### Scenario: Rectangular region
- **WHEN** the user drags a rectangle on the overlay and releases
- **THEN** that rectangle stays visible and save and copy actions appear beside it

#### Scenario: Window
- **WHEN** the user chooses window mode and clicks a visible top-level window
- **THEN** that window's bounds stay highlighted and save and copy actions appear beside them

#### Scenario: Entire display
- **WHEN** the user chooses entire-display mode and clicks a monitor
- **THEN** that monitor's bounds stay highlighted and save and copy actions appear beside them

### Requirement: Cancel without a file
The user MUST be able to leave the overlay without creating a screenshot file and without changing the clipboard image.

#### Scenario: Escape cancels
- **WHEN** the overlay is open and the user presses Esc
- **THEN** the overlay closes, no new PNG is written, and the clipboard image is unchanged

#### Scenario: Empty drag cancels
- **WHEN** the user releases a rectangular drag that does not form a region
- **THEN** the overlay closes, save and copy actions are not shown, and no new PNG is written

### Requirement: Save or copy
After a selection is shown, the overlay SHALL offer save and copy beside that selection. Save MUST write a PNG in the configured save folder and MUST NOT place the image on the clipboard. Copy MUST place the image on the clipboard and MUST NOT write a PNG. The file name MUST distinguish a saved screenshot from a recording. Choosing either action MUST close the overlay.

#### Scenario: Save only
- **WHEN** the user chooses save and the save folder is writable
- **THEN** a new PNG exists in the save folder, the overlay closes, and the clipboard image is unchanged

#### Scenario: Copy only
- **WHEN** the user chooses copy and the clipboard accepts the image
- **THEN** pasting an image into another application pastes that screenshot, the overlay closes, and no new PNG is written

#### Scenario: Save fails
- **WHEN** the user chooses save and the save folder cannot accept the PNG
- **THEN** the application tells the user, no success is reported, and the clipboard image is unchanged

#### Scenario: Copy fails
- **WHEN** the user chooses copy and the clipboard cannot accept the image
- **THEN** the application tells the user, no new PNG is written, and success is not reported

### Requirement: Screenshot entry points
The home mode row SHALL show a screenshot tile in the slot that previously held game capture. Activating that tile MUST open the snip overlay and MUST NOT change the selected recording mode. The same overlay MUST open from the configured screenshot hotkey when global hotkeys are enabled. The tray menu and the recording bar MUST NOT gain a screenshot action.

#### Scenario: Home tile
- **WHEN** the user activates the screenshot tile on the home mode row
- **THEN** the snip overlay opens and the previously selected recording mode stays selected

#### Scenario: Hotkey while unfocused
- **WHEN** global hotkeys are enabled and another application is focused
- **THEN** the configured screenshot hotkey opens the snip overlay

### Requirement: Recording stays independent
A screenshot MUST NOT start, pause, resume, or stop a recording session. While a recording is active, the snip overlay MUST be excluded from the recording when the operating system allows excluding a window from capture. Opening a screenshot during recording MUST leave the main window hidden and MUST leave the recording bar visible.

#### Scenario: Shot during recording
- **WHEN** a recording is in progress and the user completes a screenshot with save or copy
- **THEN** the recording continues with the same output file, and the main window stays hidden

#### Scenario: Cancel during recording
- **WHEN** a recording is in progress and the user cancels the overlay
- **THEN** the recording continues, the recording bar stays visible, and no new PNG is written
