## Why

The library froze while reading durations, missed files after the save folder changed, and jumped to the top on every refresh. Settings sliders wrote the file, re-registered hotkeys and ran `schtasks` on every step. Common errors and recording warnings were always Simplified Chinese, and start-time warnings such as an unavailable microphone were never shown. The web app re-downloaded its scripts and posters on every load and kept polling while hidden.

## What Changes

- Library durations are read in the background; files that can't be read are not retried until they change.
- The library watches the current save folder, coalesces bursts of file changes into one refresh, and keeps its scroll position.
- Settings changes take effect at once but are written to disk after a short pause; hotkeys and the logon task are reapplied only when they change, including changes made from the web app or by restoring defaults.
- Common errors and recording warnings follow the UI language; warnings raised at start are shown at start.
- Web scripts, styles and posters carry an ETag and answer 304 when unchanged; the web app pauses status polling while its tab is hidden.

## Capabilities

### New Capabilities

### Modified Capabilities

- `recording-library`: background durations, folder watching, refresh behavior.
- `desktop-app`: settings persistence timing and localized messages.
- `lan-control`: asset revalidation and hidden-tab polling.

## Impact

`src/Luma.App` (library, settings, start/stop notices), `src/Luma.Core` (`CoreText`, `LanServer`, `LanWebAssets`, session rules), `src/Luma.Obs` (`HostText`, `ObsHostClient`), `src/Luma.Media/MediaProbe.cs`, the native host's encoder fallback warning, and `app.js`.
