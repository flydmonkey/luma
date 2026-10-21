## ADDED Requirements

### Requirement: Pen style and undo
Screen annotation SHALL draw with the saved pen color (one of six) and width (2, 4 or 8 device-independent pixels); other saved values MUST fall back to red and 4. Undo SHALL remove the most recent stroke from the screen and from what is recorded afterwards. While the pen is on, Ctrl+Z SHALL undo.

#### Scenario: Undo a stroke
- **WHEN** the user draws two strokes and presses Ctrl+Z
- **THEN** only the first stroke stays on screen and in the recording

#### Scenario: Invalid saved style
- **WHEN** the settings file has an unknown pen color or width
- **THEN** the pen draws red at width 4
