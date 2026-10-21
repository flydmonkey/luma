## ADDED Requirements

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
