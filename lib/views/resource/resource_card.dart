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
///
/// **置顶**：右上角图钉按钮，钉住后该资产永远排在最前，
/// 卡片同时加上主题色描边 + 图钉角标，与普通项明显区分。
class ResourceCard extends StatelessWidget {
  const ResourceCard({
    Key? key,
    required this.item,
    this.onTap,
    this.pinned = false,
    this.onTogglePin,
  }) : super(key: key);

  final ResourceItemModel item;
  final VoidCallback? onTap;

  /// 是否已置顶
  final bool pinned;

  /// 切换置顶（为 null 时不显示图钉按钮）
  final VoidCallback? onTogglePin;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    // 置顶项用主题色描边 + 略高的背景不透明度，视觉上明显"浮"出来
    final borderColor = pinned
        ? theme.colorScheme.primary.withValues(alpha: 0.75)
        : theme.dividerColor.withValues(alpha: 0.3);
    final bgColor = pinned
        ? theme.colorScheme.primaryContainer.withValues(alpha: 0.28)
        : theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.45);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.fromLTRB(10, 8, 6, 8),
        decoration: BoxDecoration(
          color: bgColor,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: borderColor,
            width: pinned ? 1.5 : 1,
          ),
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
                  Row(
                    children: [
                      if (pinned) ...[
                        Icon(Icons.push_pin_rounded,
                            size: 12, color: theme.colorScheme.primary),
                        const SizedBox(width: 3),
                      ],
                      Flexible(
                        child: Text(item.name,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: theme.textTheme.bodySmall?.copyWith(
                              fontWeight:
                                  pinned ? FontWeight.w600 : FontWeight.normal,
                              color: pinned ? theme.colorScheme.primary : null,
                            )),
                      ),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text(item.displayCount,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w600)),
                ],
              ),
            ),
            // 增量数值与图钉按钮分开，避免挤在一起
            if (item.hasDelta) ...[
              const SizedBox(width: 6),
              _delta(theme),
            ],
            if (onTogglePin != null) ...[
              const SizedBox(width: 4),
              _pinButton(context),
            ],
          ],
        ),
      ),
    );
  }

  /// 图钉按钮：置顶后填充显示，未置顶时半透明描边
  Widget _pinButton(BuildContext context) {
    final theme = Theme.of(context);
    return IconButton(
      onPressed: onTogglePin,
      visualDensity: VisualDensity.compact,
      padding: EdgeInsets.zero,
      constraints: const BoxConstraints.tightFor(width: 26, height: 26),
      tooltip: pinned ? '取消置顶' : '置顶',
      icon: Icon(
        pinned ? Icons.push_pin : Icons.push_pin_outlined,
        size: 15,
        color: pinned
            ? theme.colorScheme.primary
            : theme.disabledColor.withValues(alpha: 0.6),
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
    return ConstrainedBox(
      // 限宽：增量可能是 +1,234,567 这样的长数字，
      // 不限制会把图钉按钮挤出卡片
      constraints: const BoxConstraints(maxWidth: 88),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Text(
            item.displayDelta,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: theme.textTheme.bodySmall
                ?.copyWith(color: color, fontWeight: FontWeight.w600),
          ),
          Text(
            item.deltaFrom == 'yesterday'
                ? I18n.resource_vs_yesterday.tr
                : I18n.resource_vs_earlier.tr,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: theme.textTheme.labelSmall
                ?.copyWith(color: theme.disabledColor, fontSize: 10),
          ),
        ],
      ),
    );
  }
}
