part of resource;

/// 顶部工具条：**三行结构**，任何窗口宽度都不溢出。
///
/// 演进史：
/// - 单行（角色+日期+状态+时间+2按钮）：需求 628px > 侧栏 560px，溢出；
/// - 两行（身份 / 状态+时间+按钮）：最大化 OK，但行2 的 258px 在
///   移动端/窄窗口（<260px 可用）仍放不下；
/// - **三行（身份 / 状态 / 时间+按钮）**：每行最坏 ≤230px，
///   覆盖移动端最小宽度，数学上不可能溢出。
///
/// ```
/// ┌──────────────────────────────┐
/// │ 👤 英雄崎凛 (Lv60) (2)▾       │  ← 行1 身份（含服务器名，见 _RoleSelector）
/// │ 📅 [10-19 ▾]   ● 自动采集中   │  ← 行2 日期 + 状态
/// │ 更新于 02:35:52      [⬇][⟳]  │  ← 行3 时间 + 按钮
/// └──────────────────────────────┘
/// ```
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
      padding: const EdgeInsets.fromLTRB(10, 6, 8, 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // ---- 行1：身份（角色选择，含服务器名）----
          _RoleSelector(controller: controller),
          const SizedBox(height: 2),
          // ---- 行2：日期 + 采集状态 ----
          Row(
            children: [
              Flexible(
                child: _DateDropdown(
                  dates: dates,
                  current: current,
                  onChanged: controller.changeDate,
                ),
              ),
              const SizedBox(width: 10),
              _StatusChip(controller: controller),
            ],
          ),
          // ---- 行3：更新时间 + 按钮 ----
          Row(
            children: [
              Expanded(
                child: Text(
                  updatedAt.isEmpty
                      ? I18n.resource_updated_at.tr
                      : '${I18n.resource_updated_at.tr} $updatedAt',
                  style: theme.textTheme.labelSmall
                      ?.copyWith(color: theme.disabledColor),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              IconButton(
                visualDensity: VisualDensity.compact,
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
              IconButton(
                visualDensity: VisualDensity.compact,
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

/// 日期下拉。
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
    return DropdownButton<String>(
      isDense: true,
      value: dates.contains(current) ? current : null,
      hint: Text(current,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: theme.textTheme.bodySmall),
      items: dates
          .map((d) => DropdownMenuItem(
              value: d,
              child: Text(d,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.bodySmall)))
          .toList(),
      onChanged: (v) {
        if (v != null) onChanged(v);
      },
    );
  }
}
