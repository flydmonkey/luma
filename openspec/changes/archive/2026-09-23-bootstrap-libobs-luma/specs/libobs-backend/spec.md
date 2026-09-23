## Purpose

把实时采集、混音、硬件编码和封装交给开源 OBS 核心引擎，让成片帧率与编码器状态可核对，并禁止再实现一套自研硬编栈。

## ADDED Requirements

### Requirement: Realtime pipeline uses the OBS engine
Every realtime recording session SHALL capture, mix, encode, and mux through the open-source OBS Studio core engine and its shipped Windows plugins. The application MUST NOT implement its own hardware encoder, GPU color-conversion writer, or Media Foundation sink-writer recording path for live sessions.

#### Scenario: Display session produces a playable file
- **WHEN** the user completes a display-mode recording with default quality
- **THEN** a playable `.mp4` exists in the save folder and the session reports an encoder name that comes from the OBS engine

#### Scenario: Custom hardware encoder path is absent
- **WHEN** a reviewer inspects the shipped realtime recording path
- **THEN** live video is not written by a first-party Media Foundation sink writer or a first-party GPU NV12 surface pool

### Requirement: Hardware encoding with software fallback
WHEN hardware encoding is enabled and a compatible GPU encoder (NVIDIA, AMD, or Intel) is available through the OBS engine, the session SHALL use that encoder. WHEN hardware encoding is enabled but no compatible GPU encoder is available, the application MUST fall back to software encoding and inform the user.

#### Scenario: Hardware encoder used
- **WHEN** hardware encoding is enabled and a compatible GPU encoder exists
- **THEN** the recording session uses that encoder, remains usable while the user interacts with other desktop apps, and the session state names the actual encoder

#### Scenario: Software fallback
- **WHEN** hardware encoding is enabled but no compatible GPU encoder is available
- **THEN** the application MUST fall back to software encoding and inform the user that hardware encoding is unavailable

### Requirement: Honest encoder progress
Session state SHALL report actual output width and height, effective frame rate, encoder name, whether hardware encoding is in use, and skipped-frame counts. Encoded duration MUST be derived from finished encoded packets or equivalent OBS output completion, not from the count of raw frames submitted to the engine.

#### Scenario: Status names the real encoder
- **WHEN** a hardware session is active on a machine with a known GPU encoder
- **THEN** session status includes that encoder's identifiable name rather than a generic "hardware" label

#### Scenario: Duration matches the file
- **WHEN** a recording stops successfully
- **THEN** the reported video duration and the playable file duration agree within one second, even if the encoder lagged behind capture

### Requirement: Backpressure drops frames instead of tearing
WHEN the encoder cannot keep up, the engine SHALL drop frames or increment skipped counts and MUST NOT reuse or overwrite surfaces that are still being encoded. The capture clock MUST keep running.

#### Scenario: Encoder lag does not tear frames
- **WHEN** the encoder falls behind during a high-motion 1080p or higher session
- **THEN** the output may have a lower unique frame rate, but frames MUST NOT tear, scramble, or show mixed timestamps from different capture instants

### Requirement: Audio and overlays ride the same engine timeline
System audio, microphone, camera overlay, and watermarks that are enabled for a session SHALL be mixed on the OBS engine timeline with the video or audio-only output.

#### Scenario: Audio stays aligned
- **WHEN** both system audio and microphone are enabled for a video session
- **THEN** the finished file has one audio timeline aligned with the video and does not drift by more than one second over a five-minute recording

### Requirement: Output containers
Video sessions SHALL write `.mp4` with H.264 video and AAC audio. Audio-only sessions SHALL write `.m4a` with AAC audio. Frame orientation MUST match the captured screen (top of the screen stays at the top of the file).

#### Scenario: Default video container
- **WHEN** the user finishes a display recording
- **THEN** the library item is an `.mp4` that plays in the built-in previewer

#### Scenario: Top of the screen stays at the top
- **WHEN** the user records a display whose taskbar is at the bottom
- **THEN** that chrome appears at the bottom of the output, not the top

### Requirement: Post-processing stays outside the live engine
Library repair, compress, trim, merge, subtitle, and soundtrack jobs SHALL NOT start or replace the live OBS recording engine. Those jobs MAY use a bundled media tool, but a live session MUST keep using the OBS engine until stop completes.

#### Scenario: Edit does not hijack a live session
- **WHEN** a recording is active and the user starts a library compress job on an older file
- **THEN** the live session continues and the compress job does not take over the live encoder
