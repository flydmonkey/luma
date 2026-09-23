---
name: luma-control
description: Use when the user asks to start, stop, pause, or remotely control Luma, change its LAN settings, list or delete Library recordings, call /api/v1 or OpenAPI, or mentions X-Record-Key, port 12345, or LAN access.
---

# Luma Control

Control a running Luma instance only through the shipped LAN API. Do not invent routes from older builds, named pipes, or UI Automation.

## When to Use

- User wants Luma to start, pause, stop, change the few settings the API accepts, or list or delete finished recordings
- User mentions the LAN web app, OpenAPI, `/api/v1`, port 12345, or LAN access

**Do not use** to redesign the recorder, add HTTP routes, or drive the WinUI window when the server is down.

## Steps

1. **Probe.** `GET {base}/api/v1`. Default `base` is `http://127.0.0.1:12345`. Use a URL the user pasted, or the address shown in desktop settings, if different.
2. **LAN off.** Connection refused or timeout: tell the user to enable LAN access. Stop. Do not click the desktop, send hotkeys, or claim success.
3. **Unlock.** `401`: send `X-Record-Key` or `Authorization: Bearer` from the user, or from `%LOCALAPPDATA%\Luma\settings.json` → `lan.accessKey`. Never print the key.
4. **See state.** `GET {base}/api/v1/session` returns `{session, target}` inside `data`. `session.state` is `idle`, `recording`, or `paused`.
5. **Record.** List the needed targets, `PUT {base}/api/v1/target`, then `POST {base}/api/v1/session/start`. Region and window need a chosen target. `mode` `game` is rejected. If start fails, report the API error.
6. **Pause / stop.** `POST .../session/pause` or `.../session/stop`. Confirm with `GET .../session`.
7. **Settings.** `PATCH {base}/api/v1/settings` with only the fields asked. `lan`, `lanPort`, and `port` are rejected. `POST .../settings/reset` only when the user explicitly asked.
8. **Library.** `GET {base}/api/v1/library`. Delete only after the user confirmed, with `?confirm=true`. Compress, trim, and repair return a `jobId`; poll `GET /api/v1/jobs/{jobId}`.

Before any JSON body, read `reference.md` in this folder. Schemas live in `docs/openapi/openapi.json`. Do not guess field names.

## Example

User: "Start the recording I already set up"

```powershell
$base = "http://127.0.0.1:12345"
Invoke-RestMethod "$base/api/v1"
$windows = Invoke-RestMethod "$base/api/v1/targets/windows"
# pick the item whose title matches Notes
Invoke-RestMethod -Method Put "$base/api/v1/target" -ContentType "application/json" -Body '{"mode":"window","windowId":"<id>"}'
Invoke-RestMethod -Method Post "$base/api/v1/session/start"
Invoke-RestMethod "$base/api/v1/session"
```

## Common Mistakes

| Excuse | Reality |
|--------|---------|
| "I'll add a /record route" | Only the shipped `/api/v1` paths. |
| "LAN is off, I'll click Start" | Fail closed. Ask to enable LAN access. |
| "Start first, pick the window later" | `PUT /target` then start. |
| "Delete it, they implied it" | No `confirm=true` until they confirm. |
| "Patch the port from here" | `lan`, `lanPort`, and `port` are rejected. Change the port on the desktop. |
| "Show the key so they can check" | Never echo the access key. |

## Red Flags

- A URL that is not listed in `reference.md`, including `/api/v1/target`, `/api/v1/settings/reset`, and `/api/v1/jobs/{id}`
- UI Automation, hotkeys, or clicking Luma used as a control fallback
- `POST /session/start` with `mode` `game` or `3`
- `DELETE /library/{id}` without `confirm=true`
- Printing `accessKey` in the reply
- Guessing body fields instead of reading `reference.md` or `docs/openapi/openapi.json`
