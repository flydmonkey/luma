## ADDED Requirements

### Requirement: Disk space is checked
Luma SHALL check free space in the save folder before and during recording, estimating time left from the video bitrate plus 160 kbps per audio track.

#### Scenario: Too little space to start
- **WHEN** less than 500 MB is free at start
- **THEN** Luma does not start and says how much space is left

#### Scenario: Little time left at start
- **WHEN** fewer than ten minutes fit at start
- **THEN** the recording starts and Luma warns how many minutes fit

#### Scenario: Space runs out while recording
- **WHEN** a check during recording finds less than 300 MB free
- **THEN** Luma stops the recording, keeps the file, and tells the user why

### Requirement: Failed recordings finish on their own
When the host reports a lost output, Luma SHALL finish the recording, keep what was written, and tell the user, through a notification when the window is hidden.

#### Scenario: Output lost while the window is hidden
- **WHEN** the output is lost while Luma is in the tray
- **THEN** Luma stops the session, shows a notification with the reason, and the library lists the file
