## ADDED Requirements

### Requirement: Effective frame rate counts missed frames
The native host SHALL report `effectiveFps` as the configured rate multiplied by written frames over written plus missed frames, where missed frames are render-lag frames, encoder-skipped frames and output-dropped frames since the recording started. `skippedFrames` SHALL report the missed count. Encoder start-up latency MUST NOT lower the reported rate.

#### Scenario: Short clip without drops
- **WHEN** a 30 fps recording runs for 1.5 seconds and no frame is lagged, skipped or dropped
- **THEN** status reports `effectiveFps` 30 and `skippedFrames` 0

#### Scenario: Overloaded encoder
- **WHEN** the encoder skips one frame in ten
- **THEN** status reports `effectiveFps` near 27 and counts the skipped frames
