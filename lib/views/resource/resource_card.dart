part of resource;

/// 去掉图片扩展名。游戏配置表里的 `icon` 有两种写法：
/// `icon_jinbi` 与 `icon_longcheranliao.png`，统一成 basename 便于比较/拼路径。
String stripImageExt(String icon) {
  var name = icon.trim();
  if (name.contains('/')) {
    name = name.split('/').last;
  }
  final lower = name.toLowerCase();
  for (final ext in const ['.png', '.jpg', '.jpeg', '.webp']) {
    if (lower.endsWith(ext)) {
      return name.substring(0, name.length - ext.length);
    }
  }
  return name;
}

/// 把游戏下发的图标名规范成资源路径。
///
/// 返回 `null` 表示该项没有图标名（走占位）。
String? resourceIconAsset(String icon) {
  final base = stripImageExt(icon);
  if (base.isEmpty) return null;
  return 'assets/currency/$base.png';
}

/// 单个资源卡片：图标 + 名称 + 数量 + 增量。
///
/// 图标放在 `OASX/assets/currency/`，文件名 = 游戏配置表里的 `icon` 字段（补 `.png`）。
/// 找不到时退化为「首字 + 稳定配色」的圆形占位，保证任何情况下都能看。
class ResourceCard extends StatelessWidget {
  const ResourceCard({Key? key, required this.item, this.onTap}) : super(key: key);

  final ResourceItemModel item;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
        decoration: BoxDecoration(
          color: theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.45),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: theme.dividerColor.withValues(alpha: 0.3)),
        ),
        child: Row(
          children: [
            _icon(context),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(item.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.bodySmall),
                  const SizedBox(height: 2),
                  Text(item.displayCount,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w600)),
                ],
              ),
            ),
            if (item.hasDelta) _delta(theme),
          ],
        ),
      ),
    );
  }

  Widget _icon(BuildContext context) {
    final asset = resourceIconAsset(item.icon);
    if (asset == null) return _placeholder(context);
    return ClipRRect(
      borderRadius: BorderRadius.circular(8),
      child: Image.asset(
        asset,
        width: 34,
        height: 34,
        fit: BoxFit.cover,
        errorBuilder: (context, error, stack) => _placeholder(context),
      ),
    );
  }

  Widget _placeholder(BuildContext context) {
    // 用名称的稳定 hash 选色，保证同一资源每次颜色一致
    final palette = [
      const Color(0xFFE8A33D), const Color(0xFF4F9BE8), const Color(0xFF58B87A),
      const Color(0xFFB06FD6), const Color(0xFFE06A6A), const Color(0xFF3FBFC0),
    ];
    final color = palette[item.name.hashCode.abs() % palette.length];
    final ch = item.name.isEmpty ? '?' : item.name.substring(0, 1);
    return Container(
      width: 34,
      height: 34,
      alignment: Alignment.center,
      color: color.withValues(alpha: 0.18),
      child: Text(ch,
          style: TextStyle(
              color: color, fontSize: 16, fontWeight: FontWeight.bold)),
    );
  }

  Widget _delta(ThemeData theme) {
    final positive = (item.delta ?? 0) > 0;
    final color = positive ? const Color(0xFF2E9E5B) : const Color(0xFFD0453B);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(
          item.displayDelta,
          style: theme.textTheme.bodySmall
              ?.copyWith(color: color, fontWeight: FontWeight.w600),
        ),
        Text(
          item.deltaFrom == 'yesterday'
              ? I18n.resource_vs_yesterday.tr
              : I18n.resource_vs_earlier.tr,
          style: theme.textTheme.labelSmall
              ?.copyWith(color: theme.disabledColor, fontSize: 10),
        ),
      ],
    );
  }
}
