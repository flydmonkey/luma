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

1. **Probe.** `GET {base}/api/v1/session`. Default `base` is `http://127.0.0.1:12345`. Use a URL the user pasted, or the address shown in desktop settings, if different.
2. **LAN off.** Connection refused or timeout: tell the user to enable LAN access. Stop. Do not click the desktop, send hotkeys, or claim success.
3. **Unlock.** `401` with `{"error":"unauthorized"}`: send `X-Record-Key` or `Authorization: Bearer` from the user, or from `%LOCALAPPDATA%\Luma\settings.json` → `lan.accessKey`. Never print the key. `/` is reachable without the key; `/api/v1/*` is not.
4. **See state.** `GET {base}/api/v1/session` returns `{ok, phase}`. `phase`: 0 idle, 1 countdown, 2 recording, 3 paused, 4 processing.
5. **Record.** Start uses the target already chosen on the desktop. `POST {base}/api/v1/session/start` with `{}`. Region and window must already be selected there; otherwise the error is `请先选择录制目标。` Game mode is closed: a body `{"mode":"game"}` or a desktop game selection returns `游戏录制已关闭。` Listing targets does not select one.
6. **Pause / stop.** `POST .../session/pause` or `.../session/stop`. Stop includes `path` when a file was produced. Confirm with `GET .../session`.
7. **Settings.** `PATCH {base}/api/v1/settings` with only `theme`, `uiLanguage`, or `saveFolder`. `lanPort` and `port` return 400. The API cannot turn LAN off or set the access key.
8. **Library.** `GET {base}/api/v1/library`. Delete only after the user confirmed, with `DELETE .../library/{id}?confirm=true`. There is no rename, trim, compress, or job route.

Before any JSON body, read `reference.md` in this folder. Schemas live in `docs/openapi/openapi.json`. Do not guess field names.

## Example

User: "Start the recording I already set up"

```powershell
$base = "http://127.0.0.1:12345"
Invoke-RestMethod "$base/api/v1/session"
Invoke-RestMethod -Method Post "$base/api/v1/session/start" -ContentType "application/json" -Body "{}"
Invoke-RestMethod "$base/api/v1/session"
```

## Common Mistakes

| Excuse | Reality |
|--------|---------|
| "I'll PUT /api/v1/target" | This build has no target-select route. Start uses the desktop selection. |
| "I'll add a /record route" | Only the shipped `/api/v1` paths. |
| "LAN is off, I'll click Start" | Fail closed. Ask to enable LAN access. |
| "List windows, then start that window" | The list is informational. The desktop must already have that window selected. |
| "Delete it, they implied it" | No `confirm=true` until they confirm. |
| "Patch the port from here" | `lanPort` / `port` are rejected. Change the port on the desktop. |
| "Show the key so they can check" | Never echo the access key. |

## Red Flags

- A URL that is not listed in `reference.md`, including `/api/v1/target`, `/api/v1/settings/reset`, and `/api/v1/jobs/{id}`
- UI Automation, hotkeys, or clicking Luma used as a control fallback
- `POST /session/start` with `mode` `game` or `3`
- `DELETE /library/{id}` without `confirm=true`
- Printing `accessKey` in the reply
- Guessing body fields instead of reading `reference.md` or `docs/openapi/openapi.json`
