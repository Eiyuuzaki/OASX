# 资产图标目录

**这里放哪些图标，界面上就显示哪些资源。** 没放图标的资源不会出现在资产页。

完整文件名对照见 [图标命名清单](../../../docs/图标命名清单.md)。

## 工作原理

启动时 `IconRegistry` 读取 `AssetManifest`，记住本目录下所有 PNG 的 basename，
然后只保留 `icon` 字段能对上的资源项。

- 新增图标 → 重新构建 → 自动出现，**不用改代码**
- 删除图标 → 重新构建 → 自动消失
- 一个图标可对应多个资源（如 `icon_tili.png` 同时给体力/自动小纸人体力等）

## 命名规则

**文件名 = 游戏配置表里的 `icon` 字段 + `.png`**

代码侧（`stripImageExt()`）会做规范化：

1. 去掉可能已有的扩展名（`.png` / `.jpg` / `.jpeg` / `.webp`）
2. 如果带路径，只取文件名

所以下面两种游戏写法都指向**同一个**文件：

| 游戏里的 `icon` | 需要的文件名 |
|---|---|
| `icon_jinbi` | `icon_jinbi.png` |
| `icon_longcheranliao.png` | `icon_longcheranliao.png` |

## 举例

| 资源 | 需要的文件名 |
|---|---|
| 金币 | `icon_jinbi.png` |
| 勾玉 | `icon_gouyu.png` |
| 体力 | `icon_tili.png` |
| 神秘的符咒 | `icon_choukaquan.png` |
| 友情点 | `icon_youqingdian.png` |
| 荣誉 | `icon_rongyu.png` |
| 百鬼夜行门票 | `icon_baiguiyexingmenpiao.png` |

## 规格建议

- **格式**：PNG（带透明通道）
- **尺寸**：64×64 或 96×96 即可，界面显示为 34×34，`BoxFit.cover`
- **命名**：全小写 + 下划线，与游戏一致，不要自行改写

## 图标放了但没显示？

三种可能：

1. **该资源当前持有量为 0** —— `player1.currency` 只包含持有过的条目，
   完全没接触过的资源不会出现（例如 `pic_suipian1.png` / 绘卷碎片·小）
2. **文件名不匹配** —— 对照 `docs/图标命名清单.md` 检查拼写
3. **没重新构建** —— 改了 assets 必须 `flutter build windows`，热重载不加载新 asset

## 重要

新增图标后需要**重新构建**才能生效：

```powershell
cd D:\OAS-Kirin\OASX
flutter build windows --debug
```
