## Why

仓库现在只能在本机 `dotnet build` 出调试目录，没有可重复的发布包。别人要试用 Luma，就得自己装 SDK、拉 OBS 运行时并编译宿主。需要一条 GitHub Actions 流水线，在打版本标签时同时给出 zip 和 exe 安装包。

## What Changes

- 增加只在 `v*` 标签上运行的 Windows 发布工作流。标签去掉 `v` 之后必须和仓库里的应用版本一致，否则失败并且不创建 Release。
- 测试通过后，以 Release / win-x64 / 自包含方式发布，铺上 OBS 运行时。同一份发布目录打成便携 zip，也打成 Inno Setup 的 exe 安装包。
- 第一个作业创建标题为 `Luma <tag>` 的草稿 Release，附上 exe。第二个作业把 zip 补到同一个 Release 上。
- 包内带上 GPL-2.0 许可证文本。不签名，不把 FFmpeg 打进包。

## Capabilities

### New Capabilities

- `release-packaging`: 推送匹配应用版本的 `v*` 标签时，GitHub Actions 产出 x64 zip 与 exe 安装包，并挂到该标签的草稿 GitHub Release。

### Modified Capabilities

## Impact

- 新增 `.github/workflows/release.yml`，以及 Inno Setup 脚本（按当前用户装到 `%LOCALAPPDATA%\Programs\Luma`，创建开始菜单快捷方式、可卸载）。
- `Directory.Build.props` 增加 `<Version>`，供标签核对。工作流依赖已有的 `tools/fetch-obs-runtime.ps1` 与 `tools/build-obs-host.ps1`，在 `windows-latest` 上执行。
- 发布物是自包含的 unpackaged WinUI 应用，目标仍是 Windows 10 1809+ x64。不改录制、片库或局域网行为。
- 产物未签名，Windows SmartScreen 可能提示未知发布者。Release 以草稿创建，由维护者核对后再公开。
