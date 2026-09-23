## Context

本仓库目前只有 Agent / OpenSpec / WinUI 技能，没有应用代码。对照实现是 `C:\Users\Administrator\Projects\record`：WinUI 3 三页壳 + 自研 DXGI/WGC 采集 + Media Foundation 硬编。动机与产品范围见 `proposal.md`；用户可见行为见 `specs/`。

约束：x64、Windows 10 1809+、.NET 8、WinUI 3、Windows App SDK 1.6、免安装自包含；实时管线必须走 libobs；片库后期可以继续用随包 FFmpeg；链接 libobs 后许可证必须与 GPL-2.0 兼容。

## Goals / Non-Goals

**Goals:**

- 用新代码重建与现有 Luma 同构的桌面壳，而不是在 `record` 里继续打补丁。
- 把实时采集、混音、硬编、封装收成一条 libobs 会话，UI 只发「配源 / 开停 / 读状态」。
- 用独立进程隔离 GPU 驱动和编码器崩溃，避免 UI 被硬编带崩。
- 会话状态用 OBS 的已编码 packet / skipped 计数，而不是提交帧数。

**Non-Goals:**

- 不复用 `record` 的 `Record.Media/Encoding`、`GpuSurfaceWriter` 或 DXGI 共享设备槽。
- 不把完整 OBS Studio 界面或多场景编排暴露给用户。
- 不在本设计中做 RTMP / 推流。
- 不把 `record` 仓库改成本仓库的子模块或自动同步源。
- 不在第一期做产品网站、法律文档站和 `luma-control` Skill。

## Decisions

### 1. 新仓库绿场重建，对照但不拷贝编码栈

在 `luma-windows` 用 `dotnet new winui` 建解决方案，按现有 Luma 的信息架构重写 XAML/C#。`record` 只作交互与文案对照。

- 备选：直接 fork `record` 并替换编码器。否决原因：自研管线与会话、采集、telemetry 缠在一起，替换成本接近重写，还容易把 MF 缺陷带过来。

### 2. 实时媒体走独立 `Luma.ObsHost` 进程

架构：

```
Luma.exe (WinUI, STA)
  └─ Luma.Core（设置、会话门面、片库、LAN）
        ├─ IRecordingEngine ──named pipe──► Luma.ObsHost.exe
        │                                      └─ libobs + Windows 插件
        └─ IMediaEditor ──进程──► 随包 FFmpeg（仅后期）
```

`Luma.ObsHost` 是原生 C++ 宿主：`obs_startup`、加载模块、创建**一个**内部 scene、按模式挂 source / filter、选 encoder、用 OBS 的 MP4/M4A output 开停。C# 只看见 `Start/Pause/Stop/GetStatus` 和配置 DTO。

选择独立进程而不是进程内 P/Invoke：

- WinUI STA 与 libobs 图形线程不能混用。
- 硬编 / 驱动崩溃不应带走设置页和片库。
- 以后可以单独重启宿主而不杀 UI。

备选：

- 进程内 C++/CLI 或 P/Invoke：集成简单，但驱动崩溃会杀整个 Luma。
- 启动完整 `obs64.exe` + obs-websocket：能用，但依赖 OBS 安装/便携包，窗口和场景模型会漏给用户，和「嵌入 libobs」不符。

### 3. 用官方 OBS Windows x64 运行时，而不是自编译整棵 OBS

锁定一组已发布的 OBS Studio Windows x64 构建（或同等 libobs + 官方插件集），随应用发布：`obs.dll`、`obs-frontend` 不需要、采集/编码/封装插件、`data/`、locale。仓库用脚本拉取并做版本钉扎，不把整个 OBS 源码树当日常开发依赖。

宿主只调用 libobs C API 和模块加载，不链 `obs-frontend-api`，不显示 OBS 主窗。

备选：从源码编译 libobs。可复现性更好，但 Windows 依赖面大，第一期会拖垮「先把产品跑起来」。

### 4. 模式映射到 OBS source，而不是自研 DXGI 再喂帧

| Luma 模式 | OBS 侧 |
| --- | --- |
| 显示器 | `monitor_capture` |
| 区域 | `monitor_capture` + crop filter |
| 窗口 | `window_capture` |
| 游戏 | `game_capture`，失败则明确报错 |
| 纯音频 | 不建视频源，只建音频 output |
| 系统声 / 麦克风 | `wasapi_output_capture` / `wasapi_input_capture` |
| 摄像头 | `dshow_input`（或当前 OBS Windows 摄像头源） |
| 文字 / 时间戳 / 图片水印 | `text_gdiplus` / `image_source` |
| 视频编码 | 按 GPU 探测 `jim_nvenc` / AMF H.264 / `obs_qsv11`，失败 `obs_x264` |
| 封装 | OBS 的 MP4 mux（优先可修复 / fragmented MP4）；纯音频 AAC → `.m4a` |

区域选择、窗口列表、倒计时仍由 WinUI 负责；宿主只接收已经确认的 monitor id、crop 矩形或窗口标识。

备选：保留 DXGI/WGC，只把帧推进 libobs encoder。否决原因：还是要自管 GPU 表面生命周期，正是当前过不去的坑；libobs 的价值在整条 source → video-io → encoder → output。

### 5. 产品层保持 Luma 语义，OBS 内部 scene 不外露

设置、热键、托盘、倒计时、浮动条、Honest 字段（实际分辨率、有效帧率、编码器名、是否硬编、skipped）仍是 Luma 概念。OBS 的 scene/source 树是实现细节，设置页不出现「场景」。

暂停：停 output 或对源静音/冻结，保证成片去掉暂停区间。分段：停当前 output、立刻按同一配置开下一个文件。

桌面设置暴露 LAN 监听端口，出厂 `12345`。请求中的 `123456` 超出 TCP 上限 65535，不能绑定；合法范围为 1–65535。占用时报错，不自动改端口。网页和 API 不能改端口。

### 6. 项目与许可证

建议工程：

- `src/Luma.App` — WinUI 壳
- `src/Luma.Core` — 设置、会话、片库目录、LAN
- `src/Luma.Obs` — C# 宿主客户端（named pipe）
- `native/Luma.ObsHost` — C++ libobs 宿主
- `src/Luma.Media` — 仅 FFmpeg 后期
- `tests/Luma.Core.Tests`、`tests/Luma.Obs.Tests`

根许可证采用与 libobs 兼容的 GPL-2.0（或 GPL-2.0-or-later，以律师/选定 OBS 版本的 COPYING 为准）。发行包带 `COPYING`、`ThirdPartyNotices`、OBS 与 FFmpeg 源码获取方式。UI 品牌仍是 Luma，不显示 OBS 主界面。

### 7. UI 对照方式

第一期按 `record` 的三页、模式行、设置分组和浮动条重做，达到「同一条用户路径」而不是像素级叉叉对齐。资源（图标、五语文案）可以从 `record` 对照抄产品文案；编码与采集代码不搬。

### 8. 实施切片

1. 脚手架 + 空壳三页 + 设置落盘
2. ObsHost 最小闭环：显示器 + 系统声 → 硬编 MP4
3. 五种模式、目标选择、倒计时、暂停、浮动条
4. 叠加与自动化
5. 片库 + FFmpeg 后期
6. LAN Web / API
7. 五语与托盘/热键收尾

每一切片都必须能在本机用真实文件验收，而不是只靠 mock 编码器。

## Risks / Trade-offs

- [GPL-2.0 传染] → 本仓库按 GPL 兼容发布；不链闭源 SDK 进 ObsHost。若以后要换许可证，必须先拿掉 libobs。
- [OBS 运行时体积大] → 只打包 Windows 录制需要的插件和 data，去掉浏览器源、websocket、前端、无用 locale。
- [game_capture 权限 / 反作弊] → 失败时明确提示改无边框或改窗口模式，不写黑片。
- [宿主崩溃] → UI 把会话标失败，提示可修复的部分文件，允许重启宿主后再录。
- [暂停 / 分段语义和 OBS output 不完全同构] → 在 ObsHost 内用「停 output / 再开 output」适配，产品层仍是单会话。
- [对照 `record` 时带入版权或缺陷代码] → 只对照交互；禁止复制 `Record.Media/Encoding`。
- [插件 API 随 OBS 小版本变化] → 钉扎一组官方构建，升级单独做变更。

## Migration Plan

这是新应用，没有旧安装要迁移。

- 开发期：`record` 与 `luma-windows` 并行；用户设置、片库目录不自动导入。本应用默认监听 `12345`，避免和旧版固定的 `18765` 冲突。用户可在桌面设置改端口（1–65535）；占用时明确报错，不静默换端口。网页和 API 不能改端口，以免远程把自己踢下线。
- 回滚：停用本仓库构建即可，不影响已安装的旧 Luma。
- 若将来替换旧发行包：保持 `.mp4` / `.m4a` 和默认保存目录。端口不再固定成 `18765`。设置 JSON 形状可以对齐旧字段，但第一期不承诺读取旧 `%AppData%` 文件。

## Open Questions

- 钉扎哪一个 OBS Studio 稳定版（实现时选当前 Windows x64 发行版并写入脚本）。
- named pipe 的报文用 JSON 还是定长二进制（不影响规格，实现宿主客户端时再定）。
- 摄像头源在选定 OBS 版本里的准确模块 id（`dshow_input` 或后继名）。
