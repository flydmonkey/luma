## MODIFIED Requirements

### Requirement: Webcam and custom watermarks
The application SHALL let the user enable a camera overlay with position and size, and add text, image, and at most one timestamp watermark, each with position and size. Text and image watermarks SHALL also have opacity. The application MUST NOT burn a product brand watermark the user cannot remove.

#### Scenario: Overlay appears in output
- **WHEN** the camera overlay is enabled and a recording completes
- **THEN** the output video contains the live camera image at the configured size and position

#### Scenario: Watermark opacity is applied
- **WHEN** the user adds a text watermark at 50% opacity and a recording completes
- **THEN** the output shows that text semi-transparent over the captured picture

#### Scenario: Clean output by default
- **WHEN** the user records with no custom watermarks configured
- **THEN** the output contains no application brand watermark

### Requirement: Session control and stop preview
The application SHALL let the user start, pause, resume, and stop a single active recording session, MUST display elapsed time while a session is active or paused, and MUST keep the home surface responsive after stop is requested. Elapsed time MUST stay visible when the recording bar is turned off, for example in the tray tooltip. The elapsed clock SHALL freeze at the moment stop is requested. WHEN stop completes with a playable file and the main window is visible or is brought back, the application SHALL show a processing indicator and then open that file in the library preview. WHEN the main window stays hidden for the session, or silent mode is on and the stop came from the LAN control API, the application MUST NOT change pages or show the main window. WHEN a web client stops the recording, the web app SHALL show its own saved notice with the file name and a way to open that file in the web library preview.

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

#### Scenario: Web stop shows the saved file
- **WHEN** a web client stops a recording and the stop produced a playable file
- **THEN** the web app shows the saved file name and can open it in the web library preview
