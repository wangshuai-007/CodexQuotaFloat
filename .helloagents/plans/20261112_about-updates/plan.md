# 关于与更新 — 实施规划

## 目标与范围
版本1.1.0，增加关于与更新菜单及安装向导。
## 架构与实现策略
GitHubUpdates负责只读Release与受限下载；AboutWindow协调交互，ApplicationUpdates启动检查。Inno Setup复用同一AppId覆盖安装。
## 领域语言
自动更新指自动检查、用户确认下载并打开安装向导，不是后台强制替换。
## 完成定义
qaMode standard，qaFocus 来源与完整性校验、旧版本保留、发布产物兼容、UI可达。
## 文件结构
src/QuotaFloat.Core/GitHubUpdates.cs；src/QuotaFloat/AboutWindow.cs；installer/CodexQuotaFloat.iss；scripts/build-installer.ps1及发布工作流。
## UI / 设计约束
沿用既有Surface、字体、AppIcon，紧凑独立窗口；检查/可更新/下载/失败/取消覆盖；不抢焦点自动弹窗。
## 风险与验证
校验GitHub SHA256但未数字签名，不宣称发布者签名认证。仅正式版，缺包/摘要则拒绝。便携转安装保留设置但旧钩子需程序迁移后重新审核。安装覆盖不等于事务性回滚；不自动运行真实升级验收，交用户安装测试。
## 决策记录
- 2026-11-12 用户选择1：当前用户EXE安装，无需管理员权限。
