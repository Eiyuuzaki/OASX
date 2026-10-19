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

ThemeData lightTheme = ThemeData(
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
  scaffoldBackgroundColor: AcrylicService.transparentBackground
      ? const Color(0xD9FBFBFF) // 85% 不透明的浅色
      : const Color.fromRGBO(255, 251, 255, 1),
  navigationRailTheme: const NavigationRailThemeData(
      backgroundColor: Color.fromRGBO(255, 251, 255, 1)),
  // 卡片统一观感：16px 圆角 + 细描边 + 极低阴影（移动端柔和现代）
  cardTheme: CardThemeData(
    elevation: 0,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(16),
      side: BorderSide(
        color: Colors.black.withValues(alpha: 0.06),
      ),
    ),
    margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
  ),
);

// AMOLED 纯黑深色主题：设置页可切换（OLED 省电 + 对比度拉满）
ThemeData amoledDarkTheme = darkTheme.copyWith(
  scaffoldBackgroundColor: AcrylicService.transparentBackground
      ? const Color(0xE6000000) // 亚克力下也要接近纯黑
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

ThemeData darkTheme = ThemeData(
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
      ? const Color(0xD9313033) // 85% 不透明的深色
      : const Color.fromRGBO(49, 48, 51, 1),
  navigationRailTheme: const NavigationRailThemeData(
      backgroundColor: Color.fromRGBO(49, 48, 51, 1)),
);
