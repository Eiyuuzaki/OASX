
<div align="center">
<br><br>

# OASX

<br>

<br>

同 [OAS](https://github.com/runhey/OnmyojiAutoScript) 对接的全平台 GUI

</br>
</div>

> [!NOTE]
> 本仓库是 OASX 的增强分支：基于 [AzurTian/OASX](https://github.com/AzurTian/OASX)
> 的 modules/ 架构重构版继续开发，并合并了 [luchi5/OASX](https://github.com/luchi5/OASX)
> 的三栏工作台与统计图表。感谢两位前辈——AzurTian 的重构是这一切的基底，
> luchi5 的统计/工作台设计是直接来源。

### 关于OAS

OAS 是阴阳师的一款自动化脚本，同时也是基于 Alas 进行开发。

Alas 的GUI设计是方案是 [PyWebIO](https://github.com/pywebio/PyWebIO) + [Electron](https://www.electronjs.org/)，实现上非常高效快速，非常满足当时的设计需求。但是同样存在相当的问题：

- 性能开销大，Electron以臃肿而臭名昭著。
- 扩展性受限，所能实现的需求基本依赖于PyWebIO库，缺乏灵活性。
- 屎山难以搬，Alas的长期发展使得其GUI同游戏强耦合，新入手难以理解。

为此 OAS 进行了初次尝试，即 [Qt for Python](https://wiki.qt.io/Qt_for_Python) + [Qt QML](https://doc.qt.io/qt-6/qtqml-index.html), QML语言是Qt家族的一个分支，是基于C++的解释型语言，使用的QML库是[FluentUI](https://github.com/zhuzichu520/FluentUI)，几乎可以满足上方的不足，但是会存在更多的问题。

- QML并不够成熟，缺少三方库管理，非常灵活的代码写法...生态很少
- 运行速度低，qml <-> c++ <-> python 绕了挺多层的数据导致其他的额外开销
- 无法理解的内存回收机制，缓存策略挺绕的，实际下来并不可以
- 只有桌面端

至此将 OAS 的 GUI 部分拆离出来形成 OASX。

- 全平台，Flutter 以全平台著称，随时用手机控制你的游戏脚本是非常便捷的
- 性能开销低，编译型语言几乎等同于原生语言开发
- 生态足够丰富，Flutter 以移动端起家，几年的发展其桌面端足够成熟，以及丰富的第三方包

---

## 本分支改了什么

在 AzurTian 重构版（modules/ 架构 + GetX 路由 + PrimaryNavigationShell）之上：

### 1. 三栏工作台（来自 luchi5/OASX）

- 脚本集合 / 任务工作台 / 侧栏三栏布局，分隔条可拖拽、可折叠、宽度按脚本持久化
- 窄屏自动降级两栏/单栏（阈值 720px），移动端走底栏导航

### 2. 统计面板（来自 luchi5/OASX）

- 任务耗时条形图 + 横轴自适应刻度，点击任务看单次运行明细
- 数据来自 OAS 后端 `/stats/*`（日志解析），带实时 SSE 推送
- 探测式兼容：先查 `/openapi.json`，后端无统计路由时优雅显示空态
- 任务名显示中文（走后端 `/home/task_name_translate`，182 条映射）

### 3. 资产面板（本分支新增）

- 通过 Frida 读取游戏内存 `Globals.player1.currency`，按日存档 + 增量对比
- **数据按角色隔离**（`role_key = avatarId@server_id`），切号不串数据
- 只显示本地有图标的资源；**同图标同名自动聚合求和**——绘卷碎片每期活动
  换新 ID，聚合后新活动自动纳入，无需改代码
- 历史折线图按名称跨期查询，换期不断档；数据超过 7 天可自选时间范围
- 采集时机事件驱动（脚本启停 / 每日 04:00 / 心跳兜底），不轮询

### 4. 服务器显示名

- 角色下拉显示游戏内区服名（如「永远的平安京」）而非渠道注册名
- 映射来自游戏内存 `ServerManager.getServerDefineByServerID()['showName']`，
  155 个服务器全量

### 5. 桌面窗口效果

- Windows 亚克力/云母（链式降级，Win10/11 自适应）、macOS 振动材质
- 移动端/Web 自动回退纯色主题，行为与原版一致

### 6. 移动端美化

- 底栏/AppBar 毛玻璃（BackdropFilter），内容滚动时从栏后透出
- 全局卡片 16px 圆角统一、AMOLED 纯黑深色模式、触感反馈

---

## 环境要求

```
Flutter 3.35.0 • Dart 3.9.0
```

> [!WARNING]
> Flutter 会修改接口导致报错，版本必须对齐（3.27.1 无法编译本分支）。

Android 构建需要 SDK 34（platforms;android-34 + build-tools;34.0.0）。

## 构建

```powershell
flutter pub get
flutter build windows --debug                  # 桌面
flutter build apk --release --split-per-abi    # Android（单架构约 22MB）
```

后端地址默认 `127.0.0.1:22288`；Android 模拟器内访问宿主机用 `10.0.2.2:22288`。

对应的 OAS 后端需要挂载资产与统计路由（`/resource/*`、`/stats/*`、
`/home/task_name_translate`），见分支内 `docs/` 说明。

---

## 许可证 LICENSE

This project is licensed under the GNU General Public License v3.0.

## 声明 Announcement
本软件开源、免费，仅供学习交流使用。开发者团队拥有本项目的最终解释权。使用本软件产生的所有问题与本项目与开发者团队无关。
