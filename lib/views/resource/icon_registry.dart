part of resource;

/// 已放置的图标清单（从打包资源里动态读取）。
///
/// 只展示「本地有图标」的资源：这是刻意的产品决定——资源共有 190+ 项，
/// 绝大多数是活动代币/过期材料，用户只关心自己关心那几项。
/// 想增删展示项，**只要往 `assets/currency/` 放/删图片即可**，不用改代码。
///
/// 实现用 `AssetManifest` 而不是硬编码列表：新增图标后重新构建就生效。
class IconRegistry {
  IconRegistry._();

  static final IconRegistry instance = IconRegistry._();

  /// 已规范化的图标 basename（不含目录与扩展名），如 `icon_jinbi`
  final Set<String> _available = <String>{};

  bool _loaded = false;
  Future<void>? _loading;

  bool get loaded => _loaded;
  int get count => _available.length;

  /// 读取 `assets/currency/` 下所有图片的 basename。
  Future<void> ensureLoaded() {
    if (_loaded) return Future.value();
    return _loading ??= _load();
  }

  Future<void> _load() async {
    try {
      final manifest = await AssetManifest.loadFromAssetBundle(rootBundle);
      for (final path in manifest.listAssets()) {
        if (!path.startsWith('assets/currency/')) continue;
        final name = path.split('/').last;
        final base = stripImageExt(name);
        if (base.isNotEmpty && base.toLowerCase() != 'readme') {
          _available.add(base);
        }
      }
    } catch (e) {
      printError(info: 'IconRegistry.load: $e');
    } finally {
      _loaded = true;
      _loading = null;
    }
  }

  /// 该图标是否可用（`icon` 是后端下发的原始值，可能是 `icon_jinbi` 或 `xxx.png`）
  bool has(String icon) {
    final base = stripImageExt(icon);
    return base.isNotEmpty && _available.contains(base);
  }

  /// 是否一个图标都没有（用于区分「没放图标」与「有图标但当天无数据」）
  bool get isEmpty => _available.isEmpty;
}
