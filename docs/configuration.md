# 数据源配置与排错

Codex Hook 关联启动不属于数据源配置；开启、信任、首次消息触发及排错见 [Hook 关联启动图文指南](codex-hook.md)。

## CPA额度

新版管理根地址例：`https://cpa.example.com/v8/management`；本机默认 `http://127.0.0.1:8317/v8/management`。旧版支持显式 `/v0/management`，不自动探测回退。

新版通过 `GET /credentials` 筛选启用Codex账号，管理代理 `POST /requests/api-call` 查询上游usage；旧版使用 `/auth-files`、`/api-call`。按窗口秒数识别5h/周，将已使用比例转为剩余比例。并发最多3个，每个请求12秒超时；大量账号不建议5秒轮询。

密钥可在设置输入或使用 `CPA_MANAGEMENT_KEY`。读取进程→Windows用户→系统环境变量。双击缺少密钥时检查是否写入Windows用户变量，而非仅某个PowerShell进程。不要在问题报告中粘贴密钥。

- 401/403：核对CPA管理密钥及管理接口权限，修正后F5。
- 404：核对版本、根路径与反向代理转发，不能把网页URL当作管理API根地址。
- 某账号失败：保留旧值并标记状态，不以演示或满额替代。

依据：[CPA管理接口](https://help.router-for.me/management/apiv8)。

## 本地认证

路径留空优先使用PATH中的原生 `codex.exe app-server`，其次查找Windows Codex应用CLI位置。只执行初始化、`account/read` 和 `account/rateLimits/read`，不启动推理；CLI自行维护认证缓存。不执行codex.cmd等shell脚本。

无CLI时尝试 `CODEX_HOME/auth.json` 或 `%USERPROFILE%/.codex/auth.json`。手动路径只读文件，不复制、刷新或改写令牌。API Key模式没有订阅限额，必须是ChatGPT账号登录。换号后刷新；认证过期应在Codex更新登录。本地只显示一个账号，流量需另接Keeper。

- 桌面客户端能显示额度，不代表默认CLI或所选文件使用同一份ChatGPT登录。提示API Key时，应选择实际的ChatGPT `auth.json`，不要把API Key转换或伪装成OAuth令牌；不要覆盖正在使用的CPA认证。
- 文件模式兼容UTF8（有/无BOM）及带BOM的UTF16。认证文件解析失败、额度接口返回网页/无效JSON、CLI协议失败分别提示；不会输出文件、令牌或响应正文。
- 官方支持文件、系统凭据库及仅进程内认证。凭据库或进程内认证不一定有可用的 `auth.json`；本工具不会读取桌面客户端进程内存或导出系统凭据。路径留空的CLI模式仍须使用有效ChatGPT登录。
- 月周期或其他非5h/7天窗口按服务实际时长显示在账号列表/悬停详情；不折算成5h/周，也不计入双窗口水位。没有5h/7天窗口时水位保持“—”，不伪造百分比。
- 本地CLI及文件模式最多等待30秒；CLI超时会标明启动、账号读取或额度读取阶段。文件模式连接超时检查网络/代理；CPA查询超时不受本地设置影响。失败保留旧快照并标记，旧值不是当前额度。

依据：[Codex认证](https://developers.openai.com/codex/auth/)、[App Server](https://developers.openai.com/codex/app-server/)。

## Keeper

独立部署 [CPA Usage Keeper](https://github.com/Willxup/cpa-usage-keeper)，确认浏览器能打开其应用首页，再把**应用根URL**填写到Keeper地址，例如 `https://stats.example.com/keeper/`。不要填写CPA管理根路径、插件菜单路径或 `/api/v1/...`。

程序查询 `GET /api/v1/auth/session`，必要时以Keeper密码调用 `POST /api/v1/auth/login`，随后每秒只读查询 `/api/v1/usage/events?range=5h&page_size=100&cursor_mode=true`。查询范围5h，但显示只统计最近60秒。事件ID去重，只使用时间和Token统计，不读取请求正文。

密码可用 `KEEPER_LOGIN_PASSWORD`（进程→用户→系统）；不复用CPA密钥、不落盘。默认HTTPS，本机HTTP允许，私有IP的HTTP需手动勾选允许局域网HTTP；公网HTTP、URL内嵌凭据、重定向及跳过证书验证均不支持。

- 400：检查Keeper版本及事件查询接口是否支持range=5h和游标分页。
- 401/403：检查Keeper自己的密码；修正后保存或F5。
- 404：检查应用根URL、部署子路径与代理配置，而非不断更换密码。
- 近期无记录：平均流量显示“—”，不是数据接入失败；悬停看截至时间及提示。
- 流量滞后：只统计已完成请求，受Keeper采集延迟及时间偏差影响，不能据此判断正在生成的速度。

401/403/404会暂停自动查询；整轮4秒超时，分页或返回超限不使用部分数据冒充完整统计。本工具不控制Keeper的采集方式，部署者仍应确认Keeper不会回退消费CPA队列。

## 同源只读快照

旧版CPA分支仍有 `GET /usage` 时可留空快照地址；新版v8未配置只读快照显示流量未接入。也可提供**与CPA同域名、同端口**的只读统计快照URL，由反向代理提供，不跨源转发密钥。

绝不访问 `/usage-queue` 或 `/observability/usage/queue` 等读取后删除记录的接口，不回退消费队列，不创建或改写统计服务。

快照至少包含最近60秒已完成请求，可返回数组或：

```json
{"records":[{"request_id":"unique-id","timestamp":"2026-10-12T12:00:00+08:00","tokens":{"total_tokens":1234}}]}
```

稳定唯一请求ID可可靠去重，无ID则兼容摘要与出现次数；服务需足够及时，服务缓存5秒时每秒轮询也不能取得逐秒原始事件。仅内存保留必要统计，不保存密钥、邮箱、输入或响应正文。
