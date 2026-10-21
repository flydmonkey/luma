## Purpose

让局域网网页上的用户框选主机屏幕的一块静止画面，然后由浏览器保存或复制，而不必先录一段视频，也不必把文件写回主机。

## ADDED Requirements

### Requirement: Web screenshot entry
The web record page SHALL show a screenshot action on the mode row. Activating it MUST open the screenshot overlay and MUST NOT change the selected recording mode.

#### Scenario: Tile leaves the recording mode
- **WHEN** display mode is selected and the user activates the screenshot action
- **THEN** the screenshot overlay opens and display mode stays selected

### Requirement: Host still on a browser canvas
The overlay SHALL show one still image of the host virtual desktop, covering every connected monitor, captured at the moment the overlay opens. The delivered still MUST use the host's pixel dimensions. The image MUST be the host PC's screen, not the screen of the device running the browser. The overlay MUST NOT keep a live picture of the host after that still is shown.

#### Scenario: Phone sees the host desktop
- **WHEN** a phone on the LAN opens the screenshot overlay
- **THEN** the picture is the Windows host's virtual desktop from that moment, not the phone's own screen

#### Scenario: Picture does not keep moving
- **WHEN** the host desktop changes after the still is shown
- **THEN** the overlay keeps the already shown still until the user closes it or asks for a new one

### Requirement: Three selection methods
The overlay MUST offer rectangular region, window, and entire display. A finished selection MUST stay visible, and save and copy MUST appear beside it. A rectangular selection smaller than 8 by 8 host pixels MUST NOT count as a region.

#### Scenario: Rectangular region
- **WHEN** the user drags a rectangle on the still and releases a region of at least 8 by 8 host pixels
- **THEN** that rectangle stays visible and save and copy appear beside it

#### Scenario: Window
- **WHEN** the user chooses window selection and clicks a listed visible window on the still
- **THEN** that window's bounds stay highlighted and save and copy appear beside them

#### Scenario: Entire display
- **WHEN** the user chooses entire-display selection and clicks a monitor on the still
- **THEN** that monitor's bounds stay highlighted and save and copy appear beside them

#### Scenario: Missed window click
- **WHEN** the user chooses window selection and clicks a point that is not inside a listed window
- **THEN** no selection is confirmed and save and copy stay hidden

### Requirement: Cancel without a file
The user MUST be able to leave the overlay without a browser download, without a new host file, and without changing the host clipboard or the browser clipboard image. Cancel MUST NOT send another request to the host.

#### Scenario: Escape cancels
- **WHEN** the overlay is open and the user presses Esc
- **THEN** the overlay closes, the browser does not download a file, no new host PNG is written, and neither clipboard image changes

#### Scenario: Empty drag cancels
- **WHEN** the user releases a rectangular drag that does not form a region
- **THEN** the overlay closes, save and copy are not shown, and no file is downloaded or written

### Requirement: Save or copy in the browser
After a selection is shown, the overlay SHALL offer save and copy beside that selection. Save MUST download one PNG through the browser onto the device running the browser, and MUST NOT send a request to the host, write a host file, or change either clipboard. Copy MUST place the image on the browser clipboard, and MUST NOT download a file, send a request to the host, or change the host clipboard. The downloaded file name MUST distinguish the screenshot from a recording. The saved or copied pixels MUST match the selection in the delivered still. Choosing either action MUST close the overlay.

#### Scenario: Save only
- **WHEN** the user chooses save and the browser accepts the download
- **THEN** the browser downloads a PNG whose pixel size equals the selection, the overlay closes, the host save folder is unchanged, and both clipboards are unchanged

#### Scenario: Copy only
- **WHEN** the user chooses copy and the browser accepts the image
- **THEN** pasting in that browser pastes the screenshot, the overlay closes, the browser does not download a file, and the host save folder and host clipboard are unchanged

#### Scenario: Save fails
- **WHEN** the user chooses save and the browser cannot download the PNG
- **THEN** the page tells the user, no success is reported, and the host save folder and both clipboards stay unchanged

#### Scenario: Copy fails
- **WHEN** the user chooses copy and the browser cannot accept the image
- **THEN** the page tells the user, the browser does not download a file, and success is not reported

### Requirement: Recording stays independent
A web screenshot MUST NOT start, pause, resume, or stop a recording session. Saving or copying MUST NOT add a file on the host, and the web library MUST stay unchanged.

#### Scenario: Shot during recording
- **WHEN** a recording is in progress and the user completes a web screenshot with save or copy
- **THEN** the recording continues with the same output file

#### Scenario: Host library unchanged
- **WHEN** the user saves a screenshot in the browser and then opens the web library
- **THEN** the library lists the same items as before the screenshot

### Requirement: Screenshot copy follows the active language
Screenshot, save, copy, cancel, and failure text on the web overlay MUST appear in the active interface language.

#### Scenario: Chinese labels
- **WHEN** the web interface language is 简体中文 and the overlay is open
- **THEN** the screenshot action and the save and copy labels are Chinese, not missing-key placeholders
