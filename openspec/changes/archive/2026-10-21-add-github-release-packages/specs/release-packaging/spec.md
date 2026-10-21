## Purpose

让 GitHub Actions 在版本标签上产出可直接分发的 Windows x64 便携压缩包和 exe 安装包，并挂到该标签的草稿 GitHub Release。

## ADDED Requirements

### Requirement: Portable zip package
The release workflow SHALL produce a zip archive of the x64 Release build. The archive MUST contain `Luma.exe`, the libobs runtime required to record, and the GPL-2.0 license text. On Windows 10 version 1809 or later x64, extracting the archive and starting `Luma.exe` MUST NOT require a separate .NET SDK or .NET runtime install.

#### Scenario: Zip is runnable without the .NET runtime
- **WHEN** a successful release uploads the zip and a supported Windows machine extracts it
- **THEN** `Luma.exe` is present beside the license text and the libobs runtime, and it starts without a preinstalled .NET runtime

### Requirement: Exe installer
The release workflow SHALL produce a Windows exe installer built from the same publish contents as the zip. Running the installer MUST install those files for the current user without administrator rights, MUST create a Start menu shortcut named Luma, and MUST register an uninstaller that removes the installed files and that shortcut.

#### Scenario: Install and uninstall for the current user
- **WHEN** the user runs the exe installer and completes it without elevation
- **THEN** Luma is installed for that user, a Start menu shortcut named Luma exists, and the uninstaller removes the installed files and the shortcut

### Requirement: Tag matches the application version
The workflow SHALL run only when a tag matching `v*` is pushed. It MUST compare the tag name without the leading `v` to the application version stored in the repository. WHEN they differ, the workflow MUST fail before creating a GitHub Release or uploading either package.

#### Scenario: Matching tag continues
- **WHEN** the application version is `1.2.3` and the tag `v1.2.3` is pushed
- **THEN** the workflow continues past the version check

#### Scenario: Mismatched tag stops
- **WHEN** the application version is `1.2.3` and the tag `v1.2.4` is pushed
- **THEN** the workflow fails and does not create a Release or upload a zip or an exe installer

### Requirement: Draft release with both packages
A successful run MUST create a draft GitHub Release for that tag. The release title MUST be `Luma` followed by the tag name, the notes MUST be `Automatic release`, and the release MUST NOT be marked as a prerelease. The exe installer MUST be attached when the draft is created. The zip MUST be uploaded to that same release afterward. Both asset names MUST include the version. The workflow MUST NOT run on pull requests.

#### Scenario: Tag publishes a draft with both assets
- **WHEN** the tag `v1.2.3` matches the application version and the workflow succeeds
- **THEN** the GitHub Release for `v1.2.3` is a draft titled `Luma v1.2.3`, its notes are `Automatic release`, it is not a prerelease, and it has both the zip and the exe installer with `1.2.3` in each name

#### Scenario: Zip arrives after the draft exists
- **WHEN** the installer has been attached to the draft release and the zip is produced
- **THEN** the zip is added to that same release

### Requirement: Tests gate the packages
The workflow MUST run the solution tests before it creates a zip, an installer, or a GitHub Release. WHEN tests fail, the workflow MUST fail and MUST NOT upload either package or publish a Release.

#### Scenario: Failed tests produce no package
- **WHEN** the solution tests fail
- **THEN** the workflow fails and uploads neither the zip nor the exe installer, and does not publish a Release

### Requirement: FFmpeg stays optional
The zip and the installer MUST NOT include `ffmpeg.exe`. Library tools that need FFmpeg keep their existing behavior of using a copy beside the app or on `PATH`.

#### Scenario: Package does not ship FFmpeg
- **WHEN** a successful run produces a zip or an installer
- **THEN** the packaged files do not contain `ffmpeg.exe`
