# Luma `/api/v1` verbs

Open this file before sending any JSON body. The browser uses the same routes. Default `{base}`: `http://127.0.0.1:12345`.

Every JSON response is `{ "ok": true, "data": {}, "error": null }`. Failure sets `ok` to false and `error` to a string.

| Method | Path | Body / result |
|--------|------|----------------|
| GET | `/` | LAN shell. A stored access key shows the unlock form first. |
| GET | `/api/v1` | `{name:"luma", control:true}` |
| GET | `/api/v1/session` | `{session, target}` |
| POST | `/api/v1/session/start` | none. Uses the target from `PUT /target`. |
| POST | `/api/v1/session/pause` | toggles pause |
| POST | `/api/v1/session/stop` | saves the file; `session.lastSaved.name` is the file name |
| GET | `/api/v1/targets/displays` | `{id, title, detail, preview}` |
| GET | `/api/v1/targets/windows` | same shape |
| GET | `/api/v1/targets/games` | empty. Game capture is closed. |
| GET | `/api/v1/targets/cameras` | device list |
| GET | `/api/v1/targets/microphones` | device list |
| PUT | `/api/v1/target` | see below |
| GET | `/api/v1/settings` | full settings. `lan.accessKey` is `""` or `***`. |
| PATCH | `/api/v1/settings` | partial settings. `lan`, `lanPort`, and `port` are rejected. |
| POST | `/api/v1/settings/reset` | factory settings, keeping the current LAN port, switch, and key |
| GET | `/api/v1/library` | `{id, name, length, duration, date, isAudio}` |
| PATCH | `/api/v1/library/{id}` | `{"name":"..."}` |
| DELETE | `/api/v1/library/{id}?confirm=true` | required confirm |
| POST | `/api/v1/library/{id}/compress` | `{jobId}` |
| POST | `/api/v1/library/{id}/trim` | `startSeconds`, `endSeconds` |
| POST | `/api/v1/library/{id}/repair` | `{jobId}` |
| GET | `/api/v1/jobs/{jobId}` | `status` is `running`, `done`, or `error` |
| GET | `/media/{id}` | file bytes |
| GET | `/poster/{id}` | poster jpeg when one exists |

## Auth

If `lan.accessKey` is set: `X-Record-Key`, `Authorization: Bearer`, or the `luma_lan` cookie from `POST /unlock`. Never print the key.

## `PUT /target`

```json
{"mode":"fullscreen","displayId":"0"}
{"mode":"audio"}
{"mode":"window","windowId":"<id>","windowTitle":"Notes"}
{"mode":"region","displayId":"0","region":{"x":0,"y":0,"width":800,"height":600}}
```

`mode`: `fullscreen`, `region`, `window`, or `audio`. `game` returns `游戏录制已关闭。` Region width and height must be greater than 1. Window needs `windowId` from the list.

## `GET /session` `data.session`

`state`: `idle`, `recording`, or `paused`. `elapsed` is `hh:mm:ss`. `lastSaved` is `{name, warning}` or null.

## Library jobs

`{id}` is `GET /library` → `id`. Poll `GET /jobs/{jobId}` until `status` is `done` or `error`. Merge, captions, and music stay on the desktop.
