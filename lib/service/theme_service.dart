import 'package:flutter/material.dart';
import 'package:get/get.dart';
import 'package:get_storage/get_storage.dart';
import 'package:oasx/config/theme.dart';
import 'package:oasx/modules/common/models/storage_key.dart';

class ThemeService extends GetxService {
  final _storage = GetStorage();
  final _dark = false.obs;
  final _amoled = false.obs;
  final _color = ColorSeed.baseColor.color.obs;

  bool get isDarkMode => _dark.value;

  /// AMOLED 纯黑深色：仅深色模式下有意义（OLED 省电 + 对比度拉满）
  bool get isAmoled => _amoled.value;

  Color get color => _color.value;

  ThemeMode get themeMode => isDarkMode ? ThemeMode.dark : ThemeMode.light;

  @override
  void onInit() {
    _dark.value = _storage.read(StorageKey.dark.name) ?? false;
    _amoled.value = _storage.read('amoled') ?? false;
    switchTheme(_dark.value);
    super.onInit();
  }

  void switchTheme([bool? dark]) {
    _dark.value = dark ?? !_dark.value;
    _storage.write(StorageKey.dark.name, _dark.value);
    Get.changeThemeMode(themeMode);
  }

  /// 切换 AMOLED 纯黑；仅在深色模式下实际改变观感
  void switchAmoled([bool? value]) {
    _amoled.value = value ?? !_amoled.value;
    _storage.write('amoled', _amoled.value);
    // darkTheme 实例需要重建，GetMaterialApp 整体刷新
    Get.forceAppUpdate();
  }
}
