## ADDED Requirements

### Requirement: Pointer and annotation settings
Settings SHALL have a pointer and annotation section with switches for recording the mouse pointer (on by default), highlighting clicks (off by default) and screen annotation (off by default).

#### Scenario: Defaults
- **WHEN** Luma runs with new settings
- **THEN** the pointer is recorded, and click highlight and annotation are off

### Requirement: Recording bar pen
When annotation is on and the screen or a region is being recorded, the recording bar SHALL show a pen toggle and a clear button. The pen toggle SHALL reflect when the pen was put away with Esc or a right-click.

#### Scenario: Esc puts the pen away
- **WHEN** the pen is on and the user presses Esc
- **THEN** the pen toggle on the recording bar turns off
