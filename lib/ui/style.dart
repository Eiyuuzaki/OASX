import 'package:flutter/material.dart';

import 'package:oasx/utils/platform_utils.dart';

/// 语义组件层的**风格枚举**。
///
/// 平台到风格的映射集中在这里，业务代码不直接判断平台：
///
/// | 平台 | 风格 | 依据 |
/// |---|---|---|
/// | Windows | fluent | WinUI 3 / Fluent Design |
/// | macOS / iOS | cupertino | Apple HIG |
/// | Android | material | Material You |
/// | Linux | material | 无统一原生范式，沿用 M3 |
/// | Web | material | 同 Linux |
enum AppUiStyle {
  /// WinUI / Fluent Design（Windows）。
  fluent,

  /// Material You（Android / Linux / Web）。
  material,

  /// Apple Human Interface Guidelines（macOS / iOS）。
  cupertino,
}

/// 解析当前平台应使用的 UI 风格。
AppUiStyle resolveAppUiStyle() {
  return switch (PlatformUtils.platfrom()) {
    PlatformType.windows => AppUiStyle.fluent,
    PlatformType.macOS || PlatformType.iOS => AppUiStyle.cupertino,
    _ => AppUiStyle.material,
  };
}

/// 语义层统一使用的圆角规格。
///
/// 各风格的原生规范不同（WinUI 4/8px、M3 12/16px、HIG 10/14px），
/// 但差异过大会让跨平台观感割裂，这里取一组折中值：
/// 小圆角用于紧凑元素（输入框、列表项），大圆角用于卡片/对话框。
class AppRadius {
  AppRadius._();

  /// 紧凑元素（列表项、输入框、小块按钮）。
  static const double small = 8;

  /// 容器（卡片、面板）。
  static const double medium = 12;

  /// 大容器（对话框、模态面板）。
  static const double large = 16;

  /// 胶囊（完整圆角）。
  static const double pill = 999;

  static BorderRadius get smallRadius => BorderRadius.circular(small);
  static BorderRadius get mediumRadius => BorderRadius.circular(medium);
  static BorderRadius get largeRadius => BorderRadius.circular(large);
  static BorderRadius get pillRadius => BorderRadius.circular(pill);
}

/// 语义层统一使用的间距刻度（4 的倍数，对齐主流设计系统）。
class AppSpacing {
  AppSpacing._();

  static const double xxs = 2;
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 24;
  static const double xxl = 32;

  static const EdgeInsets cardPadding = EdgeInsets.all(lg);
  static const EdgeInsets cardPaddingCompact = EdgeInsets.all(md);
  static const EdgeInsets pagePadding = EdgeInsets.all(lg);
}

/// 语义层统一的动画时长。
class AppDuration {
  AppDuration._();

  /// 悬停/按压反馈。
  static const Duration fast = Duration(milliseconds: 120);

  /// 展开/收起、状态切换。
  static const Duration normal = Duration(milliseconds: 200);

  /// 页面级过渡。
  static const Duration slow = Duration(milliseconds: 300);
}
