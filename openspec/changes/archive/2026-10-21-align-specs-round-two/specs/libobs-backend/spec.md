## MODIFIED Requirements

### Requirement: Output containers
Video sessions SHALL write H.264 video and AAC audio into the container chosen in settings: `.mp4` by default, or `.mkv`, `.mov`, or `.flv`. Audio-only sessions SHALL write `.m4a` with AAC audio. Frame orientation MUST match the captured screen (top of the screen stays at the top of the file).

#### Scenario: Default video container
- **WHEN** the user finishes a display recording
- **THEN** the library item is an `.mp4` that plays in the built-in previewer

#### Scenario: Chosen container
- **WHEN** the user sets the recording format to `mkv` and finishes a display recording
- **THEN** the library item is an `.mkv` file

#### Scenario: Top of the screen stays at the top
- **WHEN** the user records a display whose taskbar is at the bottom
- **THEN** that chrome appears at the bottom of the output, not the top
