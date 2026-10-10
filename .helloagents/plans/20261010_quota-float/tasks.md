# Codex 额度浮窗 — 任务分解

## 拆分原则
端到端交付可验证行为；AFK 可独立执行，HITL 只用于必要外部输入。

## 任务列表
- [√] 任务1（AFK）：水位和7天时间进度，涉及 Core 与 tests；完成标准：计算边界和三组示例测试通过；验证：dotnet run --project tests/QuotaFloat.Tests。
- [√] 任务2（AFK）：窄浮窗三色线、上下移动单账号小卡、滚轮视口与点击全部列表，涉及 Gauge/DetailWindow；完成标准：原生渲染与分页验证通过；验证：--render-preview。
- [√] 任务3（AFK）：停靠、置顶、设置持久化，涉及 MainWindow/ScreenPlacement/Settings；完成标准：无警告构建、逻辑与程序内窗口冒烟检查通过。
- [√] 任务4（AFK）：CPA管理数据源与失败处理；完成标准：离线模拟请求、解析和失败路径通过，演示明确标注；验证：Core 测试。
- [√] 任务5（AFK）：发布程序、README、QA证据；完成标准：发布与离线测试成功，真实CPA未联调与实屏限制明确。

## Codex /goal 执行入口
按 C:/Codes/CodexQuotaFloat/.helloagents/plans/20261010_quota-float/tasks.md 执行，遵守 requirements.md、plan.md、contract.json；/goal -> ~auto -> ~qa。真实 CPA 凭据仅在用户主动切换数据源时输入，当前演示交付不依赖该输入。

## 进度
交互修订已实现。90项Core/CPA/视口/密钥读取逻辑检查、23项WPF布局/几何检查、36项真实Windows窗口事件检查通过，包括主屏150%与副屏100%的16项双屏回归。主浮窗48×240，单账号小卡304宽，滚轮可访问第11个及后续账号，点击才展示全部。真实CPA未联调，物理鼠标拖动未人工验收；混合DPI已用实际双屏exe验证窗口事件路径。
