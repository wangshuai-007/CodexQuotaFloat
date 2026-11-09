# 项目上下文

## 基本信息
CodexQuotaFloat 是 Windows 桌面额度浮窗。

## 技术上下文
.NET 10、WPF、无依赖 Core 库、离线确定性测试及内置原生窗口诊断。

## 项目概述
src/QuotaFloat 为界面与窗口协调，src/QuotaFloat.Core 为额度/认证/流量逻辑，tests/QuotaFloat.Tests 为逻辑验证。

## 开发约定
沿用 DESIGN.md 和既有 plans/ 记录；凭据不写入知识库。构建后自动复制到 release，验证阶段可通过 SkipLocalTestReleaseCopy 保留当前运行版本。

## 当前约束
真实凭据和人工窗口交互验证分别记录，不以演示结果冒充真实数据验证。
