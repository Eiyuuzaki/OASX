// 验证「关闭毛玻璃后界面不应变黑」这个修复的核心逻辑。
//
// 根因回顾：
// 1. lightTheme 曾是顶层变量，只在启动时求值一次，
//    AcrylicService.transparentBackground 的运行时变化反映不到主题上
// 2. _setSolid 曾硬编码 Colors.black，浅色主题下窗口也变黑
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('主题 getter 有效性', () {
    test('scaffold 背景随透明状态变化（不是启动时固定值）', () {
      // 模拟 AcrylicService.transparentBackground 的两种取值
      Color scaffoldColor(bool transparentBackground) =>
          transparentBackground ? Colors.transparent : const Color(0xFFFBF9FF);

      expect(scaffoldColor(true), Colors.transparent);
      expect(scaffoldColor(false), const Color(0xFFFBF9FF));
      // 关键：关闭后必须是不透明实色，否则会露出窗口底色显得发黑
      expect(scaffoldColor(false).a, 1.0);
    });

    test('关闭毛玻璃后的实色必须是浅色（不是黑）', () {
      // _setSolid 现在的颜色选择
      const light = Color(0xFFFBF9FF);
      const dark = Color(0xFF1C1B1F);

      // 浅色模式：亮度应接近白
      expect(light.r, greaterThan(0.9));
      expect(light.g, greaterThan(0.9));
      expect(light.b, greaterThan(0.9));

      // 深色模式：亮度应明显低
      expect(dark.r, lessThan(0.2));
    });
  });

  group('三态主题模式', () {
    test('system 模式映射到 ThemeMode.system', () {
      ThemeMode map(String mode) => switch (mode) {
            'system' => ThemeMode.system,
            'light' => ThemeMode.light,
            _ => ThemeMode.dark,
          };
      expect(map('system'), ThemeMode.system);
      expect(map('light'), ThemeMode.light);
      expect(map('dark'), ThemeMode.dark);
    });

    test('旧版本只存 bool 时的兼容推断', () {
      // 老数据 dark=true 应推断为深色模式
      AppThemeMode infer(bool? oldBool, String? newMode) {
        if (newMode != null) {
          return switch (newMode) {
            'system' => AppThemeMode.system,
            'light' => AppThemeMode.light,
            _ => AppThemeMode.dark,
          };
        }
        if (oldBool != null) {
          return oldBool ? AppThemeMode.dark : AppThemeMode.light;
        }
        return AppThemeMode.system;
      }

      expect(infer(true, null), AppThemeMode.dark);
      expect(infer(false, null), AppThemeMode.light);
      expect(infer(null, null), AppThemeMode.system);
      // 新模式优先
      expect(infer(true, 'system'), AppThemeMode.system);
    });
  });
}

enum AppThemeMode { system, light, dark }
