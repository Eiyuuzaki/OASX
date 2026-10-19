import 'package:flutter/material.dart';
import 'package:chinese_font_library/chinese_font_library.dart';
import 'package:oasx/service/acrylic_service.dart';

/// 用枚举太麻烦了
enum ColorSeed {
  baseColor('M3 Baseline', Color(0xff6750a4)),
  indigo('Indigo', Colors.indigo),
  blue('Blue', Colors.blue),
  teal('Teal', Colors.teal),
  green('Green', Colors.green),
  yellow('Yellow', Colors.yellow),
  orange('Orange', Colors.orange),
  deepOrange('Deep Orange', Colors.deepOrange),
  pink('Pink', Colors.pink);

  const ColorSeed(this.label, this.color);
  final String label;
  final Color color;
}

const Map<String, Color> colorSeedMap = {
  'M3 Baseline': Color(0xff6750a4),
  'Indigo': Colors.indigo,
  'Blue': Colors.blue,
  'Teal': Colors.teal,
  'Green': Colors.green,
  'Yellow': Colors.yellow,
  'Orange': Colors.orange,
  'Deep Orange': Colors.deepOrange,
  'Pink': Colors.pink
};

/// 浅色主题。
///
/// **必须是 getter**：`scaffoldBackgroundColor` 依赖
/// `AcrylicService.transparentBackground`，而后者会在运行时
/// 随毛玻璃开关变化。写成顶层变量的话只在启动时求值一次，
/// 关闭毛玻璃后主题仍停留在旧值，界面会变成黑灰。
ThemeData get lightTheme => ThemeData(
  colorSchemeSeed: ColorSeed.baseColor.color,
  useMaterial3: true,
  brightness: Brightness.light,
  textTheme: const TextTheme(
    bodyLarge: TextStyle(),
    bodyMedium: TextStyle(),
    bodySmall: TextStyle(),
    labelLarge: TextStyle(),
    labelMedium: TextStyle(),
    labelSmall: TextStyle(),
    titleLarge: TextStyle(),
    titleMedium: TextStyle(),
    // titleMedium: TextStyle(fontWeight: FontWeight.w600),
    titleSmall: TextStyle(),
  ).apply(fontFamily: 'LatoLato').useSystemChineseFont(Brightness.light),
  // 亚克力开启时给半透明浅色底，让窗口模糊层透出来；移动端/Web 走纯色
  // 材质生效时必须完全透明，否则 Flutter 的实色会盖住系统材质。
  // 非材质环境回退到实色。
  // 毛玻璃开启 → 透明（让系统材质透出）；
  // 关闭 → 实色（否则会露出窗口底色，浅色主题下显得发黑）
  scaffoldBackgroundColor: AcrylicService.transparentBackground
      ? Colors.transparent
      : const Color(0xFFFBF9FF),
  navigationRailTheme: const NavigationRailThemeData(
      backgroundColor: Color.fromRGBO(255, 251, 255, 1)),
  // 卡片统一观感：16px 圆角 + 细描边 + 极低阴影（移动端柔和现代）
  // Glassmorphism：半透明玻璃卡片，让底层 Acrylic 自然晕染
  cardTheme: CardThemeData(
    // 轻微抬升，配合 shadowColor 产生弥散阴影（玻璃"浮"起来）
    elevation: 2,
    shadowColor: Colors.black.withValues(alpha: 0.05),
    // 材质生效时用极低透明白；否则回退到接近实色的浅白
    color: AcrylicService.transparentBackground
        ? Colors.white.withValues(alpha: 0.35)
        : Colors.white.withValues(alpha: 0.92),
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(14),
      // 玻璃高光边框：边缘一圈半透明白，制造"厚度感"
      side: BorderSide(
        color: Colors.white.withValues(alpha: 0.5),
        width: 1,
      ),
    ),
    margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
  ),
);

// AMOLED 纯黑深色主题：设置页可切换（OLED 省电 + 对比度拉满）
ThemeData get amoledDarkTheme => darkTheme.copyWith(
  scaffoldBackgroundColor: AcrylicService.transparentBackground
      ? Colors.transparent
      : Colors.black,
  navigationRailTheme: const NavigationRailThemeData(
      backgroundColor: Colors.black),
  cardTheme: darkTheme.cardTheme.copyWith(
    color: const Color(0xFF121212),
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(16),
      side: BorderSide(color: Colors.white.withValues(alpha: 0.08)),
    ),
  ),
);

ThemeData get darkTheme => ThemeData(
  useMaterial3: true,
  colorSchemeSeed: ColorSeed.baseColor.color,
  brightness: Brightness.dark,
  textTheme: const TextTheme(
    bodyLarge: TextStyle(),
    bodyMedium: TextStyle(),
    bodySmall: TextStyle(),
    labelLarge: TextStyle(),
    labelMedium: TextStyle(),
    labelSmall: TextStyle(),
    titleLarge: TextStyle(),
    titleMedium: TextStyle(),
    titleSmall: TextStyle(),
  ).apply(fontFamily: 'LatoLato').useSystemChineseFont(Brightness.dark),
  scaffoldBackgroundColor: AcrylicService.transparentBackground
      ? Colors.transparent
      : const Color.fromRGBO(49, 48, 51, 1),
  navigationRailTheme: const NavigationRailThemeData(
      backgroundColor: Color.fromRGBO(49, 48, 51, 1)),
  // Glassmorphism（深色）：半透明黑 + 白色高光边框
  cardTheme: CardThemeData(
    elevation: 2,
    shadowColor: Colors.black.withValues(alpha: 0.18),
    // 深色下卡片要比浅色更实（0.30 → 0.55）：
    // acrylic 会把壁纸的亮色透上来，卡片太透会让白字失去对比度
    color: AcrylicService.transparentBackground
        ? Colors.black.withValues(alpha: 0.55)
        : const Color(0xFF2A2A2E),
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(14),
      side: BorderSide(color: Colors.white.withValues(alpha: 0.18), width: 1),
    ),
    margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
  ),
);
