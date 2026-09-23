## Purpose

集中管理本机保存目录里的成片，支持预览、整理，以及不依赖云服务的本地剪切、合并、压缩、修复、字幕和配乐。

## ADDED Requirements

### Requirement: Library lists recordings
The application SHALL show a 我的视频 library of recordings from the current save folder, including newly finished sessions without requiring a manual refresh. Each row SHALL show a thumbnail when available, name, size, duration, and date.

#### Scenario: New file appears
- **WHEN** a recording session stops successfully
- **THEN** the new file is visible in the library and can be previewed

#### Scenario: Empty library
- **WHEN** the save folder has no recordings
- **THEN** the library shows an empty state that can return the user to 录制 or open the folder

### Requirement: Preview and file actions
The application SHALL let the user preview a selected item, rename it, open its containing folder, delete it after confirmation, and change the save folder. Later recordings MUST be written to the new folder.

#### Scenario: Preview playback
- **WHEN** the user opens a playable library item
- **THEN** the application plays the file without leaving the application

#### Scenario: Delete confirmation
- **WHEN** the user deletes a library item and confirms
- **THEN** the file is removed from disk and disappears from the list

#### Scenario: Change save folder
- **WHEN** the user changes the save folder
- **THEN** later recordings are written to the new folder and the library lists that folder's items

### Requirement: Repair interrupted files
The application SHALL provide a repair action for recordings that were interrupted, and MUST leave playable files unchanged.

#### Scenario: Repair damaged recording
- **WHEN** the user selects an interrupted recording and runs repair
- **THEN** the application produces a playable video from the recoverable data or explains that repair failed

### Requirement: Compress recordings
The application SHALL let the user compress a selected library video into a smaller file without replacing the original unless the user opts to replace it.

#### Scenario: Compressed copy
- **WHEN** the user compresses a library video and does not choose replace
- **THEN** a smaller video file is added to the library and the original remains

### Requirement: Trim merge subtitles and music
The application SHALL let the user trim one video, merge two or more videos in order, burn or mux subtitles, and mix background music. These exports MUST keep the source recording intact unless the user explicitly chooses to replace it.

#### Scenario: Export trimmed range
- **WHEN** the user sets a start time before an end time and confirms trim
- **THEN** the exported video contains only that time range and the original library item remains playable

#### Scenario: Concatenate two files
- **WHEN** the user merges video A then video B
- **THEN** the exported file plays A followed by B

#### Scenario: Invalid trim range
- **WHEN** the user sets an end time earlier than or equal to the start time
- **THEN** the application MUST reject the export and explain the invalid range
