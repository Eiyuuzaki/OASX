import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_acrylic/flutter_acrylic.dart';

/// 亚克力/毛玻璃窗口效果开关。
///
/// **仅桌面端生效**（Windows 亚克力 / macOS 振动 / Linux 模糊）；
/// Android/iOS/Web 自动跳过，UI 回退到主题纯色，行为与之前完全一致。
///
/// 为什么内容区仍要实底：亚克力让窗口背景透出桌面壁纸，
/// 如果整个应用都半透明，文字会叠在壁纸上，花哨壁纸直接毁掉可读性。
/// 所以只透「窗口四周的背景层」，内容卡片保持实底——
/// 与微软自家应用（资源管理器/终端）的做法一致。
class AcrylicService {
  AcrylicService._();

  /// 总开关：出问题时改 false 一键回退纯色窗口。
  static const bool enabled = true;

  static bool _initialized = false;

  /// 是否处于"背景已透明"状态（供主题层判断要不要用半透明色）。
  static bool transparentBackground = false;

  /// 支持亚克力的平台。
  static bool get _supported => !kIsWeb && (Platform.isWindows || Platform.isMacOS);

  /// 在 main() 里 runApp 之前调用。
  ///
  /// 效果选择：**Acrylic 优先**——DWM 对窗口背后的桌面内容做实时
  /// 高斯模糊（用户要的"透出壁纸+毛玻璃"就是它）。降级链：
  /// acrylic（Win10 1803+）→ mica（Win11 云母，壁纸染色不模糊）→ tabbed → 纯色。
  static Future<void> initialize() async {
    if (!enabled || !_supported || _initialized) return;
    _initialized = true;

    const acrylicColor = Color(0xCC202020); // 80% 不透明，模糊层上的 tint
    const micaColor = Color(0xE6202020); // 云母更实，90%

    // 亚克力优先（高斯模糊）；系统异常时逐级降级
    final effects = <(WindowEffect, Color)>[
      (WindowEffect.acrylic, acrylicColor),
      (WindowEffect.mica, micaColor),
      (WindowEffect.tabbed, micaColor),
    ];

    for (final (effect, color) in effects) {
      try {
        await Window.initialize();
        await Window.setEffect(effect: effect, color: color);
        transparentBackground = true;
        debugPrint('AcrylicService: using $effect');
        return;
      } catch (e) {
        debugPrint('AcrylicService: $effect failed, trying next: $e');
      }
    }
    debugPrint('AcrylicService: all effects failed, fallback to solid');
    transparentBackground = false;
  }

  /// 恢复不透明窗口（设置页将来可做开关）。
  static Future<void> disable() async {
    if (!_initialized) return;
    try {
      await Window.setEffect(effect: WindowEffect.solid, color: Colors.black);
      transparentBackground = false;
    } catch (_) {}
  }
}
