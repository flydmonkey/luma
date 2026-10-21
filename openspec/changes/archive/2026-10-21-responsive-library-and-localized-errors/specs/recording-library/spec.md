## ADDED Requirements

### Requirement: Library stays responsive
The library SHALL list files without waiting for their durations. Durations that are not yet known SHALL be read in the background and filled in as they arrive. A file whose duration cannot be read MUST NOT be read again until its size or modified time changes. The library SHALL watch the current save folder and SHALL switch to the new folder when the save folder changes. Bursts of file changes SHALL produce one refresh, and a refresh SHALL keep the list's scroll position.

#### Scenario: Many files without durations
- **WHEN** the library opens on a folder whose durations are not cached
- **THEN** the list appears at once and durations fill in afterwards

#### Scenario: Save folder changes
- **WHEN** the user picks a new save folder or restores defaults
- **THEN** new files in the new folder appear in the library without a restart

#### Scenario: Refresh while scrolled
- **WHEN** the user has scrolled down and files change in the save folder
- **THEN** the list refreshes once and stays at the same scroll position
