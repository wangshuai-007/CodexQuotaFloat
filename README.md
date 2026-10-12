# Codex 额度浮窗

Windows 10/11 原生 WPF 额度仪表，首版默认使用假数据，不自动连接 CPA。

## 启动

本地测试统一打开 `C:/Codes/CodexQuotaFloat/release/CodexQuotaFloat.exe`。每次应用项目build成功后，完整构建输出会自动复制到该目录（Debug/Release均支持，设计器构建除外）。先退出旧浮窗再启动新版；文件被占用导致复制失败时，构建会报错，不会假装测试目录已更新。发布目录内的 DLL、JSON 必须与 exe 放在一起，不能只复制 exe。

本版为轻量框架依赖发布，要求 **.NET 10 Desktop Runtime（x64）**，当前开发机器已安装。没有该运行时的机器需从 [微软 .NET 10 下载页](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)安装 Desktop Runtime，或使用源码制作自包含发布。

## 操作

- 浮窗48×296逻辑像素，水柱宽32；移到水柱上仅显示宽304的单账号小卡。
- 在水柱上上下移动：查看对应横线的账号；滚轮切换上一个/下一个账号，超过10个时自动滚动横线视口。
- 点击水柱：打开并固定全部账号列表；再次点击收起。悬停和滚轮不会自动打开全部列表。
- 移出后延迟330ms收起小卡，允许跨越两窗间隙；全部列表可用“固定详情”控制停留。
- 拖动水柱：靠近屏幕工作区四边28逻辑像素内自动吸附，不压住任务栏。
- “置顶”开关：同时控制水柱、单账号小卡和全部列表；右键菜单也可设置。
- 设置：切换演示/CPA，调整0–100个演示账号、置顶、边缘停靠、减弱动效、刷新间隔。
- 每页最多10个账号，超过10个可翻页；屏幕较矮时详情列表可滚动。
- 账号行悬停或聚焦时显示“置顶、最后、↕”；按住↕上下拖动，松开完成排序，提示目标位置。Alt+↑/↓逐行移动。排序只保存在本地，不改变CPA调度；水柱同步顺序，刷新和重启保留，新账号追加。
- 底部仅突出显示 **Token/s**，移除请求/s及累计请求次数，流量固定每秒独立GET取数，不受额度5–3600秒同步间隔影响；详情保留最近60秒累计和12格趋势（每格5秒）。Token速率=两次成功快照之间新增去重记录的Token÷实际采样秒数，首次先建立基线，慢请求不重叠，超过3秒未更新显示未知。演示明确标注假流量；不是回答生成速度或网络字节数。exe、窗口和托盘使用同一水柱图标。
- Enter / Space：打开全部列表；↑ / ↓：切换单账号；Escape：收起；F5：刷新；Ctrl+,：设置。
- 任务栏托盘：双击显示浮窗；右键可设置或退出。应用单实例运行。
- 关闭后记住位置和界面设置；不会写入管理密钥。托盘“显示浮窗”可在显示器变更后找回窗口。

## 三条线与水位

- 蓝色：**5小时剩余额度**，100%表示未使用。
- 绿色：**周剩余额度**，100%表示未使用。
- 橙色：**固定7天周期已走过的时间百分比**。公式：`clamp(1 - (周重置时间 - 当前时间)/7天, 0, 1)`，不是额度使用量。到期保持100%，等待刷新，不自动赠予新窗口额度。
- 设置中的“浮窗总额度水位”支持：**5h剩余**、**7天剩余**、**左右双水位（左5h蓝色、右7天绿色）**。默认5h，选择保存在本地，保存后立即应用；详情总体数值同步口径。
- 水位：所选窗口有效账号剩余百分比的等权平均。双水位分别计算两个窗口；不同套餐没有可相加的Token上限，不将该值称作Token绝对总量。7天水位是剩余额度，橙色线仍是已走过的时间，二者不同。
- 每个账号占一组横线，组内三色短线分别显示5h、周、7d；水柱同屏最多10组。超过10个账号可用滚轮查看后续账号，箭头提示视口外还有账号，刷新保留查看位置。
- 水位按全部有效账号计算，不受详情页码影响。
- 账号失败或所选窗口缺失、非有限值、重置已到期时，不计入对应水位；另一窗口仍独立计算。无有效数据的一侧显示“—”和空水柱，不是0%或100%。
- 演示前三账号为100/88、100/89、80/30，5h水位约93%，7天水位69%。邮箱为匿名假数据，重置时间相对当前时间生成；演示刷新会重建时间，不模拟真实消费。

## CPA 数据源

在设置选择“CPA 管理额度”，填写完整管理地址和管理密钥：

- 默认新版：`http://127.0.0.1:8317/v8/management`。
- 已有旧版/Plus分支：可显式填写 `http://127.0.0.1:8317/v0/management`；不会静默探测或回退。
- 远程地址必须为HTTPS；仅本机允许HTTP；禁用HTTP重定向，防止密钥被转发。
- 密钥仅保留本次会话；也可设置 `CPA_MANAGEMENT_KEY`。启动读取优先级为进程→Windows用户→系统环境变量，双击启动不依赖资源管理器刷新环境；当前会话密钥为空时重新打开设置也会再次读取。密钥不得写入代码或提交到版本库。
- 新版读取 `GET /credentials`，旧版读取 `GET /auth-files`，只选择启用的Codex账号。
- 通过管理服务的 `POST /requests/api-call`（新版）或 `POST /api-call`（旧版），由CPA使用其账号凭据代理查询Codex usage。浮窗不下载凭据、不读取本地OAuth token、不重置额度。
- 剩余窗口按 `limit_window_seconds` 识别5h/周，将 `used_percent` 转为剩余值。缺失字段不默认满额。
- 额度默认每60秒刷新，设置允许5–3600秒。额度最多3个并发、单请求12秒超时；流量固定每1秒独立取快照、4秒超时，慢请求跳过重叠轮询、按真实采样间隔计算速率；响应上限2MiB。错误保留旧额度快照并标记，不转成演示；鉴权失败暂停自动刷新，修正密钥后手动刷新。大量账号不建议5秒额度轮询，上一轮未结束时跳过重叠刷新。
- 如果同一CPA分支只提供自定义“额度统计缓存”而未支持上述管理接口，需要单独对接它的实际JSON字段。当前实现不是读取请求token统计队列；那类统计无法推算订阅剩余额度。
- **真实CPA未联调**：用户没有提供地址、密钥或原始JSON。已完成离线请求/响应验证，不能据此声称真实账号连接成功。

接口依据：[CPA v8管理接口](https://help.router-for.me/management/apiv8)、[CPA api-call上游实现](https://github.com/router-for-me/CLIProxyAPI/blob/main/internal/api/handlers/management/api_tools.go)。平台依据：[微软WPF概述](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)。

## 本地 Codex 认证

设置 → 数据源 → **本地 Codex 认证（ChatGPT登录）**，不需要CPA地址或管理密钥。

- 认证文件留空：优先调用已安装的原生`codex.exe app-server`，只执行`account/read`和`account/rateLimits/read`，复用CLI当前登录，包括文件或系统keyring认证；本工具不直接提取系统令牌、不启动推理。CLI按自己的规则维护登录缓存。
- 自动查找PATH中的`codex.exe`，其次仅查找Windows Codex应用的`%LOCALAPPDATA%/OpenAI/Codex/bin`。不执行`codex.cmd`或其它shell脚本；无原生CLI时尝试默认认证文件。
- 手动选择JSON文件：只读该文件，不调用CLI、不复制或改写认证、不刷新OAuth令牌。默认文件为`CODEX_HOME/auth.json`，未设置时为`%USERPROFILE%/.codex/auth.json`；只读取ChatGPT登录的访问令牌及账号标识，固定HTTPS请求官方额度地址，不把凭据发往CPA。
- 仅显示当前本地登录的一个账号，不与CPA账号混合。来源标为“本地Codex”。API Key模式没有订阅5h/周额度，明确提示，不显示满额或假数据。
- 文件模式每次刷新重读认证，Codex换号或更新令牌后刷新即可生效；文件令牌失效须在Codex更新登录。CLI模式复用隐藏连接，避免5秒轮询反复启动进程，最多1分钟更新连接；F5/手动刷新立即重连。切换数据源和退出只关闭本工具创建的CLI进程，不影响正在使用的Codex。
- 两种本地方式都不提供最近60秒Token及请求次数，流量明确显示不可用，不能用额度百分比推算请求流量。
- 设置仅保存方式和文件路径，不保存访问/刷新令牌。不要上传、粘贴或提交`auth.json`。文件直连使用Codex内部额度接口，非OpenAI Platform公开计费接口，可能随Codex版本变化；官方CLI协议是优先方式。
- 当前本机实测：默认目录无auth.json；官方CLI能完成初始化/账号读取，但不是ChatGPT登录，不能声称已取得真实订阅额度。选择已有有效ChatGPT认证或在Codex以ChatGPT登录后再刷新。

官方说明：[Codex认证缓存及存储](https://learn.chatgpt.com/docs/auth)、[官方App Server账号/额度接口](https://learn.chatgpt.com/docs/app-server)。

## 只读实时流量

### 本地构建与自动重启

本地构建使用 `scripts/build-local.ps1`：检测到本项目测试程序运行时，先结束它释放文件，构建并校验release复制成功后重新启动；不结束CPA、Keeper或其他目录的同名程序。构建失败不启动不完整版本。Keeper密码不落盘，重启后需重新输入或使用 `KEEPER_LOGIN_PASSWORD`。

### CPA Usage Keeper

鼠标移到浮窗底部Token/s或趋势区域，会显示最近60秒Token总量与12格趋势（每格5秒），同时标明来源、截至时间及查询错误；移回账号区恢复额度小卡。仅复用已有每秒快照，不额外请求或显示请求次数。

设置中勾选“使用 CPA Usage Keeper 查询Token统计”，填写**Keeper应用根地址**（不是CPA管理地址，也不是插件菜单地址）和Keeper自己的登录密码。支持部署子路径，例如`https://stats.example.com/keeper/`。不要将根地址填入旧的“只读流量快照地址”。

- Keeper与额度来源独立，可搭配CPA额度或本地Codex额度；演示模式不访问Keeper。
- 密码仅会话内存保存，不写入settings.json，也可由`KEEPER_LOGIN_PASSWORD`提供（进程→Windows用户→系统）；不会复用或转发`CPA_MANAGEMENT_KEY`。重启后需重新输入密码或提供环境变量。
- 默认HTTPS；本机回环可HTTP。私有IP局域网HTTP须明确勾选“允许局域网HTTP”，密码和Cookie会明文传输，仅在可信网络使用。公网HTTP、地址内嵌密码/查询参数/片段、HTTP重定向均拒绝，不跳过证书验证。
- 首次查询读取`GET /api/v1/auth/session`；需要认证时仅执行`POST /api/v1/auth/login`建立独立Cookie会话，后续只读`GET /api/v1/usage/events?range=5h&page_size=100&cursor_mode=true`。Keeper小时范围最低5h，工具仍仅累计最近60秒。不查询请求正文、下载日志，不操作CPA/Keeper配置或队列。
- 用Keeper稳定事件ID去重，映射`timestamp`和`tokens.total_tokens`，保持每秒独立取数、60秒Token累计和趋势。Token/s是新增已完成记录用量除以真实采样间隔，不是流式输出速度，暂未使用Keeper的`speed_tps`字段。
- 游标分页最多200页、返回总量4MiB、整轮4秒；不完整分页/失败/超限不使用部分数据冒充完整统计。401/403/404暂停自动查询，修正设置或F5后重试，不持续尝试错误密码。Keeper自身采集延迟、请求时间口径及两端时间偏差会影响实时性。
- 联调边界：已确认用户部署的登录状态接口可达且需要登录，用量接口未登录返回401；没有获取用户密码，真实认证后的Token数据尚未验证。离线Cookie、子路径、分页、去重、错误及安全检查已通过。

依据：[Keeper官方认证实现](https://github.com/Willxup/cpa-usage-keeper/blob/main/internal/api/auth.go)、[只读事件接口](https://github.com/Willxup/cpa-usage-keeper/blob/main/internal/api/usage_events.go)。本工具不控制Keeper采集方式，部署者仍需确认它不回退消费CPA队列。

### 原有CPA只读快照

严格不访问`/usage-queue`或`/observability/usage/queue`：这类接口读取后移除记录。v8未配置只读地址时直接显示“流量未接入”；当前官方v8无内置最近Token只读快照，不能仅凭管理密钥启用该功能。

提供旧版`GET /usage`的分支可留空；已移除此接口的版本返回404后暂停流量自动刷新，并显示失败，不回退队列。新版需要已有统计服务的**同源只读快照地址**（可通过CPA所在服务的反向代理提供）。地址不得包含密钥参数，不能跨源转发管理密钥；拒绝队列/重置/配置/凭据路径，不跟随重定向。不会自动创建统计服务或改CPA配置。

快照返回最近至少60秒的已完成请求，允许顶层数组或如下格式。请求ID应稳定且唯一，Token使用统计服务的`total_tokens`口径，统计CPA全局而非仅Codex账号。兼容旧版`usage.apis.*.models.*.details`。明细缺失、格式错误或接口不可用显示“—”，不伪造零，只有有效空快照显示0。每秒GET真实取数，先建立基线后统计新增记录速率，并保留最近60秒累计；详情提示截至时间和真实采样间隔。快照服务若自身5秒缓存或批量汇报，请求每秒也不能获得逐秒原始事件，需服务提供足够及时的只读快照。迟到/补报记录会影响瞬时速率，不能把此指标当作生成中的Token速度。

```json
{"records":[{"request_id":"unique-id","timestamp":"2026-10-10T17:00:00+08:00","tokens":{"total_tokens":1234}}]}
```

按请求ID去重；没有ID时使用记录摘要与相同记录出现次数兼容，旧快照字段变化可能影响去重，稳定ID更可靠。仅内存保留时间、Token和ID，不存API密钥、邮箱、输入或响应正文；流量窗口最多20000条。演示流量只用于效果测试。

## 构建和验证

从 `C:/Codes/CodexQuotaFloat` 执行：

```powershell
dotnet build "src/QuotaFloat/QuotaFloat.csproj" -c Release
dotnet run --project "tests/QuotaFloat.Tests/QuotaFloat.Tests.csproj"
& "scripts/verify-release-copy.ps1" -Configuration "Release"
& "release/CodexQuotaFloat.exe" --render-preview "artifacts"
```

`--demo` 可在启动时强制演示模式，不访问网络。`--render-preview` 使用真实WPF组件离线生成PNG及UI验证记录，不读取本地设置、不接入CPA，也不打开浮窗。

自动验证包含192项额度/CPA/本地认证/CLI协议/流量/排序/视口/密钥逻辑检查、39项WPF布局/几何检查、当前双屏环境54项真实Windows窗口冒烟检查（其中16项为多屏回归，数量随显示器数量变化）。原生冒烟检查已验证窄浮窗尺寸、上下位置切换单账号、高精度滚轮累积、超过10个账号滚动、刷新保持位置、点击才打开全部列表、Owner关系、Windows置顶标志开关、右边停靠、向内展开与Escape收起。已用实际exe验证主屏150%、副屏100%缩放：两屏左右下角悬停/点击、面板跨屏跟随均通过。真实物理鼠标跨窗移动/拖动仍需人工验收，事件模拟不能替代物理鼠标操作。

原生冒烟检查应运行带PerMonitorV2清单的exe：`Start-Process -FilePath "release/CodexQuotaFloat.exe" -ArgumentList '--smoke-test "artifacts/native-final"' -WindowStyle Hidden -Wait`。不要用dotnet加载DLL验证混合DPI，因为宿主进程清单不同。该模式短暂创建真实窗口，不保存设置，不接入CPA，不操作系统鼠标。

设置位置：`%LOCALAPPDATA%/CodexQuotaFloat/settings.json`。只包含本地界面选项，不包含密钥和账号快照。
