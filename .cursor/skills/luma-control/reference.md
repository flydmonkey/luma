# Luma `/api/v1` verbs

Open this file before sending any JSON body. Field schemas: `docs/openapi/openapi.json`. Do not invent keys from older Luma builds.

Default `{base}`: `http://127.0.0.1:12345`. Settings and session bodies use camelCase. Library items use the property names below.

| Method | Path | Body / result |
|--------|------|----------------|
| GET | `/` | LAN shell HTML. No key required. |
| GET | `/api/v1/session` | none → `{ok, phase}` |
| POST | `/api/v1/session/start` | `{}` or `{"mode":"..."}`. Starts the desktop's current target. |
| POST | `/api/v1/session/pause` | `{}`; toggles pause/resume when a session is active |
| POST | `/api/v1/session/stop` | `{}`; saves the file and returns `path` |
| GET | `/api/v1/targets?kind=displays` | default kind |
| GET | `/api/v1/targets?kind=windows` | window id is informational |
| GET | `/api/v1/targets?kind=games` | empty; game capture is closed |
| GET | `/api/v1/targets?kind=cameras` | device id and label |
| GET | `/api/v1/targets?kind=microphones` | `mics` is the same list |
| GET | `/api/v1/settings` | flat settings; `accessKey` is `""` |
| PATCH or POST | `/api/v1/settings` | `theme`, `uiLanguage`, `saveFolder` only |
| GET | `/api/v1/library` | array of library items |
| DELETE | `/api/v1/library/{id}?confirm=true` | `{ok:true}` |

Send `Content-Type: application/json` on PATCH/POST.

## Auth

If `lan.accessKey` is set: `X-Record-Key: <key>` or `Authorization: Bearer <key>`. Empty key: omit both. Never print the key. A missing key on `/api/v1/*` is HTTP 401 `{"error":"unauthorized"}`.

## `GET /session`

```json
{"ok": true, "phase": 0}
```

`phase`: `0` idle, `1` countdown, `2` recording, `3` paused, `4` processing.

`POST /session/start` success: `{"ok":true,"phase":2}`. Failure: `{"ok":false,"error":"..."}` and, when the desktop rejected the take before it began, `"phase":0`.

`POST /session/stop` success: `{"ok":true,"phase":0,"path":"D:\\Videos\\Recordings\\Luma-....mp4"}`.

`mode` in the start body is only a guard. `game` or `3` returns `游戏录制已关闭。` Any other mode does not change the desktop selection. If region or window is not already chosen, the error is `请先选择录制目标。`

## `GET /targets?kind=`

```json
[{"id":"0","label":"1  \\\\.\\DISPLAY1  1920×1080","previewMissing":true}]
```

`kind` defaults to `displays`. Items always include `id`, `label`, and `previewMissing`.

## `PATCH /settings`

Send only the fields the user asked to change.

```json
{"theme":"Dark"}
{"uiLanguage":"zh-Hans"}
{"saveFolder":"D:\\Videos\\Recordings"}
```

`theme`: `Light` or `Dark`. `uiLanguage`: `system`, `en`, `zh-Hans`, `zh-Hant`, `ja`, or `ko`. `lanPort` or `port` is HTTP 400 `{"error":"listen port cannot be changed from the web API"}`. Other keys are ignored. GET never returns the real access key.

## Library

`{id}` is `Id` from `GET /library`: 12 hex characters, the leading bytes of the SHA-256 of the full path. Match is case-insensitive.

```json
{
  "Id": "A1B2C3D4E5F6",
  "Path": "D:\\Videos\\Recordings\\Luma-20260923-160000.mp4",
  "Name": "Luma-20260923-160000",
  "SizeBytes": 1048576,
  "Duration": "00:00:00",
  "Created": "2026-09-23T08:00:00+00:00",
  "PosterPath": null,
  "IsAudio": false,
  "SizeText": "1.0 MB",
  "DurationText": "",
  "DateText": "2026-09-23 16:00"
}
```

Delete without `confirm=true` is HTTP 400 `{"error":"confirmation is required"}`. Unknown id is HTTP 404 `{"error":"not found"}`.
