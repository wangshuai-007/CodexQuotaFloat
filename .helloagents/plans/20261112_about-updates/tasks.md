# 关于与更新 — 任务分解

## 拆分原则
按可验收行为拆分。
## 任务列表
- [√] AFK 关于页面：版本、GitHub、更新入口，离线预览验证。
- [√] AFK 检查与受限下载：正式版比较、SHA256、主机限定及取消测试。
- [√] AFK 当前用户安装包与Release：生成EXE及ZIP，产物哈希与预览验证。
- [ ] HITL 实际安装、覆盖升级及卸载人工验收；不替用户安装正式版。
## Codex /goal 执行入口
按本目录requirements.md、plan.md、contract.json执行；/goal -> ~auto -> ~qa，独立完成AFK，HITL不自动替用户安装。
## 进度
AFK实现完成，365项验证、安装包编译、哈希和两个ZIP离线预览通过；HITL保留明确人工验收边界。