part of resource;

/// 单项资源的历史折线图弹窗。
///
/// 时间范围规则：
/// * 数据 ≤ 7 天：只有「最近 N 天」，N 就是全部天数
/// * 数据 > 7 天：默认最近 7 天，可切 7/30/90/全部，也可自选起止日期
class ResourceHistoryDialog extends StatelessWidget {
  const ResourceHistoryDialog({
    Key? key,
    required this.controllerTag,
    required this.title,
  }) : super(key: key);

  final String controllerTag;
  final String title;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Dialog(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 720, maxHeight: 560),
        child: GetX<ResourceHistoryController>(
          tag: controllerTag,
          builder: (c) {
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // 标题栏
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 8, 8),
                  child: Row(children: [
                    Expanded(
                      child: Text(title, style: theme.textTheme.titleMedium),
                    ),
                    IconButton(
                      onPressed: () => Get.back(),
                      icon: const Icon(Icons.close_rounded, size: 20),
                      tooltip: I18n.resource_close.tr,
                    ),
                  ]),
                ),
                const Divider(height: 1),
                _RangeBar(controller: c),
                const Divider(height: 1),
                Expanded(
                  child: c.loading.value && c.points.isEmpty
                      ? const Center(child: CircularProgressIndicator())
                      : c.points.isEmpty
                          ? Center(
                              child: Text(I18n.resource_no_history.tr,
                                  style: theme.textTheme.bodyMedium),
                            )
                          : _Chart(controller: c),
                ),
                _Footer(controller: c),
              ],
            );
          },
        ),
      ),
    );
  }
}

/// 范围选择条
class _RangeBar extends StatelessWidget {
  const _RangeBar({required this.controller});

  final ResourceHistoryController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final span = controller.span.value;
    final canPick = controller.canPickRange;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Wrap(
        spacing: 6,
        runSpacing: 6,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          for (final d in const [7, 30, 90])
            if (span == null || canPick || d <= (span.totalDays))
              ChoiceChip(
                label: Text('最近 $d 天'),
                selected: controller.selectedDays.value == d &&
                    controller.customStart.value.isEmpty,
                onSelected: canPick || d == controller.selectedDays.value
                    ? (_) => controller.selectDays(d)
                    : null,
                labelStyle: theme.textTheme.labelSmall,
              ),
          if (span != null && canPick)
            ActionChip(
              avatar: const Icon(Icons.date_range, size: 14),
              label: Text(
                controller.customStart.value.isEmpty
                    ? '自选范围'
                    : '${controller.customStart.value} ~ ${controller.customEnd.value}',
                style: theme.textTheme.labelSmall,
              ),
              onPressed: () => _pickRange(context),
            ),
          if (span != null)
            Padding(
              padding: const EdgeInsets.only(left: 4),
              child: Text(
                '共 ${span.totalDays} 天数据',
                style: theme.textTheme.labelSmall
                    ?.copyWith(color: theme.disabledColor),
              ),
            ),
        ],
      ),
    );
  }

  Future<void> _pickRange(BuildContext context) async {
    final span = controller.span.value;
    final now = DateTime.now();
    final first = span != null && span.first.isNotEmpty
        ? DateTime.tryParse(span.first) ?? now.subtract(const Duration(days: 30))
        : now.subtract(const Duration(days: 30));
    final last = span != null && span.last.isNotEmpty
        ? DateTime.tryParse(span.last) ?? now
        : now;

    final picked = await showDateRangePicker(
      context: context,
      firstDate: first,
      lastDate: last,
      initialDateRange: DateTimeRange(
        start: first,
        end: last,
      ),
      helpText: '选择时间范围',
      saveText: '确定',
    );
    if (picked == null) return;
    await controller.applyRange(
      _fmt(picked.start),
      _fmt(picked.end),
    );
  }

  static String _fmt(DateTime d) {
    final m = d.month.toString().padLeft(2, '0');
    final day = d.day.toString().padLeft(2, '0');
    return '${d.year}-$m-$day';
  }
}

/// 折线图本体
class _Chart extends StatelessWidget {
  const _Chart({required this.controller});

  final ResourceHistoryController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final points = controller.points;
    if (points.isEmpty) return const SizedBox.shrink();

    final spots = <FlSpot>[];
    for (var i = 0; i < points.length; i++) {
      spots.add(FlSpot(i.toDouble(), points[i].count.toDouble()));
    }
    final values = spots.map((s) => s.y).toList()..sort();
    final minY = values.first;
    final maxY = values.last;
    // 上下各留 8% 余量，避免线贴边
    final pad = (maxY - minY) * 0.08;
    final lo = (minY - pad).clamp(0.0, double.infinity);
    final hi = maxY + pad == lo ? lo + 1 : maxY + pad;

    // 横轴标签最多 7 个
    final step = math.max(1, (points.length / 7).ceil());

    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 12, 20, 8),
      child: LineChart(
        LineChartData(
          minY: lo,
          maxY: hi,
          minX: 0,
          maxX: (points.length - 1).toDouble(),
          gridData: FlGridData(
            show: true,
            drawVerticalLine: false,
            horizontalInterval: math.max((hi - lo) / 4, 1),
            getDrawingHorizontalLine: (v) => FlLine(
              color: theme.dividerColor.withValues(alpha: 0.25),
              strokeWidth: 1,
            ),
          ),
          borderData: FlBorderData(show: false),
          titlesData: FlTitlesData(
            topTitles:
                const AxisTitles(sideTitles: SideTitles(showTitles: false)),
            rightTitles:
                const AxisTitles(sideTitles: SideTitles(showTitles: false)),
            leftTitles: AxisTitles(
              sideTitles: SideTitles(
                showTitles: true,
                reservedSize: 58,
                getTitlesWidget: (value, meta) => Text(
                  ResourceItemModel.formatQuantity(value.round()),
                  style: theme.textTheme.labelSmall?.copyWith(fontSize: 9),
                ),
              ),
            ),
            bottomTitles: AxisTitles(
              sideTitles: SideTitles(
                showTitles: true,
                reservedSize: 26,
                interval: 1,
                getTitlesWidget: (value, meta) {
                  final i = value.toInt();
                  if (i < 0 || i >= points.length || i % step != 0) {
                    return const SizedBox.shrink();
                  }
                  // 只显示 MM-DD，省空间
                  final d = points[i].date;
                  final short = d.length >= 10 ? d.substring(5) : d;
                  return Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(short,
                        style:
                            theme.textTheme.labelSmall?.copyWith(fontSize: 9)),
                  );
                },
              ),
            ),
          ),
          lineTouchData: LineTouchData(
            touchTooltipData: LineTouchTooltipData(
              getTooltipItems: (touched) => touched.map((s) {
                final i = s.x.toInt();
                final d = (i >= 0 && i < points.length) ? points[i].date : '';
                return LineTooltipItem(
                  '$d\n${ResourceItemModel.formatQuantity(s.y.round())}',
                  TextStyle(
                    color: theme.colorScheme.onInverseSurface,
                    fontSize: 11,
                  ),
                );
              }).toList(),
            ),
          ),
          lineBarsData: [
            LineChartBarData(
              spots: spots,
              isCurved: false,
              barWidth: 2,
              color: theme.colorScheme.primary,
              dotData: FlDotData(
                show: points.length <= 40,
                getDotPainter: (spot, percent, bar, index) =>
                    FlDotCirclePainter(
                  radius: 2.5,
                  color: theme.colorScheme.primary,
                  strokeWidth: 0,
                ),
              ),
              belowBarData: BarAreaData(
                show: true,
                color: theme.colorScheme.primary.withValues(alpha: 0.12),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// 底部：首末值与变化量
class _Footer extends StatelessWidget {
  const _Footer({required this.controller});

  final ResourceHistoryController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final points = controller.points;
    if (points.isEmpty) return const SizedBox.shrink();

    final first = points.first;
    final last = points.last;
    final diff = last.count - first.count;
    final positive = diff >= 0;
    final color = positive ? const Color(0xFF2E9E5B) : const Color(0xFFD0453B);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        border: Border(
          top: BorderSide(color: theme.dividerColor.withValues(alpha: 0.4)),
        ),
      ),
      child: Row(children: [
        Text('${first.date} 起 ${ResourceItemModel.formatQuantity(first.count)}',
            style: theme.textTheme.labelSmall),
        const SizedBox(width: 12),
        Text('→', style: theme.textTheme.labelSmall),
        const SizedBox(width: 12),
        Text('${last.date} 止 ${ResourceItemModel.formatQuantity(last.count)}',
            style: theme.textTheme.labelSmall),
        const Spacer(),
        Text(
          '${positive ? '+' : ''}${ResourceItemModel.formatQuantity(diff)}',
          style: theme.textTheme.labelMedium
              ?.copyWith(color: color, fontWeight: FontWeight.w600),
        ),
        const SizedBox(width: 6),
        Text('（${points.length} 个采样日）',
            style: theme.textTheme.labelSmall
                ?.copyWith(color: theme.disabledColor)),
      ]),
    );
  }
}
