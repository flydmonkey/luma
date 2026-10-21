## Why

第二轮规格核对又发现几处规格和已发布行为不一致：录制格式早已支持 mkv、mov、flv（提交 `913a5cc`），接口文档、法律文本和技能文件本来就不需要解锁，网页停录走的是自己的"已保存"浮层，网页截图的 Esc 是逐层关闭，摄像头和时间戳并没有透明度设置。这些写法会在验收时被当成缺陷，也会误导后续实现。

同一轮也修了几处确实偏离规格的代码：局域网开录时主窗口没让出画面、热键/托盘/局域网开录没检查系统版本、剪切区间无效的提示没本地化、合并没按录制先后排序、目标列表缺预览时没有明确标记。

## What Changes

- 视频录制格式：默认 `.mp4`，用户可改为 `.mkv`、`.mov`、`.flv`；只录音仍是 `.m4a`。
- 局域网：存了访问密钥时，控制路由、网页和媒体仍需解锁；接口文档、OpenAPI、技能文件、隐私政策和服务条款不需要解锁，且不含录制文件或设置。
- 停录预览：桌面行为不变；网页停录在网页里显示"已保存"浮层，可从浮层跳到网页的视频预览。
- 网页截图：Esc 先关文字输入，再关识别结果，最后才关闭整个截图层。
- 合并：按录制时间先后拼接，不按点选顺序。
- 摄像头和水印：文字和图片水印有位置、大小和透明度；摄像头和时间戳只有位置和大小。
- 目标列表：每项都带 `previewMissing`，没有预览图时为 true。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `libobs-backend`：修改 Output containers。
- `lan-control`：修改 LAN server and optional access key、Targets include labels and previews。
- `screen-recording`：修改 Webcam and custom watermarks、Session control and stop preview。
- `web-screenshot`：修改 Cancel without a file。
- `recording-library`：修改 Trim merge subtitles and music。

## Impact

规格见上。代码改动在 `MainWindow.xaml.cs`、`MainWindow.Library.cs`、`LanControlApi.cs`、`obs_session.cpp`、`TrayService.cs`、`app.js`；文档改动在 `docs/openapi/*.json` 和 `luma-control` 技能的两份副本。
