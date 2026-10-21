## 1. 代码

- [x] 1.1 局域网或接口开录显示器/区域时，先把可见主窗口移出画面
- [x] 1.2 热键、托盘、局域网开录也检查系统版本，提示走 `os.unsupported`
- [x] 1.3 桌面和局域网剪切在结束时间不晚于开始时间时拒绝，提示走 `lib.trim.invalid`
- [x] 1.4 合并按录制时间先后排序
- [x] 1.5 目标列表每项带 `previewMissing`
- [x] 1.6 `effectiveFps` 按实际输出帧计算
- [x] 1.7 托盘提示显示录制时长
- [x] 1.8 删掉不会再出现的已保存/处理中面板、定时开录轮询和 `ScheduleGate`、网页游戏目标残留、`ListWindows` 的 `gamesOnly` 参数

## 2. 文档

- [x] 2.1 OpenAPI 补 `/media` 的 HEAD、Range、206、416，`TargetItem.previewMissing`，`LibraryItem.missing`，五种语言的 `/media` 说明
- [x] 2.2 `luma-control` 技能两份副本补同样的字段

## 3. 规格

- [x] 3.1 `libobs-backend` Output containers 加 mkv、mov、flv
- [x] 3.2 `lan-control` 写明文档路由免解锁，目标列表写明 `previewMissing`
- [x] 3.3 `screen-recording` 收窄透明度范围，加网页停录浮层
- [x] 3.4 `web-screenshot` Esc 逐层关闭
- [x] 3.5 `recording-library` 合并按录制先后
