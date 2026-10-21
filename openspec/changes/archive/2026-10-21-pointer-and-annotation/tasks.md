## 1. Pointer

- [x] 1.1 Add `ScreenAidSettings` and pass `showCursor` to the host's capture sources

## 2. Overlays

- [x] 2.1 Layered, capture-visible topmost surface
- [x] 2.2 Click ring from a low-level mouse hook
- [x] 2.3 Full-monitor ink layer that is click-through unless the pen is on; Esc and right-click exit
- [x] 2.4 Create overlays when recording the screen or a region starts; remove them when it ends

## 3. UI

- [x] 3.1 Pen and clear buttons on the recording bar
- [x] 3.2 Desktop and web settings switches in five languages; LAN API and OpenAPI

## 4. Verification

- [x] 4.1 Host test: ink drawn on the overlay appears in a display recording
- [x] 4.2 LAN test: settings patch updates `screenAids`
