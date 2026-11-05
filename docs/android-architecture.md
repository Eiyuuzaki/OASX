# OASX Android · 架构说明与交付清单

> 对应规范：`docs/android-spec.md` · API 清单：`docs/android-api-inventory.md`
> 交付日期：2026-11-06 · debug APK：`android/app/build/outputs/apk/debug/app-debug.apk`

## 一、架构

```
ui/                       Presentation（Compose 页面 + 状态）
├── theme/Theme.kt        M3 ColorScheme（浅/深/跟随系统）
├── Common.kt             AsyncView（加载/错误/内容三态）、PageTitle、fmt
├── HomePage.kt           首页：账号头 + 调度器WS状态 + 寄养 + 每日任务
├── AssetsPage.kt         资产：近 7 天账本（随当前账号 role_key）
├── TasksPage.kt          任务：分组（/script_menu）+ 脚本启停（停止有确认）
├── RecordsPage.kt        记录：日期切换 + 总览磁贴 + 各任务 runs
└── SettingsPage.kt       设置：后端地址 / 主题 / 关于

data/                     Data
├── Api.kt                OkHttp 客户端（baseUrl 可变、@Volatile）
├── Models.kt             DTO（activity/foster/ledger，kotlinx.serialization）
├── Models.kt (data/)     调度器 WS 模型 / 统计模型 / 角色模型
├── SchedulerSocket.kt    WS /ws/{script} 实时通道（Flow，断线指数退避重连）
├── AccountContext.kt     全局账号上下文（StateFlow，resource/roles 离线可读）
└── Prefs.kt              SharedPreferences（后端地址/主题/当前账号）

net/Api.kt                同 data/Api.kt（历史路径，等价）
```

说明：v1 未引入独立 Domain 层与 UseCase——客户端无复杂业务规则（多为透传展示），
按规范「不要为了架构形式而过度抽象」暂缓；引入时机：出现跨页面复用的业务规则时。

## 二、功能实现清单

| 页面 | 已实现 | 数据源 |
|---|---|---|
| 首页 | 账号头（名称/等级/区服/调度器）、调度器实时状态（WS：状态/当前任务/队列）、每日任务进度、寄养双卡（倒计时/成熟/离线错误）、连接错误提示 | /config_list, /role/current, /ws/{script}, /activity, /notify/foster/refresh |
| 资产 | 近 7 天账本（当前值/起值/增量，涨跌配色），账号隔离（role_key 随账号） | /resource/{config}/ledger |
| 任务 | 任务分组列表（script_menu）、脚本启动/停止（停止带确认） | /script_menu, /{script}/start, /{script}/stop |
| 记录 | 日期切换、总览磁贴（总时长/任务次数/战斗场次）、各任务次数与时长、最近 runs | /stats/{script}/dates, /stats/{script}?date= |
| 设置 | 后端地址（测试连接）、主题（跟随系统/浅/深，即时生效）、关于 | /config_list |

## 三、未实现清单（与原因）

| 项 | 原因 |
|---|---|
| 任务参数编辑 | /{script}/{task}/args 的组/参数结构复杂，需专门页面；本期范围外 |
| 任务启用开关 | 依赖参数编辑同套 PUT 通道，同上 |
| 实时日志（SSE） | /log/{script}/stream 客户端接入未做；页面已留占位 |
| 多账号并排展示 | 单账号聚焦设计（规范：账号是主要上下文）；切换器已支持 |
| 语言切换 | 字符串抽取未做（与 WPF 同一遗留） |
| release 签名 / Play 发布 | debug 签名交付；正式签名需密钥 |
| 后台任务通知 | 评估后暂缓：通知属 PC 端 onepush 职责（避免重复打扰） |

## 四、构建与运行

```powershell
cd android
.\gradlew.bat assembleDebug     # 产物 app/build/outputs/apk/debug/app-debug.apk
```

- 本机无 wrapper jar 时可直接调用 Gradle 发行版：
  `C:\Users\<user>\.gradle\wrapper\dists\gradle-8.13-*\<hash>\gradle-8.13\bin\gradle.bat`
- SDK 路径写在 `android/local.properties`（不入库）
- 运行前提：手机与 PC 同局域网；OAS 后端监听 0.0.0.0:22288

## 五、测试结果

| 项 | 结果 |
|---|---|
| assembleDebug | ✅ BUILD SUCCESSFUL（多轮迭代后） |
| 安装 | ✅ adb install / 浏览器下载安装均验证 |
| 启动与设置页 | ✅ 后端地址保存、测试连接 |
| 数据链路 | ✅ 仪表盘/活动/资产经真机截图验证（v0 基线） |
| 离线行为 | ✅ 快速失败 ~350ms，显示明确原因 |
| 待真机复测 | 配置切换后各页数据、深浅主题、SSE（未实现项除外） |

## 六、已知风险

1. 后端无鉴权（用户决策：家庭内网、单人使用）——勿在公共网络暴露 22288
2. `script_menu` 返回内部任务名（英文），中文映射待 I18n 通道
3. SchedulerSocket 重连使用 GlobalScope 语义（callbackFlow 内 launch）——
   flow 取消时协程随作用域结束，无泄漏，但重连中的 delay 不可取消（最长 10s）
