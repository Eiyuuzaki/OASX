# OASX Android · API 对接清单

> 后端：OAS FastAPI，`http://<PC内网IP>:22288`，无认证（家庭内网）。
> 契约来源：`docs/openapi.json`（87 条路由），本清单只列 Android 客户端用到的子集。
> 生成日期：2026-11-06，对应后端 working copy。

## 全局约定

- 所有响应为 JSON；部分端点有 `success: bool` + `error: string` 包装
- 错误形态：FastAPI `{"detail": ...}`（4xx/5xx）或业务 `{"success": false, "error": ...}`
- 实时通道：WebSocket `/ws/{script}`（调度器状态，REST `/state` 是占位假实现，禁止使用）
- 明文 HTTP：Manifest 已开 `usesCleartextTraffic`

## 页面 → 接口映射

### 首页（Dashboard）

| 接口 | 方法 | 用途 |
|---|---|---|
| `/config_list` | GET | 配置（调度器）列表 → 推导账号列表 |
| `/role/current/{config}` | GET | 当前角色（name/level/server_show_name/role_key）|
| `/ws/{script}` | WS | 调度器实时状态：`{"state"}` / `{"schedule":{running,pending,waiting}}` |
| `/notify/foster/refresh` | GET/POST | 寄养槽+自结界卡（双槽位，含 card_name/remaining）|
| `/activity/{config}?force=1` | GET | 每日任务 done/total、悬赏、当前活动、时段 |
| `/device/current` | GET | 模拟器连接状态 |

### 资产（Assets）

| 接口 | 方法 | 用途 |
|---|---|---|
| `/resource/{config}/ledger?days=7` | GET | 区间账本（name/count/first_count/delta）|
| `/resource/{config}/dates` | GET | 有数据的日期（快照模式用）|
| `/resource/{config}?date=` | GET | 单日快照（name/icon/count/delta）|

### 任务（Tasks）

| 接口 | 方法 | 用途 |
|---|---|---|
| `/script_menu` | GET | 任务分组结构 |
| `/{script}/config_all` 或 `/config` | GET | 任务启停/调度配置（读取）|
| `/{script}/{task}/{group}/{argument}/value` | PUT | 修改任务参数 |
| `/{script}/start` / `/{script}/stop` | GET | 立即执行 / 停止 |
| `/{script}/state` | ⛔ | **占位假实现（"Hello, SSE!"），禁用** |

### 记录（Records）

| 接口 | 方法 | 用途 |
|---|---|---|
| `/stats/{script}/dates` | GET | 有统计的日期 |
| `/stats/{script}?date=` | GET | 总时长/总次数 + 各任务 runs[]（start/end/duration/battle）|
| `/log/{script}/stream` | SSE | 实时日志（后续版本）|
| `/log/errors` | GET | 错误日志列表（后续版本）|

### 设置（Settings）

| 接口 | 方法 | 用途 |
|---|---|---|
| `/notify/providers` | GET | 渠道与参数 schema |
| `/notify/config` | GET/PUT | 通知配置读取/保存（YAML）|
| `/notify/test` | POST | 测试发送 |
| `/home/update_info` | GET | 更新检查 |
| `/device/instances` / `/device/current` | GET/PUT | 模拟器选择 |

## 已知边界

1. **调度器状态只能走 WS**——REST state 是假实现。
2. **配置即调度器**：每个 config 对应一个调度器，其当前角色即该调度器下登录的游戏账号；账号列表 = 各 config 的 current role 扁平化（与 WPF GlobalAccountContext 同口径）。
3. `/resource/*` 与 `/activity` 均以 config 定位；切换 config 不影响其它 config 的调度器状态。
4. `/stats/{script}?date=` 的 date 为必填 Query（alias=date）。
5. 未认证：家庭内网假设，客户端不做鉴权（用户决策 2026-11-06）。