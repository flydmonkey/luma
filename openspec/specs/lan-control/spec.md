# lan-control Specification

## Purpose

在同一本机 HTTP 服务上提供与桌面同构的三页网页和 JSON 控制接口，让局域网设备远程操作正在运行的 Luma，而不把媒体上传到云端。

## Requirements

### Requirement: LAN server and optional access key
The application SHALL expose a local HTTP server when LAN access is enabled. WHEN an access key is stored, pages and control routes MUST require a valid unlock. WHEN the key is empty, they MUST be reachable without a key. WHEN the server is off, they MUST NOT accept connections. The factory-default listen port SHALL be `12345`, so the loopback URL is `http://127.0.0.1:12345` until the user changes it.

#### Scenario: Key empty allows access
- **WHEN** LAN access is on and the access key is empty
- **THEN** a client on the same machine can open the web app and call control routes without submitting a key

#### Scenario: Key blocks anonymous control
- **WHEN** an access key is stored and the client has no valid unlock
- **THEN** control routes do not start a recording, change settings, or list library file names

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
The 录制 page and the control API SHALL start, pause, resume, and stop the same recording session as the desktop, using the same target and quality rules. Region mode MUST require a confirmed rectangle. Window and game modes MUST require a listed target id.

#### Scenario: Start after picking a window
- **WHEN** the user selects a window from the labeled preview list and starts recording
- **THEN** the session becomes active and later status shows it as recording

#### Scenario: API start without a required target fails
- **WHEN** the mode is region, window, or game and no target is confirmed
- **THEN** the start call fails with an explanation and no output file is created

### Requirement: Targets include labels and previews
The control API SHALL list selectable displays, windows, games, cameras, and microphones with a stable id and visible label. Display, window, and game items SHALL include a preview image when a snapshot can be taken; snapshot failure MUST NOT omit the item.

#### Scenario: Window list carries title and picture
- **WHEN** the client asks for window targets
- **THEN** each item includes an id, a title, and either a preview image or an explicit missing-preview mark

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
