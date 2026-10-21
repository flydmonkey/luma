## MODIFIED Requirements

### Requirement: Cancel without a file
The user MUST be able to leave the overlay without a browser download, without a new host file, and without changing the host clipboard or the browser clipboard image. Cancel MUST NOT send another request to the host. Esc SHALL close one layer at a time: an open text input first, then an open recognized-text panel, then the overlay.

#### Scenario: Escape cancels
- **WHEN** the overlay is open with no text input or recognized-text panel showing and the user presses Esc
- **THEN** the overlay closes, the browser does not download a file, no new host PNG is written, and neither clipboard image changes

#### Scenario: Escape closes the inner layer first
- **WHEN** the recognized-text panel is showing and the user presses Esc
- **THEN** only that panel closes and the overlay stays open with its selection

#### Scenario: Empty drag cancels
- **WHEN** the user releases a rectangular drag that does not form a region
- **THEN** the overlay closes, save and copy are not shown, and no file is downloaded or written
