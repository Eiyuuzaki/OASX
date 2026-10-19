import 'package:flutter/material.dart';
import 'package:get/get.dart';
import 'package:get_storage/get_storage.dart';
import 'package:oasx/api/api_client.dart';
import 'package:oasx/modules/common/models/storage_key.dart';

class LocaleService extends GetxService {
  final _storage = GetStorage();
  final language = 'zh-CN'.obs;

  final fallbackLocale = const Locale('zh', 'CN');

  Locale get currentLocale => _loadLocaleFromStorage();

  @override
  void onInit() {
    language.value = _storage.read(StorageKey.language.name) ?? 'zh-CN';
    _updateLocale(language.value);
    super.onInit();
  }

  void switchLanguage(String lang) {
    language.value = lang;
    _storage.write(StorageKey.language.name, lang);
    _updateLocale(lang);
  }

  void _updateLocale(String lang) {
    final locale = switch (lang) {
      'zh-CN' => const Locale('zh', 'CN'),
      'en-US' => const Locale('en', 'US'),
      _ => fallbackLocale,
    };
    Get.updateLocale(locale);
  }

  Locale _loadLocaleFromStorage() {
    final code = _storage.read(StorageKey.language.name);
    if (code == null) return fallbackLocale;
    return Locale(code);
  }

  Future<void> refreshTransFromRemote() async {
    final additionalTrans = await ApiClient().getAdditionalTranslate();
    Get.appendTranslations(additionalTrans);

    // 任务名（Exploration → 探索 等）不在 assets/i18n 里，
    // 后端 /home/task_name_translate 从 zh-CN.json 过滤生成。
    // 统计图的横条标签用 `taskName.tr` 渲染，缺了这批词条会显示英文原类名，
    // 在固定宽度的标签栏里两行截断，视觉上就是"错位"。
    try {
      final taskNames = await ApiClient().getTaskNameTranslate();
      if (taskNames.isNotEmpty) {
        Get.appendTranslations({'zh-CN': taskNames});
      }
    } catch (_) {
      // 翻译失败不影响主流程：任务名回落为英文
    }

    _updateLocale(language.value);
  }
}
