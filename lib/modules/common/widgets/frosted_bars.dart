import 'dart:ui' show ImageFilter;

import 'package:flutter/foundation.dart' show defaultTargetPlatform, kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// 移动端毛玻璃组件集：底栏 / AppBar / 触感反馈工具。
///
/// 用 Flutter 自渲染的 `BackdropFilter` 复刻系统级毛玻璃
/// （flutter_acrylic 不支持移动端）。只用在底栏/AppBar 这类
/// 固定小面积区域——全局模糊在移动端 GPU 上代价过高，勿扩散。
class FrostedBars {
  FrostedBars._();

  /// 毛玻璃底栏：内容滚动时从栏底透出。
  ///
  /// [child] 通常是 NavigationBar；透明度取主题 surface 的 ~72%，
  /// 深浅色主题自动跟随。
  static Widget bottomBar(BuildContext context, {required Widget child}) {
    return ClipRect(
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 24, sigmaY: 24),
        child: ColoredBox(
          color: _tint(context),
          child: child,
        ),
      ),
    );
  }

  /// 毛玻璃 AppBar，与底栏成对使用。
  static Widget appBar(BuildContext context, {required Widget child}) {
    return ClipRect(
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 24, sigmaY: 24),
        child: ColoredBox(
          color: _tint(context),
          child: child,
        ),
      ),
    );
  }

  /// 半透明底色：浅色 78% / 深色 72%（深色下透一点更通透）。
  static Color _tint(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return scheme.brightness == Brightness.light
        ? scheme.surface.withValues(alpha: 0.78)
        : scheme.surface.withValues(alpha: 0.72);
  }
}

/// 触感反馈：只在真实移动设备上生效，桌面/Web 是空操作。
class Haptics {
  Haptics._();

  /// 页签/导航切换：轻微-selectionClick。
  static void selection() {
    if (!isMobile) return;
    HapticFeedback.selectionClick();
  }

  /// 下拉刷新触发 / 采集完成：mediumImpact。
  static void medium() {
    if (!isMobile) return;
    HapticFeedback.mediumImpact();
  }

  static bool get isMobile =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS);
}
