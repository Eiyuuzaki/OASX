# OASX

> [!WARNING]
> ## 本仓库已归档（Archived）
>
> 这个仓库不再接收更新，代码保留作为**可用的历史基线**。
>
> **后续开发已迁移到新仓库**：`<!-- TODO: 填入新仓库地址，如 https://github.com/Eiyuuzaki/OASX-Next -->`
>
> 迁移的原因：本项目功能上已经稳定可用，但 UI 全部基于 Material Design 一套范式，
> 无法体现各平台的原生设计语言（Windows 的 Fluent、Apple 的 HIG）。
> 新仓库会在**共用业务逻辑**的前提下，把 UI 层按平台分化重做。
>
> **注意**：OAS 后端仍在维护。即使后端更新了新功能，本仓库的前端也**不会**跟进适配，
> 需要新前端才能使用。

---

同 [OAS](https://github.com/runhey/OnmyojiAutoScript) 对接的全平台 GUI。

> [!NOTE]
> 本仓库基于 [AzurTian/OASX](https://github.com/AzurTian/OASX) 的 modules/ 架构重构版，
> 合并了 [luchi5/OASX](https://github.com/luchi5/OASX) 的三栏工作台与统计图表。
> AzurTian 的重构是地基，luchi5 的工作台和统计设计是直接来源。

### 关于 OAS

OAS 是阴阳师的一款自动化脚本，基于 Alas 开发。

Alas 用的是 PyWebIO + Electron。这套方案在当时很高效，但后来越来越难推：

- Electron 臃肿，性能开销大
- 扩展性受限于 PyWebIO，想要什么效果得看库支不支持
- 长期迭代后 GUI 和游戏逻辑耦合很深，新人很难接手

OAS 先试过 Qt for Python + QML + FluentUI，问题反而更多：QML 生态薄弱、三层数据往返拖慢速度、内存回收机制难以捉摸，而且只有桌面端。

于是把 GUI 拆出来做成 OASX：

- 全平台，手机也能控制脚本
- 编译型语言，性能接近原生
- Flutter 桌面端已足够成熟，第三方包丰富

---

## 本分支的改动

### 三栏工作台

来自 luchi5/OASX。脚本集合 / 任务工作台 / 侧栏三栏布局，分隔条可拖拽、面板可折叠，
宽度按脚本分别记忆。窄屏（< 720px）自动降级两栏或单栏，移动端走底栏导航。

### 统计面板

同样来自 luchi5/OASX。任务耗时条形图，横轴刻度自适应，点任务能看单次运行明细。
数据来自后端 `/stats/*`（解析日志），支持 SSE 实时推送。

后端没有统计路由时不会报错——启动时先查 `/openapi.json`，缺路由就显示空态。
任务名走后端 `/home/task_name_translate` 映射成中文（182 条）。

本分支按 Material 3 重做了统计界面：概览三指标块（Card.filled tonal）、
进度条改 8px 细轨 + 圆角覆盖段（数值独立右列）、明细列表两行布局
（tabular figures 等宽对齐时长、primaryContainer 序号头像）。
连接状态指示只在「今天」有实时流时出现——历史数据是静态的，
放个「云端已同步」云图标语义是错的（数据从不上云）。

### 资产面板

本分支新增。用 Frida attach 到游戏进程，借游戏内嵌的 CPython 读
`Globals.player1.currency`，按日存档并计算增量。

几个实现上的选择：

- **按角色隔离**。`role_key = avatarId@server_id`，同一台设备切号不会串数据，
  读不到身份时拒绝写入。
- **只显示本地有图标的资源**。游戏下发 200 多项，绝大部分没人关心；
  按 `assets/currency/` 里实际存在的图标过滤，只剩 14 项左右。
- **同图标同名求和**。绘卷碎片每期活动会换新 ID（实测 55～64 期），
  但名称始终是「绘卷碎片·小/中/大」。按 `(图标, 名称)` 聚合后，
  新活动开了自动纳入，不需要改代码。历史查询也按名称走，换期不断档。
- **采集由事件驱动**。脚本启停、每日 04:00、6 小时心跳、手动触发，
  不做轮询。
- **可以置顶**。常用的资产（比如勾玉）钉在网格最前，位置不再随当天增量变化。
  置顶状态存在 GetStorage，按脚本配置隔离。
- **工具条按 M3 重做**。角色与日期为胶囊下拉按钮（MenuAnchor），
  采集状态用 8px 小圆点表意，空状态有 tonal 圆形容器托底 + 胶囊 FilledButton。

### 服务器显示名

角色下拉里显示游戏内的区服名（「永远的平安京」），不是渠道注册名。
原来两个字段容易混：`ServerName` 是渠道注册用的，`showName` 才是玩家看到的。
映射取自 `ServerManager.getServerDefineByServerID()`，155 个服务器全量。

### 窗口材质

Windows 用 **Mica Alt（tabbed）**，DWM 系统材质、按桌面壁纸取色，开销远低于
Acrylic 的实时模糊；失败时依次退退 mica / acrylic / 纯色。macOS 用振动材质。
默认开启、无开关——实测 Acrylic 模糊的是「窗口背后的内容」，背后铺着白色网页时
40% tint 糊上去就是一片平坦浅灰，观感等同没有玻璃；Mica 不受背后窗口影响。

踩过的坑记录一下，免得后来人重走：

- `Window.initialize()` 必须在 `runApp` 之前调，放到 `window_manager` 初始化之后会失效
- 材质要在 `window_manager` 所有窗口样式操作**之后**设置，否则被覆盖
- `dark` 参数只对 mica / tabbed 有效，**acrylic 认的是 `color`**。不传的话
  深色主题下材质仍是亮的，白字压在亮玻璃上几乎看不清
- 材质生效时 scaffold 必须完全透明；但 `ThemeData` 写成顶层变量只在启动时求值一次，
  开关切换后主题还停在旧值，所以改成了 getter
- MicaForEveryone 之类会给别的窗口改 `TitleBarColor` 的工具会把本窗口的
  `IMMERSIVE_DARK_MODE` 强制改回浅色，深色主题下标题栏会发白——不是本项目的 bug

### 深浅色

设置页三选一：跟随系统 / 浅色 / 深色。跟随时读 `MediaQuery.platformBrightness`，
系统切换会同步。旧版本只存了 `dark: bool`，读取时做了兼容推断。

### 移动端

底栏和 AppBar 用 `BackdropFilter` 做毛玻璃，内容滚动时从栏后透出。
全局卡片统一 16px 圆角，另有 AMOLED 纯黑模式，触感反馈。

---

## 环境

```
Flutter 3.35.0 · Dart 3.9.0
```

> [!WARNING]
> Flutter 各版本接口有差异，版本必须对齐，3.27.1 编译不过本分支。

Android 构建需要 SDK 34（`platforms;android-34` + `build-tools;34.0.0`）。

## 构建

```powershell
flutter pub get
flutter build windows --debug                       # 桌面
flutter build apk --release --target-platform android-arm64   # Android，约 22MB
```

> 只构建 arm64，不要 `--split-per-abi`——v7a / x86_64 用不上。

后端地址默认 `127.0.0.1:22288`；Android 模拟器访问宿主机用 `10.0.2.2:22288`。

配套的 OAS 后端需要挂载 `/resource/*`、`/stats/*`、`/home/task_name_translate`
三组路由，实现说明见 `docs/`。

## 测试

```powershell
flutter test
```

覆盖置顶排序、主题模式映射、旧配置兼容、统计面板渲染。

## 归档说明

本仓库于 2026-10 归档，作为可用的历史基线保留。

### 归档时的状态

功能完整可用，日常使用没有问题。已实现并验证过的能力：

| 模块 | 状态 |
|---|---|
| 三栏工作台 | 稳定，含拖拽分隔条、折叠、按脚本记忆宽度 |
| 任务统计 | 稳定，SSE 实时推送 + 历史查询，界面按 M3 重做过 |
| 资产面板 | 稳定，Frida 读游戏内存，按角色隔离、按日存档 |
| 网易大神签到 | 稳定，ADB + Frida 签名 + 官方 API |
| 百鬼棋局 | 稳定，实机完整跑通一局（第 2 名，0 错误） |
| 窗口材质 | Windows Mica Alt 默认开启；移动端为 Flutter 自渲染模糊 |
| 深浅色 / AMOLED | 稳定 |
| 设备徽标 | 稳定，标题栏显示当前实例，支持多模拟器切换 |

### 已知不足

- **UI 只有一套 Material 外观**，Windows 上没有 Fluent 的观感，Apple 平台也没有原生质感
- **移动端毛玻璃效果不明显**——缺少可模糊的内容层次，白底糊白底等于没有
- 97 处硬编码颜色，没有全部走主题 token

这三点正是新仓库要解决的问题。

### 迁移到新仓库

新仓库会：

1. **保留全部业务逻辑**（API 客户端、控制器、服务、数据模型），这部分约 1.7 万行，经过实测验证，直接沿用
2. **重做 UI 层**，按平台分化：Windows → Fluent/WinUI，Android → Material You，iOS/macOS → Cupertino
3. 通过**语义组件层**（`AppCard`、`AppButton` 这类）隔离平台差异，业务代码不判断平台

本仓库已开始铺这层地基（`lib/ui/`），新仓库在此基础上继续。

### 如何使用本仓库

代码仍然完整可编译、可运行，作为参考或自行 fork 都没问题。但请注意：

- **不接受新的功能请求和 bug 修复**
- OAS 后端即使新增了 API，本前端也不会跟进适配

## 许可证

GNU General Public License v3.0

## 声明

本软件开源免费，仅供学习交流。使用本软件产生的任何问题与开发者无关。
