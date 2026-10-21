## ADDED Requirements

### Requirement: Mouse pointer is optional
Recordings SHALL include the mouse pointer unless `screenAids.showCursor` is off.

#### Scenario: Pointer hidden
- **WHEN** the user turns off recording the mouse pointer and records the screen
- **THEN** the recording does not show the pointer

### Requirement: Click highlight
When `screenAids.highlightClicks` is on and the screen or a region is being recorded, Luma SHALL show a fading ring at each mouse click, and the recording SHALL include it.

#### Scenario: Left click while recording the screen
- **WHEN** the user left-clicks during a display recording with click highlight on
- **THEN** a yellow ring expands and fades at the click point in the recording

#### Scenario: Window recording
- **WHEN** a window is being recorded
- **THEN** no click ring is shown

### Requirement: Screen annotation
When `screenAids.annotation` is on and the screen or a region is being recorded, Luma SHALL let the user draw on the recorded monitor, and the recording SHALL include the drawing. Outside pen mode the drawing layer MUST pass all mouse input through.

#### Scenario: Draw and put the pen away
- **WHEN** the user turns on the pen, drags across the screen, then presses Esc
- **THEN** a red line stays on screen and in the recording, and clicks reach the apps underneath again

#### Scenario: Recording ends
- **WHEN** the recording stops
- **THEN** the drawings are removed from the screen
