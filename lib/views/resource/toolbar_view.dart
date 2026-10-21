part of resource;

/// 顶部工具条：Material 3 规范排列。
///
/// 结构（从上到下）：
/// ```
/// ┌──────────────────────────────────────┐
/// │ 行1: 角色下拉 ▾            (2)        │  ← MenuAnchor + DropdownMenuButton
/// │ 行2: [📅 10-19 ▾]      ● 未在采集     │  ← 日期(同一层级) + 状态圆点
/// │ 行3: 更新于 02:35:52        (⬇) (⟳)  │  ← 时间 + 圆形 IconButton
/// └──────────────────────────────────────┘
/// ```
///
/// 三行结构保留（演进史见 git）：任何窗口宽度都不溢出，
/// 行1 最坏 ≈ 角色名，行2 ≈ 230px，行3 时间可缩。
class _Toolbar extends StatelessWidget {
  const _Toolbar({required this.controller});

  final ResourceController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dates = controller.dates;
    final current = controller.selectedDate.value;
    final updatedAt = controller.day.value.updatedAt;

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 10, 12, 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // ---- 行1：角色（MenuAnchor 下拉按钮，带箭头 + 悬停反馈）----
          _RoleSelector(controller: controller),
          const SizedBox(height: 8),
          // ---- 行2：日期 + 状态 ----
          Row(
            children: [
              Flexible(
                child: _DateDropdown(
                  dates: dates,
                  current: current,
                  onChanged: controller.changeDate,
                ),
              ),
              const SizedBox(width: 12),
              _StatusChip(controller: controller),
            ],
          ),
          const SizedBox(height: 6),
          // ---- 行3：更新时间 + 操作按钮（同层级，圆形 IconButton）----
          Row(
            children: [
              Expanded(
                child: Text(
                  updatedAt.isEmpty
                      ? I18n.resource_updated_at.tr
                      : '${I18n.resource_updated_at.tr} $updatedAt',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              // 采集：进行中转圈、平时标准图标（M3 默认圆形 hover/按压底色）
              IconButton(
                tooltip: I18n.resource_collect_now.tr,
                onPressed: controller.collecting.value
                    ? null
                    : controller.collectNow,
                icon: controller.collecting.value
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.download_rounded, size: 20),
              ),
              const SizedBox(width: 4),
              IconButton(
                tooltip: I18n.resource_refresh.tr,
                onPressed:
                    controller.loading.value ? null : controller.reload,
                icon: const Icon(Icons.refresh_rounded, size: 20),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// 日期下拉（MenuAnchor + 胶囊按钮）。
class _DateDropdown extends StatelessWidget {
  const _DateDropdown({
    required this.dates,
    required this.current,
    required this.onChanged,
  });

  final List<String> dates;
  final String current;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (dates.isEmpty) {
      return Text(current, style: theme.textTheme.bodyMedium);
    }
    return MenuAnchor(
      alignmentOffset: const Offset(0, 4),
      menuChildren: [
        for (final d in dates)
          MenuItemButton(
            leadingIcon: d == current
                ? Icon(Icons.check_rounded,
                    size: 18, color: theme.colorScheme.primary)
                : null,
            onPressed: () => onChanged(d),
            child: Text(d),
          ),
      ],
      builder: (context, menu, _) {
        // 胶囊按钮：tonal 底 + 日历图标 + 箭头，InkWell 自带水波纹
        return TextButton.icon(
          style: TextButton.styleFrom(
            visualDensity: VisualDensity.compact,
            shape: const StadiumBorder(),
            backgroundColor: theme.colorScheme.surfaceContainerHigh,
            foregroundColor: theme.colorScheme.onSurface,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          ),
          onPressed: () => menu.isOpen ? menu.close() : menu.open(),
          icon: Icon(Icons.calendar_today_rounded,
              size: 16, color: theme.colorScheme.onSurfaceVariant),
          label: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 100),
                child: Text(
                  current.isEmpty ? '-' : current,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.labelLarge,
                ),
              ),
              Icon(Icons.arrow_drop_down_rounded,
                  size: 18, color: theme.colorScheme.onSurfaceVariant),
            ],
          ),
        );
      },
    );
  }
}