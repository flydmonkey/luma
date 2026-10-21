## MODIFIED Requirements

### Requirement: Trim merge subtitles and music
The application SHALL let the user trim one video, merge two or more videos in recording order (earliest first, regardless of selection order), burn or mux subtitles, and mix background music. These exports MUST keep the source recording intact unless the user explicitly chooses to replace it.

#### Scenario: Export trimmed range
- **WHEN** the user sets a start time before an end time and confirms trim
- **THEN** the exported video contains only that time range and the original library item remains playable

#### Scenario: Concatenate two files
- **WHEN** video A was recorded before video B and the user selects B then A and merges them
- **THEN** the exported file plays A followed by B

#### Scenario: Invalid trim range
- **WHEN** the user sets an end time earlier than or equal to the start time
- **THEN** the application MUST reject the export and explain the invalid range
