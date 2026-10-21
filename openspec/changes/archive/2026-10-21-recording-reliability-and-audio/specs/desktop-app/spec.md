## ADDED Requirements

### Requirement: Audio volume and noise suppression settings
The sound settings SHALL offer system sound volume and microphone volume from 0 to 200 percent and a microphone noise-suppression switch that is on by default. Volume changes SHALL apply to a recording in progress.

#### Scenario: Turn the microphone down while recording
- **WHEN** the user moves the microphone volume slider during a recording
- **THEN** the setting is saved and the recording's microphone volume changes right away

#### Scenario: Microphone off
- **WHEN** the microphone switch is off
- **THEN** the microphone volume slider and noise-suppression switch are disabled

### Requirement: Recording bar level meters
The floating recording bar SHALL show a level meter for system sound and one for the microphone when either is recorded.

#### Scenario: Speaking into the microphone
- **WHEN** the user speaks while recording with the microphone on
- **THEN** the lower meter on the recording bar moves with the voice
