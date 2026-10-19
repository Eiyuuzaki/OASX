import 'package:get/get.dart';

import 'package:oasx/api/api_client.dart';

/// 任务名 / 界面名的中文对照。
///
/// 后端 `module/config/i18n/zh-CN.json` 已经有全部任务名的翻译
/// （`Exploration → 探索`、`RealmRaid → 个人突破` …），
/// 这里拉一次缓存起来，供统计页等处使用。
///
/// 为什么不硬编码：任务有 50+ 个，还会随版本增删，
/// 拉后端现成的表避免两边不同步。
class NameTranslator {
  NameTranslator._();

  static final NameTranslator instance = NameTranslator._();

  final Map<String, String> _map = <String, String>{};
  bool _loaded = false;

  bool get loaded => _loaded;
  int get count => _map.length;

  /// 拉取对照表；失败时静默降级为「显示原名」。
  Future<void> ensureLoaded() async {
    if (_loaded) return;
    try {
      final map = await ApiClient().getTaskNameTranslate();
      _map
        ..clear()
        ..addAll(map);
    } catch (e) {
      printError(info: 'NameTranslator.load: $e');
    } finally {
      _loaded = true;
    }
  }

  /// 翻译：查不到就返回原名（绝不返回空串，避免界面出现空白）。
  String translate(String name) {
    if (name.isEmpty) return name;
    return _map[name] ?? name;
  }
}
