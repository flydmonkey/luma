## Why

Tutorial and demo recordings need control over the mouse pointer: some want it hidden, others want viewers to see every click. Presenters also want to circle or underline something on screen while recording. All three must stay optional so ordinary recordings are unchanged.

## What Changes

- New `screenAids` settings: `showCursor` (default on), `highlightClicks` (default off), `annotation` (default off).
- `showCursor` controls whether display, region and window capture include the pointer.
- `highlightClicks` shows a fading ring at each left (yellow) or right (blue) click while recording the screen or a region.
- `annotation` adds a pen and a clear button to the recording bar while recording the screen or a region. With the pen on, dragging draws red lines on the recorded monitor; Esc or a right-click puts the pen away. Drawings stay until cleared and disappear when the recording ends.
- Highlights and drawings are topmost layered windows that are not excluded from capture, so display and region recordings include them. Window recordings capture only the target window, so these two options don't apply there.
- The desktop and web settings pages offer all three switches; the LAN settings API accepts `screenAids`.

## Capabilities

### New Capabilities

### Modified Capabilities
- `screen-recording`: pointer, click highlight and annotation behavior during recording.
- `desktop-app`: settings section and recording bar pen.
- `lan-control`: `screenAids` in web settings and the settings API.

## Impact

- `native/Luma.ObsHost`: `showCursor` start field.
- `src/Luma.Core`: `ScreenAidSettings`, `RecordingRequest.ShowCursor`, LAN settings allow-list and copy.
- `src/Luma.App`: `LayeredSurface`, `ScreenInkOverlay`, `ClickHighlighter`, recording bar, settings, localized text.
- `src/Luma.Core/Lan/Web/app.js`, `docs/openapi/openapi.json`.
