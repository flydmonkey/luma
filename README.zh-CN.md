<div align="center">

# Luma

**本地优先的 Windows 录屏工具，画面采集走 libobs。**

[English](README.md) · [简体中文](README.zh-CN.md)

![Windows](https://img.shields.io/badge/Windows-10%201809%2B-0078D6?style=flat-square)
![Version](https://img.shields.io/badge/version-1.0.0-1a1a1a?style=flat-square)
![Architecture](https://img.shields.io/badge/x64-免安装-555?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-8-512BD4?style=flat-square)

[仓库](https://github.com/flydmonkey/luma) · [AI Skill](.cursor/skills/luma-control/SKILL.md) · [OpenAPI](docs/openapi/openapi.json)

</div>

Luma 可以录制显示器、区域、窗口，或只留下声音，也可以截一张带标注的静态图。不需要账号。系统声和麦克风分开控制，文件保存在你指定的目录，片库里的整理也在这台电脑上完成。

你可以在 Windows 桌面上直接使用，也可以在同一局域网的浏览器里打开同样的三页；兼容 Skill 的 AI 客户端则通过随仓库提供的 `luma-control` 调用本机接口。

实时采集和硬件编码使用开源 [libobs](https://github.com/obsproject/obs-studio)。剪切、压缩等片库工具在应用旁边或 `PATH` 里能找到 `ffmpeg.exe` 时才会运行。

## 为什么选择 Luma

- **本地优先** — 无需登录，正常使用不会上传成片。
- **和桌面一致的采集** — 显示器、区域、窗口、纯音频，以及带本机文字识别的截图。
- **录制台** — 系统声、麦克风、摄像头、文字 / 图片 / 时间戳水印、暂停和自动分段。
- **本机片库** — 预览、改名、删除、修复、压缩、剪切、合并、烧录字幕和混入背景音乐。
- **局域网遥控** — 开始、暂停或停止桌面已经选好的这一路，修改少量设置，并列出成片。
- **可核对的控制面** — Skill 和 OpenAPI 只描述这一版真正提供的路径。
- **五种界面语言** — 简体中文、繁体中文、英语、日语、韩语，或跟随 Windows。

## 可以录什么

| 模式 | 内容 | 适合 |
| --- | --- | --- |
| 显示器 | 一块屏幕 | 演示、课程和完整桌面操作 |
| 区域 | 自己框选的矩形 | 只展示应用的一部分 |
| 窗口 | 选定的一个窗口 | 教程和单应用演示 |
| 录音 | 系统声、麦克风或两者，保存为 `.m4a` | 会议、旁白和声音记录 |
| 截图 | 一张 PNG，可选本机 OCR | 留下或复制某一帧 |

画质预设从 720p 到 4K，默认 30 fps，并优先使用硬件编码。视频容器为 `.mp4`、`.mkv`、`.mov`、`.flv`。这一版不会开始游戏采集。

## AI Skill 与开放接口

仓库自带 [`luma-control`](.cursor/skills/luma-control/SKILL.md)。它只通过局域网接口操作正在运行的 Luma，不点击 WinUI 窗口，也不沿用旧版里已经不存在的路径。

远程录制跟随桌面当前的选择：

1. 请求 `GET /api/v1/session`。出厂地址是 `http://127.0.0.1:12345`。
2. 端口拒绝连接时，说明局域网没开。停下来，请用户打开。
3. 读取 `phase`。需要了解这台电脑能看到什么时，再列出显示器、窗口、摄像头或麦克风。
4. `POST /api/v1/session/start` 录的是桌面里已经选好的目标。区域和窗口必须先在桌面选好。`game` 会被拒绝。
5. 暂停或停止后，再读一次 `phase`。

接口还可以修改主题、界面语言和保存目录，列出片库，并在带上 `confirm=true` 后删除一条。改名、剪切、压缩和修复留在桌面。

- [Skill](.cursor/skills/luma-control/SKILL.md)
- [控制参考](.cursor/skills/luma-control/reference.md)
- [OpenAPI](docs/openapi/openapi.json)

## 片库

成片出现在「我的视频」，直接读取本机保存目录。可以预览、改名、打开所在位置或删除。后期处理会另存新文件，原片还在。这些编辑是桌面功能；局域网接口只能列出和删除。

## 设置与局域网

设置页管理主题、语言、保存位置、画质、容器、声音设备、摄像头和水印、热键、定时、分段、托盘和局域网访问。

打开「局域网访问」后，同一网络里的浏览器可以打开 Luma。浏览器只是远程面板，采集和编码仍在这台 Windows 电脑上。网络不完全可信时请设置访问密钥。网页不能关闭局域网，也不能改监听端口。

出厂地址是 `http://127.0.0.1:12345`。设置页会显示其他设备应使用的地址。密钥放在 `X-Record-Key` 或 `Authorization: Bearer`。

## 快速开始

1. 编译或解压 x64 程序，运行 `Luma.exe`。
2. 选择录制模式，确认系统声和麦克风，然后点「开始」。
3. 截图默认热键是 `Ctrl+Shift+S`。

默认目录是 `视频\Recordings`。

### 系统要求

- 64 位 Windows 10 1809 或更高版本
- x64
- 使用录屏、麦克风或摄像头时，允许 Windows 对应权限
- 只有局域网控制需要网络，录制本身可以离线

## 热键、托盘与自动化

| 操作 | 默认热键 |
| --- | --- |
| 开始录制 | `Ctrl+Alt+R` |
| 暂停或继续 | `Ctrl+Alt+Shift+P` |
| 停止录制 | `Ctrl+Alt+S` |
| 截图 | `Ctrl+Shift+S` |

关闭主窗口时，Luma 默认进入托盘。它可以登录后直接进托盘、登录后开始、按计划录制，或按时长 / 文件大小切开长录制。静默模式可以避免局域网发起的录制把窗口拉到前台。

## 从源码构建

安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)，然后运行：

```powershell
dotnet build src/Luma.App/Luma.App.csproj -c Debug -p:Platform=x64
dotnet test Luma.sln -c Debug -p:Platform=x64
```

免安装程序位于：

`src\Luma.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Luma.exe`

## 文档

- [隐私协议](docs/legal/zh-Hans/privacy.html)
- [使用条款](docs/legal/zh-Hans/terms.html)
- [OpenAPI](docs/openapi/openapi.json)
- [仓库](https://github.com/flydmonkey/luma)

Luma 由 [flydmonkey](https://github.com/flydmonkey) 开发。因为链接了 libobs，许可证为 GPL-2.0 或更高版本。见 `LICENSE`。
