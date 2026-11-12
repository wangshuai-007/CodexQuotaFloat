# 构建与 GitHub Release

在Windows及.NET 10 SDK环境，从仓库根目录执行：

```powershell
dotnet run --project "tests/QuotaFloat.Tests/QuotaFloat.Tests.csproj" -c Release
& "scripts/build-local.ps1"
& "scripts/publish-release.ps1" -Version "0.1.0"
& "scripts/verify-release-assets.ps1" -Version "0.1.0"
```

版本须为主.次.修订，可带预发布后缀，如 `0.1.0-beta.1`。每次使用独立暂存目录，避免混入旧输出、用户配置或测试日志，不覆盖本地release目录。

`artifacts/github-release/<版本>/` 中包含：
- `CodexQuotaFloat-<版本>-win-x64-self-contained.zip`：包含.NET 10及Windows Desktop运行时。
- `CodexQuotaFloat-<版本>-win-x64-framework-dependent.zip`：需要.NET 10 Desktop Runtime x64。
- `SHA256SUMS.txt`：两个ZIP的SHA256。
- `release-notes.md`：该版本发布说明。

包内包含README、配置说明及匿名截图，自包含包还附带运行时许可证及第三方声明，不含PDB、用户设置、凭据或开发工作区记录。自包含是完整目录，不是单文件EXE。未数字签名；发布前应在干净Windows环境启动测试，并确认安全提示。自包含运行时的安全更新需要重新构建发布包。

## GitHub工作流

`.github/workflows/release.yml` 在Windows runner执行逻辑测试、应用构建及离线WPF检查，然后生成两种部署包。

- **手动运行**：Actions → Windows Release → Run workflow，输入不带v的版本，只生成可下载Actions产物，不创建远端Release。
- **推送v开头标签**：验证、打包，创建GitHub Release **草稿**，附两个ZIP、SHA256和发布说明。维护者检查草稿后手动发布。
- 重跑只更新草稿，已有正式Release时拒绝覆盖。工作流不创建或推送Git标签。
- 使用GitHub自动提供的GITHUB_TOKEN，不需要CPA/Keeper密钥；仅草稿发布任务有contents: write权限。

普通代码push不会发布新版本。当前项目整理不等于已经推送代码或创建GitHub Release。

## 发布检查

- [ ] 版本、说明、截图无真实账号或机器专属路径。
- [ ] 测试通过，完整解压的自包含包可启动。
- [ ] 未安装.NET的干净Windows环境人工测试。
- [ ] SHA256与附件一致，无密码、auth.json、.env或本地配置。
- [ ] 核验草稿后正式发布。

依据：[微软.NET部署方式](https://learn.microsoft.com/en-us/dotnet/core/deploying/)、[GitHub发布管理](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)。
## 当前用户安装包与自动更新（1.1.0起）

仅GitHub托管runner会在工作流内从官方GitHub固定Release下载6.7.3，验证固定SHA256及Pyrsys B.V.有效签名后安装到runner临时目录，通过`ISCC_EXE`传给打包脚本。不依赖Chocolatey，也不提供或在本机运行编译器安装脚本。版本由固定安装包的SHA256及签名确认；ISCC.exe的PE ProductVersion/FileVersion可能为0.0.0.0，不以该字段判断安装版本。本地打包只使用已手动安装的编译器，支持`ISCC_EXE`、PATH、Program Files及当前用户Programs目录。旧标签任务重跑仍使用旧工作流，需从包含本修复的新提交手动运行Actions，或创建新版本标签；不自动移动已有标签。自包含输出同时编译EXE安装向导，SHA256SUMS同时包含两个ZIP与setup.exe。固定AppId用于升级，禁止每版本更换。

上传正式Release时需包含 `CodexQuotaFloat-<版本>-win-x64-setup.exe`；版本标签必须为`v主.次.修订`。只有公开正式版参与更新，草稿和预览版不参与；GitHub API需返回安装资产的`digest: sha256:...`，否则程序拒绝自动下载。工作流仍只创建草稿，需维护者检查后手动发布。统一通过Version参数生成程序集和安装包版本。

打包验证不等于真实安装验证；正式发布前请人工验收首次安装、覆盖升级、关闭占用、取消安装、卸载及设置保留，并确认Hook路径迁移审核。
