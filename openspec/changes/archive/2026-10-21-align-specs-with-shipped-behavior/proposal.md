## Why

主规格里有几条要求和已经发布的行为不一致：登录后自动开录和定时录制在提交 `361e258` 中有意去掉了，停录后直接进入预览是提交 `7ffbc5c` 有意改的，每次启动回到显示器模式也是同一次提交的决定。规格继续写着旧行为，审计时会被当成缺陷，也会误导后续实现。

## What Changes

- 自动化只保留分段录制。去掉登录后自动开录和定时开始、停止，以及定时与正在录制冲突的场景。
- 停录后：主窗口可见时先显示处理中，再在片库预览里播放新文件；主窗口为录制而隐藏且不需要带回、或静默模式下由网页停止时，不切换页面，也不弹出窗口。
- 每次启动都从显示器模式开始，不恢复上次的录制模式和显示器；默认选中显示器，用户不必先选模式也能开录。
- 持久化设置里的「自动化规则」改为「分段录制规则」。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `screen-recording`：以「Session control and stop preview」取代 Recording session control，以「Segmented recording」取代 Automation，并修改 Four capture modes。
- `desktop-app`：修改 Persistent settings 要求。

## Impact

只改规格，不改代码。涉及 `openspec/specs/screen-recording/spec.md` 和 `openspec/specs/desktop-app/spec.md`。
