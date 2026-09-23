## Why

`record` 仓库里的 Luma 用自研 Media Foundation + GPU NV12（`GpuSurfaceWriter`）做硬件加速，已经撞上 sample 生命周期、SinkWriter backlog 和「提交成功 ≠ 编码完成」等问题，继续修代价很高。现在要在本仓库新建一版 Luma：界面和用户功能对齐现有产品，实时采集/硬编/封装改走开源 **libobs**，不再自己实现编码器。

## What Changes

- 在空的 `luma-windows` 仓库新建 WinUI 3 / .NET 8 桌面应用，复刻 `record` 的整体界面与信息架构：录制、我的视频、设置三页，以及区域选择、浮动录制条、托盘与热键。
- 用户功能对齐现有 Luma：五种录制模式、系统声/麦克风、摄像头与水印、倒计时与暂停、分段/定时/登录自动化、本机片库与 FFmpeg 后期、局域网 Web 与 Control API。局域网监听端口可在桌面设置中修改，出厂默认为 `12345`（TCP 端口合法范围是 1–65535；`123456` 非法，不能绑定），以便与仍占用 `18765` 的旧版 Luma 并行。
- **BREAKING（相对 `record` 实现，不是对外文件格式）**：实时媒体管线不再使用 Media Foundation SinkWriter、自研 `GpuSurfaceWriter`、自定义 NV12，或 DXGI→编码器的自管 GPU 槽。采集、混音、硬件编码（NVENC/AMF/QSV）与 MP4/M4A 封装由 libobs 及其插件完成。
- 画质档位、硬件编码开关、软件回退、会话里的真实编码器名 / 有效帧率 / 实际分辨率仍对用户可见；统计口径改为 libobs 的 packet/skipped，而不是 `WriteSample` 提交数。
- 片库剪切、合并、压缩、修复、字幕、配乐仍用随包 FFmpeg，不经过 libobs。
- 不引入 OBS 多场景编排，也不做 RTMP 直播（现有产品也没有这两项）。

## Capabilities

### New Capabilities

- `desktop-app`: 无账号 WinUI 3 本地壳；三页导航、设置持久化、主题与语言、热键、托盘、单实例与登录行为。
- `screen-recording`: 显示器 / 区域 / 窗口 / 游戏 / 纯音频；目标门禁、倒计时、会话控制、浮动条、叠加与自动化。
- `libobs-backend`: 用 libobs 完成实时采集、混音、硬编与封装；硬件失败回退软件；禁止自研编码栈。
- `recording-library`: 本机片库列表、预览、重命名/删除，以及 FFmpeg 剪切、合并、压缩、修复、字幕、配乐。
- `lan-control`: 局域网三页 Web 与 `/api/v1` 控制接口，远程操作同一套本机会话；监听端口可配置，出厂默认 `12345`。

### Modified Capabilities

- （无。本仓库尚无主 spec。）

## Impact

- 本仓库从空壳变为完整应用：将新增解决方案、WinUI 工程、libobs 原生互操作层，以及测试项目。
- 新增原生依赖：开源 libobs（GPL-2.0）及其 Windows 采集/编码插件；发行包需随附兼容许可证与 obs 运行时文件。链接 libobs 后本项目必须保持 GPL 兼容。
- 后期仍依赖随包 FFmpeg；不把 FFmpeg 当主录制编码器。
- 不修改 `C:\Users\Administrator\Projects\record`。该仓库只作 UI/功能对照，新旧应用可并行存在。
- 对外文件仍是本机 `.mp4` / `.m4a`；Control API 路径与语义尽量对齐现有 Luma，便于以后接 `luma-control` Skill。默认监听改为 `http://127.0.0.1:12345`，避免和新旧实例抢 `18765`。
- 独立产品网站、五语法律文档站、仓库内 Skill 不在本变更交付范围内，可后续单独提案。
