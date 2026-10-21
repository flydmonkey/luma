## MODIFIED Requirements

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
