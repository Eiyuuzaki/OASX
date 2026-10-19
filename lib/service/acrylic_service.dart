import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_acrylic/flutter_acrylic.dart';
import 'package:get/get.dart';
import 'package:get_storage/get_storage.dart';

/// 窗口材质接入（flutter_acrylic）+ 运行时开关。
///
/// ## 按官方文档的标准用法
///
/// ```dart
/// Future<void> main() async {
///   WidgetsFlutterBinding.ensureInitialized();
///   await Window.initialize();      // ← 必须在 runApp 之前
///   runApp(MyApp());
/// }
/// await Window.setEffect(effect: WindowEffect.acrylic, dark: true);
/// ```
///
/// 三个关键点：
/// 1. `Window.initialize()` 要在 `runApp` **之前**，紧跟 `ensureInitialized()`
/// 2. `setEffect` 要传 **`dark`** 参数（不是 `color`），它影响 Windows 端材质分支
/// 3. 材质设置要在 `window_manager` 的样式操作**之后**（否则被覆盖）
///
/// ## 开关
///
/// 用户可在设置页随时切换。开关状态持久化在 `GetStorage`，
/// 关闭时窗口回退为不透明纯色（`WindowEffect.solid`）。
class AcrylicService {
  AcrylicService._();

  static const String _storageKey = 'window.acrylicEnabled';

  static bool _pluginReady = false;

  /// 材质是否已实际生效（供主题层判断要不要把背景设为透明）。
  static bool transparentBackground = false;

  /// 支持系统材质的平台。
  static bool get _supported =>
      !kIsWeb && (Platform.isWindows || Platform.isMacOS);

  /// 设备是否支持（不支持的平台不显示开关）。
  static bool get supported => _supported;

  /// 当前是否启用（响应式，供设置页 Switch 使用）。
  static final enabled = true.obs;

  /// 从持久化读取用户的开关选择。
  ///
  /// 读不到就用默认值（开启）。GetStorage 在某些环境下会**异步**抛错
  /// （磁盘权限、目录不存在），同步 try/catch 抓不住，因此这里只做
  /// 最小化的同步读取，异常交给上层兜底。
  static void loadPreference() {
    try {
      final raw = GetStorage().read(_storageKey);
      if (raw is bool) {
        enabled.value = raw;
      }
    } catch (_) {
      // 读取失败用默认值（开启）
    }
  }

  /// 阶段一：插件初始化。**必须在 runApp 之前调用**。
  static Future<void> initializePlugin() async {
    if (!_supported) return;
    try {
      await Window.initialize();
      _pluginReady = true;
      debugPrint('AcrylicService: Window.initialize OK');
    } catch (e) {
      debugPrint('AcrylicService: Window.initialize failed: $e');
    }
  }

  /// 阶段二：按当前开关状态应用材质。在窗口就绪之后调用。
  ///
  /// ## 为什么 acrylic 要额外传 color
  ///
  /// `dark` 参数只影响 **mica / tabbed**（它们按系统主题取色）。
  /// `acrylic` 是"实时模糊 + 叠一层 tint 色"，tint 颜色必须通过 `color`
  /// 显式指定，否则材质的明暗与 Flutter 主题无关：
  /// - 深色主题下仍是亮色玻璃 → 白字压亮底，几乎看不清
  /// - 浅色主题下若被染深 → 深字压暗底，同样看不清
  static Future<void> applyEffect({bool? dark}) async {
    if (!_supported || !_pluginReady) return;
    final useDark = dark ?? false;

    if (!enabled.value) {
      await _setSolid(dark: useDark);
      return;
    }

    // acrylic 的 tint：深色用高不透明度的深色，浅色用低不透明度的白色。
    // alpha 太低会让壁纸色彩直接淹没文字。
    //
    // 实测深色下 0x99（60%）仍然太透，壁纸的亮蓝会把白字压得看不清，
    // 因此提到 0xCC（80%）——仍能看出壁纸的明暗层次，但文字足够清晰。
    final tint = useDark
        ? const Color(0xCC0A0A0C) // 80% 近黑
        : const Color(0x66FFFFFF); // 40% 白

    try {
      await Window.setEffect(effect: WindowEffect.acrylic, color: tint);
      transparentBackground = true;
      debugPrint('AcrylicService: acrylic tint=0x'
          '${tint.toARGB32().toRadixString(16)} (dark=$useDark) OK');
      return;
    } catch (e) {
      debugPrint('AcrylicService: acrylic failed: $e');
    }

    // 回退：mica / tabbed（这两个认 dark 参数）
    for (final effect in [WindowEffect.mica, WindowEffect.tabbed]) {
      try {
        await Window.setEffect(effect: effect, dark: useDark);
        transparentBackground = true;
        debugPrint('AcrylicService: fallback $effect (dark=$useDark) OK');
        return;
      } catch (e) {
        debugPrint('AcrylicService: $effect failed: $e');
      }
    }

    debugPrint('AcrylicService: all effects failed, fallback to solid');
    await _setSolid(dark: useDark);
  }

  /// 主题切换时重新应用材质（明暗分支变化）。
  ///
  /// 与 [applyEffect] 的区别：这里**不做初始化检查**，允许重复调用，
  /// 因为切换深浅色时材质本身要重新设置一遍。
  static Future<void> refresh({bool dark = false}) async {
    if (!_supported || !_pluginReady) return;
    await applyEffect(dark: dark);
  }

  /// 设置页切换开关。
  ///
  /// 切换后立即应用效果并持久化，界面会自动重建（`enabled` 是 Rx）。
  static Future<void> setEnabled(bool value, {bool? dark}) async {
    enabled.value = value;
    try {
      GetStorage().write(_storageKey, value);
    } catch (_) {}
    await applyEffect(dark: dark);
    // 主题里的 scaffold/card 颜色依赖 transparentBackground，
    // 它刚刚变了，需要让界面重建一次才能生效。
    Get.forceAppUpdate();
  }

  /// 关闭材质：窗口回退为**不透明实色**。
  ///
  /// 颜色要跟随主题，不能写死黑色——浅色主题下窗口变黑会非常突兀
  /// （用户实测看到的就是"关掉毛玻璃后整个界面发黑"）。
  static Future<void> _setSolid({bool? dark}) async {
    final isDark = dark ?? false;
    final color = isDark ? const Color(0xFF1C1B1F) : const Color(0xFFFBF9FF);
    try {
      await Window.setEffect(effect: WindowEffect.solid, color: color);
    } catch (e) {
      debugPrint('AcrylicService: solid failed: $e');
    }
    transparentBackground = false;
  }
}
