## 1. 版本号

- [x] 1.1 在 `Directory.Build.props` 增加 `<Version>1.0.0</Version>`。用 `dotnet msbuild src/Luma.App/Luma.App.csproj -getProperty:Version -p:Platform=x64` 确认输出是 `1.0.0`

## 2. 安装脚本

- [x] 2.1 新增 `installer/luma.iss`：从 publish 目录收文件，`PrivilegesRequired=lowest`，装到 `{autopf}\Luma`（当前用户时为 `%LOCALAPPDATA%\Programs\Luma`，避开应用数据目录 `%LOCALAPPDATA%\Luma`），开始菜单快捷方式名为 Luma，并注册卸载。用 Inno 编译一次，确认安装不要求提权，开始菜单有 Luma，卸载后安装目录和快捷方式都消失
- [x] 2.2 `AppVersion` 使用传入的版本号，产物名为 `Luma-<version>-win-x64-setup.exe`。用版本 `1.2.3` 编译，确认文件名，且安装包内没有 `ffmpeg.exe`

## 3. 发布工作流

- [x] 3.1 新增 `.github/workflows/release.yml`：只在 `push` 标签 `v*` 时运行，`permissions` 只有 `contents: write`。没有 pull request 或 `workflow_dispatch`。对照文件确认触发器和权限
- [x] 3.2 `release` 作业第一步用 bash 比较 `${GITHUB_REF_NAME#v}` 和 `Directory.Build.props` 的 `Version`，不一致就失败。读该步骤，确认比较发生在创建 Release 之前
- [x] 3.3 `release` 用 `actions/cache` 缓存 `third_party/obs`，key 含 `tools/fetch-obs-runtime.ps1` 的哈希；未命中才执行该脚本，然后 `build-obs-host.ps1 -Configuration Release`。对照工作流里的 cache 路径和 key
- [x] 3.4 测试步骤是 `dotnet test Luma.sln -c Release`，位于 publish、安装包和 `gh release create` 之前，失败会中止后续步骤。读工作流步骤顺序确认
- [x] 3.5 publish 使用 `Release`、`win-x64`、`--self-contained true`，再对 publish 目录跑 `stage-obs-runtime.ps1` 并复制 `LICENSE`；缺少 `Luma.exe`、`Luma.ObsHost.exe` 或 `obs.dll` 时失败。在本机按同样命令打一份目录，确认这三个文件和 `LICENSE` 都在，且没有 `ffmpeg.exe`
- [x] 3.6 `release` 用该目录打出 `Luma-<version>-win-x64-setup.exe`，执行 `gh release create --draft`，标题为 `Luma <tag>`，说明为 `Automatic release`，不标记 prerelease，并附上这个 exe。把 publish 目录上传为 artifact。读工作流确认草稿参数和安装包附件
- [x] 3.7 `windows-portable` 设置 `needs: release`，下载该 artifact，打出根目录就是 `Luma.exe` 的 `Luma-<version>-win-x64.zip`，再 `gh release upload <tag> <zip> --clobber`。确认这个作业不再次 fetch OBS 或 publish，并且 zip 根目录布局用本机目录抽查一次

## 4. 说明

- [x] 4.1 在 README 的构建一节补上：把 `Directory.Build.props` 的版本改成要发布的号，推送同名 `vX.Y.Z` 标签后，草稿 Release 里会有 zip 和 exe。对照中英文 README，两处都能看到这条说明
