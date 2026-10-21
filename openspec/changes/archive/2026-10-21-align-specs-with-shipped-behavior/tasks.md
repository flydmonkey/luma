## 1. 规格

- [x] 1.1 `screen-recording` 的 Automation 只保留分段录制，并写明不提供登录开录和定时录制。对照 `SettingsStore.Load` 清空 `StartAtLogon` 和 `Schedules`，以及桌面和网页设置页都没有这两个开关
- [x] 1.2 `screen-recording` 的 Recording session control 改为停录后先显示处理中再进预览，主窗口隐藏或静默网页停录时不切换页面。对照 `MainWindow.StopAsync`
- [x] 1.3 `screen-recording` 的 Four capture modes 改为每次启动从默认显示器的显示器模式开始。对照 `SettingsStore.Load` 重置 `LastMode` 和 `MonitorIndex`
- [x] 1.4 `desktop-app` 的 Persistent settings 把「自动化规则」改为「分段录制规则」，并写明不恢复上次的模式和显示器
