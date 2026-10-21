## Why

局域网服务只要有一个网卡地址绑定失败，就整体退回只监听本机，而且不提示，用户只会发现其他设备连不上。硬件编码回退到软件编码时，宿主在开录时给出的提示会在停录时被覆盖，用户从头到尾都看不到。

## What Changes

- 局域网服务先绑定本机地址，再逐个尝试局域网地址；能绑定的保留，失败的跳过，并在桌面上提示哪些地址打不开。所有局域网地址都失败时，提示目前只有本机能连接。本机地址绑定失败仍按端口占用报错。
- 开启了硬件编码但实际使用软件编码时，开录后立即提示：主窗口可见时用横幅，主窗口已让出画面时用托盘通知；局域网静默开录不提示。停录后的横幅会再次带上这条提示。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `lan-control`：新增 Partial network binding。
- `libobs-backend`：修改 Hardware encoding with software fallback。

## Impact

`LanServer.cs`、`MainWindow.xaml.cs`、`MainWindow.Settings.cs`、`TrayService.cs`、`UiCopy.cs`、`RecordingPolicy.cs` 及对应测试。
