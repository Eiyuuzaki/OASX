# OASX Android · 架构说明与交付清单

> 对应规范：`docs/android-spec.md` · API 清单：`docs/android-api-inventory.md`
> 更新日期：2026-11-06 · debug APK：`android/app/build/outputs/apk/debug/app-debug.apk`

## 一、架构

```
ui/                       Presentation（Compose 页面 + 状态）
├── theme/Theme.kt        M3 ColorScheme（浅/深/跟随系统）
├── Common.kt             AsyncView（加载/错误/内容三态）、PageTitle、fmt
├── HomePage.kt           首页：账号头 + 调度器WS状态 + 寄养 + 每日任务
├── AssetsPage.kt         资产：近 7 天账本（随当前账号 role_key）
├── TasksPage.kt          任务：分组 + 启停（停止有确认）+ 运行高亮 + 参数只读
├── RecordsPage.kt        记录：子页签「统计」/「实时日志」
├── LogsPage.kt           实时日志：SSE 流（自动滚动，保留 400 行）
└── SettingsPage.kt       设置：后端地址 / 主题 / 关于

data/                     Data
├── Api.kt (net/)         OkHttp 客户端（baseUrl 可变、@Volatile）
├── Models.kt (net/)      DTO（activity/foster/ledger，kotlinx.serialization）
├── Models.kt (data/)     调度器 WS 模型 / 统计模型 / 角色模型
├── SchedulerSocket.kt    WS /ws/{script} 实时通道（Flow，断线指数退避重连）
├── LogStream.kt          SSE /log/{script}/stream 日志流（重连 + 离开即断）
├── AccountContext.kt     全局账号上下文（StateFlow，resource/roles 离线可读）
└── Prefs.kt              SharedPreferences（后端地址/主题/当前账号）
```

说明：v1 未引入独立 Domain 层与 UseCase——客户端无复杂业务规则（多为透传展示），
按规范「不要为了架构形式而过度抽象」暂缓；引入时机：出现跨页面复用的业务规则时。

## 二、功能实现清单

| 页面 | 已实现 | 数据源 |
|---|---|---|
| 首页 | 账号头（名称/等级/区服/调度器）、调度器实时状态（WS：状态/当前任务/队列）、每日任务进度、寄养双卡、连接错误提示 | /config_list, /ws/{script}, /activity, /notify/foster/refresh |
| 资产 | 近 7 天账本（当前值/起值/增量，涨跌配色），账号隔离（role_key 随账号） | /resource/{config}/ledger |
| 任务 | 任务分组列表、运行中任务实时高亮（WS）、脚本启动/停止（停止带确认）、**点任务名查看参数（只读分组渲染）** | /script_menu, /ws/{script}, /{script}/start|stop, /{script}/{task}/args |
| 记录 | 子页签「统计」：日期切换、总览磁贴、各任务次数/时长/runs；子页签「实时日志」：**SSE 实时流、自动滚动、保留 400 行** | /stats/*, /log/{script}/stream |
| 设置 | 后端地址（测试连接，保存即时生效）、主题（跟随系统/浅/深，即时生效）、关于 | /config_list |

## 三、未实现清单（与原因）

| 项 | 原因 |
|---|---|
| 任务参数**编辑**（PUT value） | 只读查看已做；编辑需字段类型映射控件（boolean/integer/date_time/enum…），下版本 |
| 任务启用开关 | 依赖参数编辑同套 PUT 通道 |
| 多账号并排展示 | 单账号聚焦设计；切换器已支持 |
| 语言切换 | 字符串抽取未做（与 WPF 同一遗留）；任务名暂为内部英文名 |
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
| assembleDebug | ✅ BUILD SUCCESSFUL |
| 安装（adb / 浏览器下载） | ✅ |
| 设置页（保存/测试/主题即时生效） | ✅ 状态上浮后逻辑验证 |
| 数据链路（v0 基线真机截图） | ✅ |
| 离线快速失败 | ✅ ~350ms |
| SSE 日志 / 运行高亮 / 参数查看 | 编译通过；待真机联调（脚本跑起来后看日志滚动与高亮） |

## 六、已知风险

1. 后端无鉴权（用户决策：家庭内网、单人使用）——勿在公共网络暴露 22288
2. `script_menu` 返回内部任务名（英文），中文映射待 I18n 通道
3. SchedulerSocket/LogStream 重连协程在 callbackFlow 作用域内——flow 取消即随
   作用域结束，无泄漏；重连中的 delay 不可取消（最长 10s/5s）
4. 任务页启停按钮与 WS 状态无双向联动（启动后状态由 WS 推送自然刷新）
