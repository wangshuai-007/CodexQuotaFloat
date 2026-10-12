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
