## MODIFIED Requirements

### Requirement: Hardware encoding with software fallback
WHEN hardware encoding is enabled and a compatible GPU encoder (NVIDIA, AMD, or Intel) is available through the OBS engine, the session SHALL use that encoder. WHEN hardware encoding is enabled but no compatible GPU encoder is available, the application MUST fall back to software encoding and inform the user as soon as recording starts: with a banner when the main window is visible, or with a tray notification when the main window has left the picture. A silent start from the LAN control API MUST NOT show either. The notice SHALL be repeated when the recording stops and the main window shows the result.

#### Scenario: Hardware encoder used
- **WHEN** hardware encoding is enabled and a compatible GPU encoder exists
- **THEN** the recording session uses that encoder, remains usable while the user interacts with other desktop apps, and the session state names the actual encoder

#### Scenario: Software fallback
- **WHEN** hardware encoding is enabled but no compatible GPU encoder is available
- **THEN** the application MUST fall back to software encoding and inform the user that hardware encoding is unavailable

#### Scenario: Fallback notice while the window is hidden
- **WHEN** a display recording starts with software fallback and the main window has left the picture
- **THEN** a tray notification says hardware encoding is unavailable, and the banner after stop repeats it
