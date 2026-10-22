import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb, defaultTargetPlatform;
import 'package:flutter/material.dart';
import 'package:flutter_acrylic/flutter_acrylic.dart';
import 'package:get/get.dart';

/// 窗口材质接入（flutter_acrylic）。
///
/// 材质**默认开启**，不再提供开关：Mica 走 DWM 的系统材质，开销远低于
/// Acrylic 的实时模糊，没有关闭的必要。
///
/// ## 为什么用 Mica 而不是 Acrylic
///
/// Acrylic 模糊的是**窗口背后**的内容。实测：背后铺一个白底网页时，
/// 40% 白 tint 糊上去就是一片平坦浅灰——标题栏 (211,211,211)、蓝-R = 0.0、
/// 局部对比度 0.00，观感完全等同于"没有玻璃"，且随背后窗口不同而变化。
///
/// Mica 取的是**桌面壁纸**，不受背后是什么窗口影响，观感稳定。
/// 这里优先用 Mica Alt（`tabbed`，染色比 Mica 更明显），依次回退。
///
/// ## 调用顺序
///
/// 1. `Window.initialize()` 必须在 `runApp` **之前**
/// 2. 材质要在 `window_manager` 的窗口样式操作**之后**应用，否则被覆盖
class AcrylicService {
  AcrylicService._();

  static bool _pluginReady = false;

  /// 材质是否已实际生效（供主题层判断要不要把背景设为透明）。
  ///
  /// - 桌面（Windows/macOS）：由系统材质下发成功与否决定
  /// - 移动端：恒为 false——系统没有窗口材质，但**主题仍需让出透明底**，
  ///   否则 `FrostedBars` 的 `BackdropFilter` 下方是不透明纯色，毛玻璃看不出来。
  ///   移动端的透明底由 [mobileFrostedBackground] 单独判定。
  static bool transparentBackground = false;

  /// 移动端是否需要透明 scaffold 底（供 `FrostedBars` 生效）。
  ///
  /// 移动端没有系统窗口材质，毛玻璃是 Flutter 自渲染的 `BackdropFilter`：
  /// 它只会模糊**下方已绘制的内容**。若 scaffold 画不透明底色，
  /// 底栏/AppBar 的模糊层下面就是纯色，视觉上等于没有毛玻璃。
  /// 因此移动端把 scaffold 底设为透明，让页面内容/壁纸透上来。
  static bool get mobileFrostedBackground => _isMobile;

  /// 支持系统材质的平台。
  static bool get _supported =>
      !kIsWeb && (Platform.isWindows || Platform.isMacOS);

  /// 移动平台（Android/iOS）。
  static bool get _isMobile =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS);

  /// 设备是否支持（不支持的平台不渲染相关 UI）。
  static bool get supported => _supported;

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

  /// 阶段二：应用材质。在窗口就绪之后调用。
  static Future<void> applyEffect({bool? dark}) async {
    if (!_supported || !_pluginReady) return;
    final useDark = dark ?? false;

    // 1) Mica Alt（tabbed）：取壁纸，染色比 Mica 更明显
    try {
      await Window.setEffect(effect: WindowEffect.tabbed, dark: useDark);
      transparentBackground = true;
      debugPrint('AcrylicService: tabbed/mica-alt (dark=$useDark) OK');
      return;
    } catch (e) {
      debugPrint('AcrylicService: tabbed failed: $e');
    }

    // 2) Mica：同样取壁纸，更含蓄
    try {
      await Window.setEffect(effect: WindowEffect.mica, dark: useDark);
      transparentBackground = true;
      debugPrint('AcrylicService: mica (dark=$useDark) OK');
      return;
    } catch (e) {
      debugPrint('AcrylicService: mica failed: $e');
    }

    // 3) Acrylic：实时模糊背后内容，用 tint 控制明暗
    //
    // 深色下 0x99（60%）仍太透，壁纸亮蓝会把白字压得看不清，故用 0xCC（80%）。
    final tint = useDark
        ? const Color(0xCC0A0A0C)
        : const Color(0x66FFFFFF);
    try {
      await Window.setEffect(effect: WindowEffect.acrylic, color: tint);
      transparentBackground = true;
      debugPrint('AcrylicService: fallback acrylic tint=0x'
          '${tint.toARGB32().toRadixString(16)} (dark=$useDark) OK');
      return;
    } catch (e) {
      debugPrint('AcrylicService: acrylic failed: $e');
    }

    debugPrint('AcrylicService: all effects failed, keep opaque background');
    transparentBackground = false;
  }

  /// 主题切换时重新应用材质（明暗分支变化）。
  static Future<void> refresh({bool dark = false}) async {
    if (!_supported || !_pluginReady) return;
    await applyEffect(dark: dark);
    // 主题里的 scaffold/card 颜色依赖 transparentBackground，
    // 它可能刚变过，需要让界面重建一次才能生效。
    Get.forceAppUpdate();
  }

  /// 仅重设 DWM 材质，不触发界面重建。
  ///
  /// 用于窗口状态变化（最小化/还原、最大化、托盘恢复）后补回丢失的材质：
  /// Windows 在这些时机可能重建合成层，把 SYSTEMBACKDROP_TYPE 重置掉，
  /// 表现为 Mica 突然消失。此时 `transparentBackground` 没变，
  /// 界面无需重建，只把材质重新下发一次即可。
  static Future<void> reapply({bool dark = false}) async {
    if (!_supported || !_pluginReady) return;
    await applyEffect(dark: dark);
  }
}