## Why

`effectiveFps` divided written frames by wall-clock time since the output started, so encoder start-up latency read as lost frames: a 1.5-second clip at 30 fps reported about 27. The web settings page also lacked the new volume and noise-suppression options the desktop app has.

## What Changes

- `effectiveFps` is the configured rate scaled by written frames over written plus missed frames, where missed frames are render lag, encoder skips and output drops since the recording started. `skippedFrames` reports the same missed count.
- The web settings page gains computer sound volume, microphone volume and microphone noise suppression. A change from the web applies to a recording in progress.
- The OpenAPI `AudioSettings` schema lists `systemVolume`, `microphoneVolume` and `noiseSuppression`.

## Capabilities

### New Capabilities

### Modified Capabilities
- `libobs-backend`: how the effective frame rate and skipped frames are measured.
- `lan-control`: audio volume and noise suppression in web settings.

## Impact

- `native/Luma.ObsHost/obs_session.cpp`
- `src/Luma.Core/Lan/Web/app.js`, `docs/openapi/openapi.json`
- `src/Luma.App/MainWindow.xaml.cs` (apply volume after a web settings change)
