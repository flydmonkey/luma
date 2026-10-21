## ADDED Requirements

### Requirement: Output loss is reported
The native host SHALL report `outputLost` with a reason of `disk`, `encoder` or `other` when the recording output stops without a stop request.

#### Scenario: Disk fills up
- **WHEN** the output stops with a no-space error while recording
- **THEN** status reports `outputLost: true` and `lostReason: "disk"`

#### Scenario: Requested stop
- **WHEN** the app asks the host to stop
- **THEN** status does not report `outputLost`

### Requirement: Constant-quality rate control
The native host SHALL encode video with CRF (x264) or CQP (hardware encoders) using the quality value from the start request, and SHALL NOT use the `zerolatency` tune.

#### Scenario: Hardware encoder
- **WHEN** a recording starts on a hardware encoder with quality 23
- **THEN** the encoder uses rate control `CQP` with `cqp` 23

### Requirement: Separate audio tracks
When both system sound and microphone are recorded with video, the file SHALL contain three audio tracks in this order: the mix, system sound only, and microphone only.

#### Scenario: Both sources on
- **WHEN** a display recording captures system sound and microphone
- **THEN** the file has three audio streams and players play the mix by default

#### Scenario: Post-processing
- **WHEN** a recording with three audio tracks is repaired, trimmed, compressed, merged or subtitled
- **THEN** the result keeps all three audio tracks

### Requirement: Audio volume, levels and noise suppression
The native host SHALL apply system and microphone volume (0–2×) at start and on the `volume` command, SHALL add noise suppression to the microphone when requested, and SHALL report a 0–1 peak level for each source.

#### Scenario: Live volume change
- **WHEN** the app sends `volume` while recording
- **THEN** the new volume applies without restarting the recording

#### Scenario: Noise suppression unavailable
- **WHEN** the noise suppression filter can't be created
- **THEN** the recording continues and the host warns that microphone noise suppression isn't available
