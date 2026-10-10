# Codex 额度浮窗

Windows 10/11 原生 WPF 额度仪表，首版默认使用假数据，不自动连接 CPA。

## 启动

打开 `C:/Codes/CodexQuotaFloat/dist/CodexQuotaFloat.exe`。发布目录内的 DLL、JSON 必须与 exe 放在一起，不能只复制 exe。

本版为轻量框架依赖发布，要求 **.NET 10 Desktop Runtime（x64）**，当前开发机器已安装。没有该运行时的机器需从 [微软 .NET 10 下载页](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)安装 Desktop Runtime，或使用源码制作自包含发布。

## 操作

- 浮窗48×240逻辑像素，水柱宽32；移到水柱上仅显示宽304的单账号小卡。
- 在水柱上上下移动：查看对应横线的账号；滚轮切换上一个/下一个账号，超过10个时自动滚动横线视口。
- 点击水柱：打开并固定全部账号列表；再次点击收起。悬停和滚轮不会自动打开全部列表。
- 移出后延迟330ms收起小卡，允许跨越两窗间隙；全部列表可用“固定详情”控制停留。
- 拖动水柱：靠近屏幕工作区四边28逻辑像素内自动吸附，不压住任务栏。
- “置顶”开关：同时控制水柱、单账号小卡和全部列表；右键菜单也可设置。
- 设置：切换演示/CPA，调整0–100个演示账号、置顶、边缘停靠、减弱动效、刷新间隔。
- 每页最多10个账号，超过10个可翻页；屏幕较矮时详情列表可滚动。
- Enter / Space：打开全部列表；↑ / ↓：切换单账号；Escape：收起；F5：刷新；Ctrl+,：设置。
- 任务栏托盘：双击显示浮窗；右键可设置或退出。应用单实例运行。
- 关闭后记住位置和界面设置；不会写入管理密钥。托盘“显示浮窗”可在显示器变更后找回窗口。

## 三条线与水位

- 蓝色：**5小时剩余额度**，100%表示未使用。
- 绿色：**周剩余额度**，100%表示未使用。
- 橙色：**固定7天周期已走过的时间百分比**。公式：`clamp(1 - (周重置时间 - 当前时间)/7天, 0, 1)`，不是额度使用量。到期保持100%，等待刷新，不自动赠予新窗口额度。
- 水位：有效账号 `min(5h剩余, 周剩余)` 的等权平均。不同套餐没有可相加的 token 上限，因此不将该值称作 token 总量。
- 每个账号占一组横线，组内三色短线分别显示5h、周、7d；水柱同屏最多10组。超过10个账号可用滚轮查看后续账号，箭头提示视口外还有账号，刷新保留查看位置。
- 水位按全部有效账号计算，不受详情页码影响。
- 账号失败、缺少双窗口或重置已到期，不计入水位；详情保留说明。全部失败或无账号时显示“—”，不是0%或100%。
- 演示前三账号为100/88、100/89、80/30，总水位69%。邮箱为匿名假数据，重置时间相对当前时间生成；演示刷新会重建时间，不模拟真实消费。

## CPA 数据源

在设置选择“CPA 管理额度”，填写完整管理地址和管理密钥：

- 默认新版：`http://127.0.0.1:8317/v8/management`。
- 已有旧版/Plus分支：可显式填写 `http://127.0.0.1:8317/v0/management`；不会静默探测或回退。
- 远程地址必须为HTTPS；仅本机允许HTTP；禁用HTTP重定向，防止密钥被转发。
- 密钥仅保留本次会话；也可自行设置 `CPA_MANAGEMENT_KEY` 环境变量后启动。密钥不得写入代码或提交到版本库。
- 新版读取 `GET /credentials`，旧版读取 `GET /auth-files`，只选择启用的Codex账号。
- 通过管理服务的 `POST /requests/api-call`（新版）或 `POST /api-call`（旧版），由CPA使用其账号凭据代理查询Codex usage。浮窗不下载凭据、不读取本地OAuth token、不重置额度。
- 剩余窗口按 `limit_window_seconds` 识别5h/周，将 `used_percent` 转为剩余值。缺失字段不默认满额。
- 默认每60秒刷新，最多3个并发，单请求12秒超时，响应上限2MiB。错误保留旧快照并标记，不转成演示；鉴权失败暂停自动刷新，修正密钥后手动刷新。
- 如果同一CPA分支只提供自定义“额度统计缓存”而未支持上述管理接口，需要单独对接它的实际JSON字段。当前实现不是读取请求token统计队列；那类统计无法推算订阅剩余额度。
- **真实CPA未联调**：用户没有提供地址、密钥或原始JSON。已完成离线请求/响应验证，不能据此声称真实账号连接成功。

接口依据：[CPA v8管理接口](https://help.router-for.me/management/apiv8)、[CPA api-call上游实现](https://github.com/router-for-me/CLIProxyAPI/blob/main/internal/api/handlers/management/api_tools.go)。平台依据：[微软WPF概述](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)。

## 构建和验证

从 `C:/Codes/CodexQuotaFloat` 执行：

```powershell
dotnet build "src/QuotaFloat/QuotaFloat.csproj" -c Release
dotnet run --project "tests/QuotaFloat.Tests/QuotaFloat.Tests.csproj"
dotnet publish "src/QuotaFloat/QuotaFloat.csproj" -c Release --self-contained false -o "dist"
& "dist/CodexQuotaFloat.exe" --render-preview "artifacts"
```

`--demo` 可在启动时强制演示模式，不访问网络。`--render-preview` 使用真实WPF组件离线生成PNG及UI验证记录，不读取本地设置、不接入CPA，也不打开浮窗。

自动验证包含82项额度/CPA/账号视口逻辑检查、23项WPF布局/几何检查、当前双屏环境36项真实Windows窗口冒烟检查（其中16项为多屏回归，数量随显示器数量变化）。原生冒烟检查已验证窄浮窗尺寸、上下位置切换单账号、高精度滚轮累积、超过10个账号滚动、刷新保持位置、点击才打开全部列表、Owner关系、Windows置顶标志开关、右边停靠、向内展开与Escape收起。已用实际exe验证主屏150%、副屏100%缩放：两屏左右下角悬停/点击、面板跨屏跟随均通过。真实物理鼠标跨窗移动/拖动仍需人工验收，事件模拟不能替代物理鼠标操作。

原生冒烟检查应运行带PerMonitorV2清单的exe：`Start-Process -FilePath "dist/CodexQuotaFloat.exe" -ArgumentList '--smoke-test "artifacts/native-final"' -WindowStyle Hidden -Wait`。不要用dotnet加载DLL验证混合DPI，因为宿主进程清单不同。该模式短暂创建真实窗口，不保存设置，不接入CPA，不操作系统鼠标。

设置位置：`%LOCALAPPDATA%/CodexQuotaFloat/settings.json`。只包含本地界面选项，不包含密钥和账号快照。
