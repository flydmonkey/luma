## Why

A single red, fixed-width pen is hard to read on red or busy content, and a slip means clearing everything. Presenters need a few colors, a few widths, and a way to take back the last stroke.

## What Changes

- The recording bar gets a pen style button and an undo button next to the pen and clear buttons.
- The style button expands the bar with six colors (red, yellow, green, blue, white, black) and three widths (thin, medium, thick). Picking one turns the pen on; the choice is saved in `screenAids.inkColor` and `screenAids.inkWidth`.
- Undo removes the last stroke; Ctrl+Z does the same while the pen is on.
- The style row lives inside the recording bar window, which is excluded from capture, so it never appears in recordings.
- The web settings page keeps the saved pen style when it saves; OpenAPI documents the two fields.

## Capabilities

### New Capabilities

### Modified Capabilities
- `desktop-app`: recording bar pen controls.
- `screen-recording`: pen color, width and undo.
- `lan-control`: `screenAids.inkColor` and `screenAids.inkWidth`.

## Impact

- `src/Luma.Core/Settings/AppSettings.cs`: palette, widths and normalization.
- `src/Luma.App/Views/ScreenAids.cs`: stroke history, `SetPen`, `Undo`, Ctrl+Z hotkey.
- `src/Luma.App/Views/RecordingBarWindow.cs`, `MainWindow.xaml.cs`, `Services/UiCopy.cs`.
- `src/Luma.Core/Lan/Web/app.js`, `docs/openapi/openapi.json`.
