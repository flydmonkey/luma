## MODIFIED Requirements

### Requirement: Recording bar pen
When annotation is on and the screen or a region is being recorded, the recording bar SHALL show a pen toggle, a pen style button, an undo button and a clear button. The pen toggle SHALL reflect when the pen was put away with Esc or a right-click. The pen style button SHALL expand the bar with six colors and three widths; picking one SHALL turn the pen on and save the choice for later recordings. The undo button SHALL be disabled when there is nothing to undo.

#### Scenario: Esc puts the pen away
- **WHEN** the pen is on and the user presses Esc
- **THEN** the pen toggle on the recording bar turns off

#### Scenario: Pick a color
- **WHEN** the user opens the pen style row and picks blue
- **THEN** the pen turns on, new strokes are blue, and the next recording starts with blue
