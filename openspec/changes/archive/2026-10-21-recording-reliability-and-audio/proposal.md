## Why

A recording could die halfway (disk full, encoder error) while Luma kept showing a running timer, and nothing warned before the disk ran out. Fixed-bitrate encoding wasted space on still desktops and blurred motion, and the `zerolatency` tune cost quality for no benefit in a file recording. System sound and microphone were mixed into one track, so neither could be fixed in editing, and there was no way to set their volume, see their level, or reduce microphone noise.

## What Changes

- The native host notices when the output stops by itself, reports the reason (`disk`, `encoder`, `other`), and the app finishes the recording and tells the user.
- Before recording, Luma refuses to start with less than 500 MB free and warns when fewer than ten minutes fit. While recording it checks every ten seconds, warns once below ten minutes, and stops gracefully below 300 MB.
- Video uses constant-quality rate control (CRF for x264, CQP for hardware encoders), with a quality value per level. The `zerolatency` tune is removed.
- When both system sound and microphone are recorded, the file carries three audio tracks: the mix (played by default), system sound, and microphone. Post-processing keeps every track.
- Settings gain system and microphone volume (0–200%) and microphone noise suppression (on by default). Volume changes apply live. The recording bar shows a level meter for each source.

## Capabilities

### New Capabilities

### Modified Capabilities
- `libobs-backend`: output-loss detection, constant-quality rate control, separate audio tracks, volume and noise suppression, level reporting.
- `screen-recording`: disk-space checks and automatic finish when the recording fails.
- `desktop-app`: audio volume and noise-suppression settings, recording bar level meter.

## Impact

- `native/Luma.ObsHost`: `obs_session.cpp/.h`, `main.cpp` (`volume` op).
- `src/Luma.Core`: `DiskSpaceRules`, `QualitySettings.QualityTarget`, `AudioSettings`, `RecordingRequest`, `EngineStatus`, localized text.
- `src/Luma.Obs`: start payload, `SetVolumeAsync`, status fields, ffmpeg stream mapping.
- `src/Luma.Media`: ffmpeg jobs map all video and audio streams.
- `src/Luma.App`: settings cards, recording bar meters, disk and output-loss handling, localized text.
