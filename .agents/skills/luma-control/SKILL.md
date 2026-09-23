---
name: luma-control
description: Use when the user asks to start, stop, pause, or remotely control Luma, change its LAN settings, list or delete Library recordings, capture a LAN still or OCR it, call /api/v1 or OpenAPI, or mentions X-Record-Key, port 12345, or LAN access.
---

# Luma Control

Control a running Luma instance only through the shipped LAN API. Do not invent routes from older builds, named pipes, or UI Automation.

## When to Use

- User wants Luma to start, pause, stop, change the settings the API accepts, list or delete finished recordings, or run compress, trim, or repair
- User wants a host still or OCR of a still the LAN page already uses
- User mentions the LAN web app, OpenAPI, `/api/v1`, port 12345, or LAN access

**Do not use** to redesign the recorder, add HTTP routes, or drive the WinUI window when the server is down.

## Steps

1. **Probe.** `GET {base}/api/v1`. Default `base` is `http://127.0.0.1:12345`. Use a URL the user pasted, or the address shown in desktop settings, if different. `data` is `{name:"luma", control:true}`.
2. **LAN off.** Connection refused or timeout: tell the user to enable LAN access. Stop. Do not click the desktop, send hotkeys, or claim success.
3. **Unlock.** `401` with `需要访问密钥。`: send `X-Record-Key` or `Authorization: Bearer` from the user, or from `%LOCALAPPDATA%\Luma\settings.json` → `lan.accessKey`. Never print the key.
4. **See state.** `GET {base}/api/v1/session` returns `{session, target}` inside `data`. `session.state` is `idle`, `recording`, `paused`, or `stopping`.
5. **Record.** List the needed targets, `PUT {base}/api/v1/target`, then `POST {base}/api/v1/session/start`. Region and window need a chosen target. `mode` `game` or `3` is rejected. If start fails, report the API error.
6. **Pause / stop.** `POST .../session/pause` or `.../session/stop`. Both return the session snapshot, not `{session, target}`. Stop returns before the file is finished. Poll `GET .../session` until `state` is `idle`. `session.lastSaved.name` is the file name.
7. **Settings.** `PATCH {base}/api/v1/settings` with only the fields asked. `lan`, `lanPort`, `port`, and `lanPlayback` are rejected. A successful patch clears `automation.startAtLogon` and `automation.schedules`. `POST .../settings/reset` only when the user explicitly asked.
8. **Library.** `GET {base}/api/v1/library`. Items are camelCase: `id`, `name`, `length`, `duration`, `date`, `isAudio`. Delete only after the user confirmed, with `?confirm=true`. Compress, trim, and repair return a `jobId`; poll `GET /api/v1/jobs/{jobId}`.
9. **Still.** Only when the user asked for a picture or OCR. `GET /api/v1/screenshot`, or `?windowIndex=` for one window from the windows list. `POST /api/v1/screenshot/ocr` with `{"png":"<base64>"}`. The browser saves and copies. There is no save or copy route.

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
| "Stop returns the filename" | Poll `GET /session` until `idle`. |
| "Library fields are PascalCase" | `id`, `name`, `length`, `duration`, `date`, `isAudio`. |
| "Delete it, they implied it" | No `confirm=true` until they confirm. |
| "Patch the port from here" | `lan`, `lanPort`, `port`, and `lanPlayback` are rejected. Change the port on the desktop. |
| "Show the key so they can check" | Never echo the access key. |

## Red Flags

- A URL that is not listed in `reference.md`
- UI Automation, hotkeys, or clicking Luma used as a control fallback
- `PUT /target` or `POST /session/start` with `mode` `game` or `3`
- `DELETE /library/{id}` without `confirm=true`
- Printing `accessKey` in the reply
- Guessing body fields instead of reading `reference.md` or `docs/openapi/openapi.json`
