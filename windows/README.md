# OASX for Windows（WPF）

OAS（[OnmyojiAutoScript](https://github.com/runhey/OnmyojiAutoScript)）的
Windows 前端主线。从 Flutter 版 OASX 迁移而来，UI 走 WPF-UI（Fluent）范式。

> 选型结论与 WinUI 3 原型的对比见 `D:\OAS-Kirin\OASX-WinUI3\README.md`（已归档）。

## 技术栈

- .NET 8 + WPF，UI 库 **WPF-UI 4.3.0**（BGI 同款：FluentWindow / NavigationView /
  Mica 内置；控件源码开放，出问题可读源码自救）
- 后端对接：本机 OAS `server.py`，默认 `http://127.0.0.1:22288`（可在设置页改）

## 构建与运行

```powershell
# 需 .NET SDK 8+（本机已装 8.0.427）
dotnet run --project .\OASX.WPF.csproj

# 发布（自包含，单文件夹绿色分发）
dotnet publish .\OASX.WPF.csproj -c Release -r win-x64 --self-contained
```

## 结构

| 路径 | 说明 |
|---|---|
| `Services/OasApi.cs` | 后端 REST 客户端（config_list / resource / state / start / stop / logs） |
| `Services/OasLogStream.cs` | SSE 实时日志流（**禁用 `EndOfStream`**，同步阻塞读会卡死 UI） |
| `Services/OasSettings.cs` | 设置持久化：`%LOCALAPPDATA%\OASX\settings.json`（故意不用 WinRT 存储） |
| `Services/PageService.cs` | 页面注册与导航 |
| `Views/` | Dashboard（状态/启停）、Log（实时日志）、Settings、Stub（占位） |

## 与 Flutter 版的对照

Flutter 版（`D:\OAS-Kirin\OASX`，已归档）仍是功能最全的参考实现：
配置编辑、调度仪表盘、资产/统计页都在那边，往本仓库移植时以其 API 契约为准。
