## ADDED Requirements

### Requirement: Web assets revalidate instead of downloading again
The server SHALL send an ETag with the web page, scripts, styles, the API documentation script and library posters, and SHALL answer 304 without a body when the request's ETag still matches. A poster that is regenerated SHALL get a new ETag. The web app SHALL pause session status polling while its tab is hidden and SHALL refresh the status when the tab becomes visible again.

#### Scenario: Reopening the web app
- **WHEN** a browser that already loaded the web app opens it again and nothing changed
- **THEN** the server answers the script and style requests with 304

#### Scenario: Hidden tab
- **WHEN** the web app's tab is hidden
- **THEN** the web app stops requesting session status until the tab is visible again
