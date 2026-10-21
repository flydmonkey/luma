## ADDED Requirements

### Requirement: Web settings include audio volume and noise suppression
The web settings page SHALL offer computer sound volume and microphone volume from 0 to 200 percent and a microphone noise-suppression switch, saved through `PATCH /api/v1/settings` as `audio.systemVolume`, `audio.microphoneVolume` and `audio.noiseSuppression`. A change SHALL apply to a recording in progress.

#### Scenario: Lower the microphone from a phone
- **WHEN** the user drags the microphone volume slider on the web page during a recording
- **THEN** the desktop saves the value and the recording's microphone volume changes

#### Scenario: Microphone off
- **WHEN** the microphone switch is off on the web page
- **THEN** the microphone volume slider and noise-suppression switch are disabled
