# Luma `/api/v1` verbs

Open this file before sending any JSON body. The browser uses the same routes. Default `{base}`: `http://127.0.0.1:12345`.

Every JSON response is `{ "ok": true, "data": {}, "error": null }`. Failure sets `ok` to false, `data` to null, and `error` to a string. Unknown API routes are HTTP 404 with `未找到这个接口。` Missing files on `/media` and `/poster`, and missing jobs, are HTTP 404. Other API failures are HTTP 400.

| Method | Path | Body / result |
|--------|------|----------------|
| GET | `/` | LAN shell. A stored access key shows the unlock form first. |
| POST | `/unlock` | Form field `access_key`. Only when the request is still locked. Success is HTTP 302 and a `luma_lan` cookie. |
| GET | `/api/v1` | `{name:"luma", control:true}` |
| GET | `/api/v1/session` | `data` is `{session, target}` |
| POST | `/api/v1/session/start` | Optional `{"mode":"..."}` guard only. `data` is the session snapshot. |
| POST | `/api/v1/session/pause` | Toggles pause when recording or paused. Idle stays idle. `data` is the session snapshot. |
| POST | `/api/v1/session/stop` | Starts saving and returns the snapshot immediately. Poll `GET /session`. |
| GET | `/api/v1/targets` | Query `kind`. Same list as the path below. |
| GET | `/api/v1/targets/{kind}` | `{id, title, detail, preview}`. `kind` is `displays`, `windows`, `games`, `cameras`, `microphones`, or `mics`. |
| GET | `/api/v1/screenshot` | Host still. Optional `windowIndex` selects one window by list index. |
| POST | `/api/v1/screenshot/ocr` | `{"png":"<base64>"}`. `data` is `{text}`. |
| PUT | `/api/v1/target` | See below. `data` is the target summary. |
| GET | `/api/v1/settings` | Full settings. `lan.accessKey` is `""` or `***`. |
| PATCH | `/api/v1/settings` | Partial settings. `POST` is the same. |
| POST | `/api/v1/settings/reset` | Factory settings, keeping the current LAN object and save folder. |
| GET | `/api/v1/library` | `{id, name, length, duration, date, isAudio}` |
| PATCH | `/api/v1/library/{id}` | `{"name":"..."}` |
| DELETE | `/api/v1/library/{id}?confirm=true` | Required. `data` is `{deleted}`. |
| POST | `/api/v1/library/{id}/compress` | `{jobId}` |
| POST | `/api/v1/library/{id}/trim` | `startSeconds`, `endSeconds`. `{jobId}` |
| POST | `/api/v1/library/{id}/repair` | `{jobId}` |
| GET | `/api/v1/jobs/{jobId}` | `{id, kind, status, result, error}` |
| GET | `/media/{id}` | File bytes. |
| GET | `/poster/{id}` | Poster JPEG when one exists. |
| GET | `/openapi.json` | This API. Query `lang` selects the overlay. Also at `/skill/openapi.json`. |
| GET | `/api/docs` | RapiDoc page. |
| GET | `/skill` | This skill, as HTML. |
| GET | `/skill/reference.md` | This file. |

## Auth

If `lan.accessKey` is set: `X-Record-Key`, `Authorization: Bearer`, or the `luma_lan` cookie from `POST /unlock`. A missing or wrong key is HTTP 401 and `需要访问密钥。` Docs, `/openapi.json`, and `/skill` are served before that check. Never print the key.

## `GET /session`

`data.session`:

- `state`: `idle`, `recording`, `paused`, or `stopping`. Countdown is reported as `recording`.
- `elapsed`: `hh:mm:ss`
- `lastSaved`: `{name, warning}` or null. `name` is the file name.
- `encoderName`, `actualWidth`, `actualHeight`, `stopForced`

`data.target`:

- `mode`: `fullscreen`, `region`, `window`, or `audio`
- `monitorIndex`
- `region`: `{x, y, width, height}` or null
- `windowId`, `windowTitle`

`POST /session/start`, `/pause`, and `/stop` put that session object directly in `data`. They do not wrap `target`.

## `PUT /target`

```json
{"mode":"fullscreen","displayId":"0"}
{"mode":"audio"}
{"mode":"window","windowId":"<id>","windowTitle":"Notes"}
{"mode":"region","displayId":"0","region":{"x":0,"y":0,"width":800,"height":600}}
```

`mode`: `fullscreen`, `region`, `window`, or `audio`. `game` and `3` return `游戏录制已关闭。` Region width and height must both be greater than 0. A window target needs `windowId` from `GET /targets/windows`. Otherwise start returns `请先选择录制目标。`

`displayId` sets `monitorIndex`.

## Targets

`preview` is a `data:image/jpeg;base64,...` string or null. Games are always an empty array. Any other `kind` lists displays. `windowIndex` on the still is the index in that windows array, not `id`.

## Screenshot

`GET /screenshot` returns `data` with `originX`, `originY`, `width`, `height`, `png`, `monitors`, and `windows`. `png` is raw base64, not a data URL. `windowIndex` returns that window's pixels in the same shape. A bad index is HTTP 400 and `没有可截取的窗口。`

`POST /screenshot/ocr` reads `png`. An empty image is HTTP 400 and `空图片。` Success is `{text}`, and `text` may be null.

## Settings

Readable fields: `saveFolder`, `lastMode`, `monitorIndex`, `quality`, `recordingFormat`, `audio`, `overlay`, `hotkeys`, `automation`, `closeToTray`, `launchToTray`, `hideTrayIcon`, `showRecordingBar`, `silentMode`, `theme`, `uiLanguage`, `lan`, `resolvedLanguage`.

`theme` is `Light` or `Dark`. `lastMode` is `Display`, `Region`, `Window`, `Game`, or `AudioOnly`. `quality.level` is `Sd`, `Hd`, `ExtraHd`, or `FourK`. `recordingFormat` is `mp4`, `mkv`, `mov`, or `flv`.

Writable fields: `theme`, `saveFolder`, `showRecordingBar`, `silentMode`, `closeToTray`, `launchToTray`, `hideTrayIcon`, `monitorIndex`, `lastMode`, `quality`, `audio`, `overlay`, `hotkeys`, `automation`, `uiLanguage`, `recordingFormat`.

`lan`, `lanPort`, `port`, and `lanPlayback` return `不能从网页修改局域网端口或开关。` Any other name returns `不能修改字段 {name}。` An empty `saveFolder` is rejected. On write, `theme` may be `Light`, `Dark`, `1`, or `2`. A successful patch forces `automation.startAtLogon` to false and `automation.schedules` to `[]`. If `hotkeys` omits `screenshot`, the previous screenshot hotkey is kept.

`POST /settings/reset` keeps `lan` and `saveFolder` and restores the other fields.

## Library jobs

`{id}` matches `GET /library` → `id` or the file name, case-insensitive. `length` is bytes. `duration` is a time-span string such as `00:00:11.8000000`.

Delete without `confirm=true` returns `删除需要 confirm=true。` A missing id returns `未找到这个文件。` That is HTTP 400, not 404.

Poll `GET /jobs/{jobId}` until `status` is `done` or `error`. `result` is the output path when done. Compress, trim, and repair are the supported kinds. Merge, captions, and music stay on the desktop. Another kind is accepted as a job and then finishes with `不支持的作业。`
