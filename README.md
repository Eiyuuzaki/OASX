# OASX · 原生双端客户端

[OnmyojiAutoScript](https://github.com/Eiyuuzaki/OnmyojiAutoScript) 的**桌面 + 移动端**图形客户端。
后端是 OAS（Python，本机 `127.0.0.1:22288`），两个客户端都是它的 HTTP 客户端——
内存读取、调度、通知全部在 PC 端完成，客户端只负责展示与操作。

> Flutter 版本已弃用（UI 受单一 Design Language 限制，无法体现平台原生观感）。
> 旧代码完整保留在 `flutter-archive` 分支，可作为行为参照。

## 目录

| 路径 | 内容 | 技术栈 |
|---|---|---|
| `windows/` | Windows 桌面客户端 | WPF + WPF-UI 4.3 / .NET 8 |
| `android/` | Android 客户端（规划中） | Kotlin + Jetpack Compose |
| `docs/openapi.json` | 后端 API 契约（两端共用） | FastAPI OpenAPI 导出 |

## Windows 客户端

```powershell
cd windows
dotnet build -c Debug
dotnet publish -c Release -r win-x64 --self-contained   # 绿色分发
```

默认连接 `http://127.0.0.1:22288`，与后端同机运行。

## API 契约

`docs/openapi.json` 是两端客户端的唯一契约来源：

```powershell
curl http://127.0.0.1:22288/openapi.json -o docs/openapi.json
```

后端路由变更后重新导出并提交——契约 diff 就是两端都要跟进的信号。

## 开发约定

- 一个功能一个 commit，message 写清「为什么」
- 不提交第三方逆向产物或解密脚本
- 提交前必须编译通过；涉及 UI 的改动需贴验证截图