# CodexQuotaFloat {{VERSION}}

Windows x64 的多账号 Codex 额度浮窗。

## 功能
- 5h、7天及左右双水位，三色账号进度线。
- 悬停逐账号详情、滚轮切换、点击全部列表、本地账号排序。
- 屏幕边缘停靠、置顶、多屏定位；跟随 Codex 并保存拖动后的相对位置。
- CPA 或本地 Codex 认证读取额度；Keeper 只读查询最近60秒 Token 流量。
- 本地认证兼容UTF8/BOM/UTF16；CLI使用UTF8协议，分别提示认证文件、额度响应和CLI协议错误。
- 支持在账号详情查看月周期及其他非标准窗口；不计入5h/7天水位。本地查询允许30秒等待，超时标明阶段。

## 下载
- `self-contained.zip`：包含 .NET 10 Desktop 运行时，无需另装 .NET。推荐普通用户使用。
- `framework-dependent.zip`：轻量包，需要 .NET 10 Desktop Runtime x64。
- `SHA256SUMS.txt`：两个ZIP的校验值。

完整解压后运行 `CodexQuotaFloat.exe`，不要单独移动 EXE。首次默认演示数据，在设置切换真实来源。更新前从托盘退出旧程序。

## 注意
Token/s 为最近60秒已完成请求Token总量÷60，不是正在生成的输出速度。密钥、Keeper密码不写入设置文件；重启后重新输入或使用环境变量。发布包未数字签名，Windows可能提示未知发布者；请核对下载来源与SHA256，不要全局关闭安全保护。

自动检查不等于真实CPA/Keeper认证联调或物理鼠标人工验收；本地认证额度接口及客户端窗口识别可能受上游更新影响。
