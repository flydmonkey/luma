# lan-control Specification

## Purpose

在同一本机 HTTP 服务上提供与桌面同构的三页网页和 JSON 控制接口，让局域网设备远程操作正在运行的 Luma，而不把媒体上传到云端。

## Requirements

### Requirement: LAN server and optional access key
The application SHALL expose a local HTTP server when LAN access is enabled. WHEN an access key is stored, the web app, control routes, media, and posters MUST require a valid unlock. The API documentation, the OpenAPI document, the skill files, the privacy policy, and the terms of service MAY be served without an unlock, and MUST NOT contain recording file names, settings values, or screen pixels. WHEN the key is empty, every route MUST be reachable without a key. WHEN the server is off, it MUST NOT accept connections. The factory-default listen port SHALL be `12345`, so the loopback URL is `http://127.0.0.1:12345` until the user changes it.

#### Scenario: Key empty allows access
- **WHEN** LAN access is on and the access key is empty
- **THEN** a client on the same machine can open the web app and call control routes without submitting a key

#### Scenario: Key blocks anonymous control
- **WHEN** an access key is stored and the client has no valid unlock
- **THEN** control routes do not start a recording, change settings, or list library file names

#### Scenario: Documentation stays readable while locked
- **WHEN** an access key is stored and the client has no valid unlock
- **THEN** the client can still read the API documentation and the OpenAPI document

#### Scenario: Server off
- **WHEN** LAN access is off
- **THEN** control routes and the web app are not reachable

### Requirement: Configurable LAN listen port
The desktop settings page SHALL let the user set the LAN listen port. The factory default and restore-defaults value MUST be `12345`. The accepted range MUST be integers from `1` through `65535`. Values outside that range, including `123456`, MUST be rejected with a visible explanation and MUST NOT change the bound port. WHEN the port is already in use, the application MUST fail to start or restart the LAN server with a visible conflict message and MUST NOT silently bind a different port. Changing the port on the desktop SHALL persist, update the displayed LAN address, and rebind the running server when LAN access is on. The web settings page and control API MUST NOT change the listen port.

#### Scenario: Factory default avoids the legacy port
- **WHEN** settings have never been saved or have been reset
- **THEN** LAN access, once enabled, listens on `12345` and the settings page shows `http://127.0.0.1:12345`

#### Scenario: User changes the port
- **WHEN** the user sets the port to a free port in range, such as `23456`, and LAN access is on
- **THEN** the server rebinds to that port, later clients use the new URL, and a restart still uses that port

#### Scenario: Out-of-range port is rejected
- **WHEN** the user enters `123456` or `0`
- **THEN** the application rejects the value, explains that the port must be between 1 and 65535, and keeps the previous listen port

#### Scenario: Occupied port is reported
- **WHEN** the chosen port is already bound by another process
- **THEN** the application tells the user the port is in use and does not pretend the LAN server is reachable

#### Scenario: Web cannot change the port
- **WHEN** a web or API client tries to change the listen port
- **THEN** the request is ignored or rejected and the server stays on the current port

### Requirement: Web app has the same three surfaces
WHEN the LAN server is on and the client is unlocked, opening the LAN address SHALL load a web application with 录制, 我的视频, and 设置. The web 设置 page MUST NOT turn the LAN server off, change the listen port, or return the real access key.

#### Scenario: Address opens the app shell
- **WHEN** LAN access is on and the client opens the LAN address with a valid unlock
- **THEN** the page can switch among 录制, 我的视频, and 设置

#### Scenario: Disable LAN stays on the desktop
- **WHEN** the user opens web settings
- **THEN** there is no LAN switch, and saving other settings does not turn the LAN server off

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
The control API SHALL list selectable displays, windows, cameras, and microphones with a stable id and visible label. Display and window items SHALL include a preview image when a snapshot can be taken; snapshot failure MUST NOT omit the item. Every item MUST carry a `previewMissing` flag that is true exactly when it has no preview image. The target list MUST NOT include games.

#### Scenario: Window list carries title and picture
- **WHEN** the client asks for window targets
- **THEN** each item includes an id, a title, and either a preview image or an explicit missing-preview mark

#### Scenario: Minimized window is marked
- **WHEN** a listed window is minimized and no snapshot can be taken
- **THEN** that item has no preview image and `previewMissing` is true

#### Scenario: Game targets are absent
- **WHEN** the client asks for selectable targets
- **THEN** the response contains no game entries

### Requirement: Settings and library through the API
The control API SHALL read and partially update the same settings store as the desktop, and SHALL list, preview, rename, and delete library items. Delete MUST require an explicit confirm flag. Light-edit jobs already offered on the desktop (compress, trim, repair) SHALL be available as jobs.

#### Scenario: Patch settings
- **WHEN** the client updates the save folder or theme through the API
- **THEN** the new value is persisted and a later desktop launch or settings read shows that value

#### Scenario: Delete requires confirm
- **WHEN** the client requests delete without the confirm flag
- **THEN** the file remains and the response explains that confirmation is required

### Requirement: Media stays on the host PC
The web client SHALL be a remote panel only. Capture, encoding, and file writes MUST run on the Windows PC that hosts Luma, and the application MUST NOT upload recordings to a cloud service as part of LAN control.

#### Scenario: Phone controls a local file
- **WHEN** a phone on the LAN starts and stops a recording
- **THEN** the finished file appears in the host PC save folder and is listed by both the desktop library and the web library

### Requirement: Web screenshot still requires the same unlock
Requesting a host still MUST use the same unlock rule as other control routes. WHEN an access key is stored and the client has no valid unlock, that call MUST NOT return screen pixels and MUST NOT write a PNG. WHEN the key is empty and LAN access is on, the client MUST be able to request the still. Save and copy MUST NOT be control routes.

#### Scenario: Locked client gets no pixels
- **WHEN** an access key is stored and the client has no valid unlock
- **THEN** a screenshot still request does not return a screen image and does not write a PNG

#### Scenario: Empty key allows a still
- **WHEN** LAN access is on, the access key is empty, and the client requests a screenshot still
- **THEN** the response includes the host still at the host's pixel dimensions

### Requirement: Save and copy do not call the host
After the still has been delivered, saving and copying MUST be completed in the browser. The host MUST NOT expose a route that writes a screenshot PNG or returns a cropped screenshot. Those actions MUST NOT change the host clipboard or the host save folder.

#### Scenario: Browser save leaves the host folder
- **WHEN** the browser downloads a finished selection
- **THEN** the host save folder has no new PNG and the host clipboard image is unchanged

#### Scenario: No crop route
- **WHEN** a client calls the control API to save or copy a selection
- **THEN** no screenshot route writes a PNG or returns cropped image bytes

### Requirement: Screenshot still does not change the recording session
Requesting a screenshot still MUST NOT start, pause, resume, or stop the recording session controlled by the same API.

#### Scenario: Still while recording
- **WHEN** a recording session is active and the client requests a still
- **THEN** session status stays recording on the same output file

### Requirement: Partial network binding
WHEN LAN access is enabled, the server SHALL always listen on the loopback address and SHALL try each local network address on its own. Addresses that bind SHALL stay reachable even when other addresses fail. WHEN some network addresses fail, the desktop MUST tell the user which addresses other devices cannot reach. WHEN every network address fails, the desktop MUST tell the user that only this PC can connect. WHEN the loopback address cannot bind, the server MUST NOT start and MUST report that the port is taken or cannot be bound.

#### Scenario: One adapter fails
- **WHEN** LAN access is turned on and one network address is already taken by another program
- **THEN** the server listens on loopback and on the remaining network addresses, and the desktop names the address that was skipped

#### Scenario: No network address binds
- **WHEN** LAN access is turned on and no network address can be bound
- **THEN** the server listens on loopback only and the desktop says that only this PC can connect

#### Scenario: Loopback taken
- **WHEN** another program already listens on the loopback address and port
- **THEN** the server does not start and the desktop reports that the port is taken

### Requirement: Web assets revalidate instead of downloading again
The server SHALL send an ETag with the web page, scripts, styles, the API documentation script and library posters, and SHALL answer 304 without a body when the request's ETag still matches. A poster that is regenerated SHALL get a new ETag. The web app SHALL pause session status polling while its tab is hidden and SHALL refresh the status when the tab becomes visible again.

#### Scenario: Reopening the web app
- **WHEN** a browser that already loaded the web app opens it again and nothing changed
- **THEN** the server answers the script and style requests with 304

#### Scenario: Hidden tab
- **WHEN** the web app's tab is hidden
- **THEN** the web app stops requesting session status until the tab is visible again

### Requirement: Web settings include audio volume and noise suppression
The web settings page SHALL offer computer sound volume and microphone volume from 0 to 200 percent and a microphone noise-suppression switch, saved through `PATCH /api/v1/settings` as `audio.systemVolume`, `audio.microphoneVolume` and `audio.noiseSuppression`. A change SHALL apply to a recording in progress.

#### Scenario: Lower the microphone from a phone
- **WHEN** the user drags the microphone volume slider on the web page during a recording
- **THEN** the desktop saves the value and the recording's microphone volume changes

#### Scenario: Microphone off
- **WHEN** the microphone switch is off on the web page
- **THEN** the microphone volume slider and noise-suppression switch are disabled
