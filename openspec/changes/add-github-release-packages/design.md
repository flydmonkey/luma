## Context

见 `proposal.md` 的 Why。对外行为以 `specs/release-packaging/spec.md` 为准。

应用是 unpackaged WinUI 3（`WindowsPackageType=None`），平台只有 `win-x64`。`WindowsAppSDKSelfContained` 已打开，但 .NET 运行时默认仍依赖本机安装。构建后 `StageObsRuntime` 把 `third_party/obs` 和 `Luma.ObsHost.exe` 复制到 `TargetDir`；`third_party/obs/` 在 gitignore 里，要先跑 `tools/fetch-obs-runtime.ps1`。宿主编译依赖 Visual Studio 的 `vcvars64.bat`。仓库里还没有 `.github/workflows`。

发布形状参考 [satelite-proxy 的 release.yml](https://github.com/zn0wii/satelite-proxy/blob/main/.github/workflows/release.yml)：只在 `v*` 标签上跑，先核对标签和应用版本，创建草稿 Release 并挂上安装包，再由依赖它的 `windows-portable` 作业把便携 zip 用 `gh release upload --clobber` 补上去。那边是 Tauri 多平台；这里只保留这条 Windows 发布顺序，不引入 Rust、Node 或 Tauri。

## Goals / Non-Goals

**Goals:**

- 一条 Windows 工作流在匹配的版本标签上同时给出 exe 安装包和便携 zip。
- 草稿 Release 先带安装包，zip 再挂到同一个 Release。
- 发布目录自包含 .NET 运行时和 Windows App SDK，并带上 libobs 运行时与 `LICENSE`。

**Non-Goals:**

- `workflow_dispatch`，或在一次运行里只打其中一种包。
- Authenticode 签名、证书密钥或 SmartScreen 信誉。
- 把 `ffmpeg.exe` 打进包。
- arm64、MSIX、自动更新、每次 pull request 都打包。
- macOS / Linux 矩阵、Tauri。

## Decisions

### 两个作业，标签触发，草稿 Release

新增 `.github/workflows/release.yml`，结构对齐参考工作流，平台收成 Windows：

```yaml
on:
  push:
    tags:
      - "v*"
permissions:
  contents: write
```

- `release`：核对版本、拉 OBS、编译宿主、测试、publish、打 exe，然后创建草稿 Release 并附上安装包。标题 `Luma ${{ github.ref_name }}`，说明 `Automatic release`，`prerelease: false`，`draft: true`。用 `gh release create --draft`，不使用 tauri-action。
- `windows-portable`：`needs: release`。下载上一作业留下的 publish 目录，打 zip，再 `gh release upload "${{ github.ref_name }}" <zip> --clobber`。

参考仓库的便携作业会再编译一次，因为它的安装包和 portable bundle 是两条 Tauri 路径。Luma 的 zip 和安装包是同一份 publish 目录，所以第二个作业只打包已经传下来的目录，避免把 OBS 宿主和自包含发布做两遍。`needs: release` 仍然保证草稿 Release 已经存在。

不在 pull request 上跑。本机调试仍用 README 里的 `dotnet build`。

### 标签必须等于 Directory.Build.props 的 Version

在 `Directory.Build.props` 写入 `<Version>`，当前与 README 上的 `1.0.0` 一致。`release` 的第一步用 bash 比较，和参考工作流对照 `package.json` 的方式相同：

```bash
TAG_VERSION="${GITHUB_REF_NAME#v}"
# 读出 Directory.Build.props 的 Version
# 不一致则 exit 1
```

不一致就停，此时还没有 Release。触发器仍写 `v*`，真正的门是这次字符串相等，而不是再收窄正则。

### OBS 运行时按脚本哈希缓存

用 `actions/cache`，路径是 `third_party/obs`，key 含 `tools/fetch-obs-runtime.ps1` 的哈希。脚本内容或它里面的 OBS 版本一变，缓存就失效。缓存未命中时再执行该脚本。这对应参考工作流里按 fetch 脚本哈希缓存 bundled cores 的做法。

`Luma.ObsHost` 仍每次用 `tools/build-obs-host.ps1 -Configuration Release` 编译。`windows-latest` 自带 Visual Studio，现有 `vcvars64.bat` 查找沿用。

### 发布目录单独再铺一次 OBS

`release` 作业在版本核对和缓存之后：

1. `actions/setup-dotnet` 安装 .NET 8 SDK。
2. 缓存未命中时 `tools/fetch-obs-runtime.ps1`，然后 `tools/build-obs-host.ps1 -Configuration Release`。
3. `dotnet test Luma.sln -c Release -p:Platform=x64`。失败则停止，不创建 Release。
4. `dotnet publish src/Luma.App/Luma.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true`。
5. 对 publish 输出目录再执行一次 `tools/stage-obs-runtime.ps1 -Dest <publish目录>`，复制仓库根目录的 `LICENSE`。缺少 `Luma.exe`、`Luma.ObsHost.exe` 或 `obs.dll` 则失败。
6. 用这份目录打 exe，创建草稿 Release。把同一目录作为 artifact 交给 `windows-portable`。

现有 `StageObsRuntime` 只把文件拷进 `TargetDir`，那些文件不在 publish 项里，所以打包前必须对 publish 目录再铺一次。

zip 名为 `Luma-<version>-win-x64.zip`，根目录就是 publish 目录的内容。安装包名为 `Luma-<version>-win-x64-setup.exe`。`<version>` 就是去掉 `v` 的标签。

### exe 安装包用 Inno Setup，按当前用户安装

用 Inno Setup 6 编译 `installer/luma.iss`。参考仓库的安装包来自 Tauri；Luma 没有这条打包器，Inno 只负责把已有的 unpackaged 目录收成 exe。

- `PrivilegesRequired=lowest`，安装到 `{autopf}\Luma`。非管理员时这是 `%LOCALAPPDATA%\Programs\Luma`。应用自己的设置、OBS 配置和日志已经在 `%LOCALAPPDATA%\Luma`，安装目录不能和数据目录是同一个文件夹。
- 开始菜单快捷方式名称为 `Luma`，指向 `Luma.exe`。
- 带卸载项。`AppVersion` 使用核对过的版本号。

### 不签名，不带 FFmpeg

工作流不配置证书，不对 exe 或安装包做 signtool。片库仍按现有逻辑在应用旁边或 `PATH` 上找 `ffmpeg.exe`。

## Risks / Trade-offs

- [OBS 官方 zip 地址或文件名变化] → `fetch-obs-runtime.ps1` 失败则 `release` 失败，不创建草稿。版本号继续由该脚本的默认参数决定。
- [publish 目录漏掉 obs.dll 或宿主] → 打包前检查 `Luma.exe`、`Luma.ObsHost.exe` 和 `obs.dll`，缺一即失败。
- [`windows-portable` 失败时草稿里只有 exe] → 与参考工作流相同。维护者看到草稿不齐就不要公开；修正后对同一标签重跑，上传使用 `--clobber`。
- [未签名安装包触发 SmartScreen] → 接受。签名留到以后有证书再加。
- [`windows-latest` 找不到 `vcvars64.bat`] → 脚本已有递归查找。仍找不到则工作流失败并保留日志。
- [自包含发布体积大] → 只在版本标签上跑，不挂在每个 pull request 上。

## Migration Plan

合并后，把 `Directory.Build.props` 的 `<Version>` 设成要发布的版本，推送同名 `vX.Y.Z` 标签。到 Actions 确认草稿 Release 里同时有 zip 和 exe，再由维护者把草稿公开。回滚是删掉工作流、`installer/luma.iss`，并去掉新增的 `<Version>`；不影响本机调试构建。

## Open Questions

无。
