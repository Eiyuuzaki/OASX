import 'package:flutter/material.dart';
import 'package:get/get.dart';
import 'package:oasx/modules/home/controllers/statistics_controller.dart';
import 'package:oasx/modules/home/models/script_statistics_models.dart';
import 'package:oasx/modules/home/widgets/statistics_detail_section.dart';
import 'package:oasx/modules/home/widgets/statistics_formatters.dart';
import 'package:oasx/modules/home/widgets/statistics_history_chart.dart';
import 'package:oasx/translation/i18n_content.dart';

const double _kGap = 12;
const double _kPagePadding = 16;

/// 当前脚本「统计」标签页的内容。
///
/// 用 Material 3 的标准组件搭：概览用 `Card.filled` 指标块，
/// 筛选用 `MenuAnchor` 菜单，明细用 `ListTile` + `Divider`。
class ScriptStatisticsPanel extends StatelessWidget {
  /// 创建统计面板。
  const ScriptStatisticsPanel({super.key});

  /// 持有统计状态的控制器。
  HomeStatisticsController get controller =>
      Get.find<HomeStatisticsController>();

  @override
  Widget build(BuildContext context) {
    return Obx(() {
      final statistics = controller.statistics.value;
      if (statistics == null) {
        return _StatisticsPlaceholder(
          label: _placeholderLabel(),
          message: controller.lastErrorMessage.value,
          loading: _placeholderIsLoading(),
        );
      }
      return ListView(
        padding: const EdgeInsets.all(_kPagePadding),
        children: [
          _SummaryTiles(statistics: statistics),
          const SizedBox(height: _kGap),
          _StatsToolbar(
            availableDateKeys:
                controller.availableDateKeys.toList(growable: false),
            controller: controller,
            canSortByTime: controller.canSortByTime,
          ),
          const SizedBox(height: _kGap),
          _ChartCard(
            controller: controller,
            entries: controller.historyTaskEntries,
            detailRuns: controller.selectedHistoryDetailRuns,
          ),
        ],
      );
    });
  }

  /// 占位标题。
  String _placeholderLabel() {
    if (controller.datesLoading.value || controller.statisticsLoading.value) {
      return I18n.homeStatsLoadingMessage.tr;
    }
    if (controller.availableDateKeys.isEmpty) {
      return controller.lastErrorMessage.value.isEmpty
          ? I18n.homeStatsHistoryDateEmpty.tr
          : I18n.error.tr;
    }
    if (controller.isTodaySelected) {
      return switch (controller.connectionState.value) {
        ScriptStatisticsConnectionState.connecting =>
          I18n.homeStatsLoadingMessage.tr,
        ScriptStatisticsConnectionState.reconnecting =>
          I18n.homeStatsReconnecting.tr,
        ScriptStatisticsConnectionState.error => I18n.homeStatsStreamError.tr,
        _ => I18n.homeStatsWaitingSnapshot.tr,
      };
    }
    return controller.lastErrorMessage.value.isEmpty
        ? I18n.homeStatsChartEmpty.tr
        : I18n.error.tr;
  }

  /// 占位处是否显示进度圈。
  bool _placeholderIsLoading() {
    if (controller.datesLoading.value || controller.statisticsLoading.value) {
      return true;
    }
    return controller.isTodaySelected &&
        controller.connectionState.value !=
            ScriptStatisticsConnectionState.error &&
        controller.availableDateKeys.isNotEmpty;
  }
}

/// 顶部三个概览指标块。
class _SummaryTiles extends StatelessWidget {
  const _SummaryTiles({required this.statistics});

  final ScriptStatisticsDay statistics;

  @override
  Widget build(BuildContext context) {
    return Row(
      // 注意：不能使用 CrossAxisAlignment.stretch —— 在 ListView 的无界
      // 高度里会让整行塌陷为 0 高且不报异常（统计页空白 bug 的根因）。
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: _StatTile(
            icon: Icons.schedule_rounded,
            label: I18n.homeStatsTotalDuration.tr,
            value: formatStatisticsDuration(statistics.totalRuntimeSeconds),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _StatTile(
            icon: Icons.playlist_play_rounded,
            label: I18n.homeStatsMetricRunCount.tr,
            value: '${statistics.totalTaskRunCount}',
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _StatTile(
            icon: Icons.sports_martial_arts_rounded,
            label: I18n.homeStatsMetricBattleCount.tr,
            value: '${statistics.totalBattleCount}',
          ),
        ),
      ],
    );
  }
}

/// 单个指标块（M3 filled card）。
class _StatTile extends StatelessWidget {
  const _StatTile({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    return Card.filled(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                Icon(icon, size: 16, color: scheme.primary),
                const SizedBox(width: 6),
                Flexible(
                  child: Text(
                    label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.labelMedium?.copyWith(
                      color: scheme.onSurfaceVariant,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Text(
                value,
                maxLines: 1,
                style: theme.textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// 筛选工具条：日期、数据流状态、指标、排序。
class _StatsToolbar extends StatelessWidget {
  const _StatsToolbar({
    required this.availableDateKeys,
    required this.controller,
    required this.canSortByTime,
  });

  final List<String> availableDateKeys;
  final HomeStatisticsController controller;
  final bool canSortByTime;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    return Card.outlined(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(8, 8, 8, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Flexible(
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: _MenuSelect<String>(
                      icon: Icons.calendar_today_rounded,
                      label: formatStatisticsDayLabel(
                        controller.selectedDateKey.value,
                      ),
                      value: controller.selectedDateKey.value,
                      options: [
                        for (final dateKey in availableDateKeys)
                          _MenuOption(
                            value: dateKey,
                            label: formatStatisticsDayLabel(dateKey),
                          ),
                      ],
                      onChanged: controller.selectHistoryDate,
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                _StatusIndicator(controller: controller),
                const SizedBox(width: 8),
                IconButton.filledTonal(
                  tooltip: controller.historySortDescending.value
                      ? I18n.homeStatsSortByData.tr
                      : I18n.homeStatsSortByTime.tr,
                  onPressed: controller.toggleHistorySortDirection,
                  icon: Icon(
                    controller.historySortDescending.value
                        ? Icons.arrow_downward_rounded
                        : Icons.arrow_upward_rounded,
                    size: 20,
                  ),
                ),
              ],
            ),
            Divider(height: 8, color: scheme.outlineVariant),
            // Wrap：窄侧栏下自动换行，不会横向溢出
            Wrap(
              spacing: 4,
              runSpacing: 4,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                _MenuSelect<ScriptStatisticsChartMetric>(
                  icon: Icons.bar_chart_rounded,
                  label: _metricLabel(controller.historyMetric.value),
                  value: controller.historyMetric.value,
                  options: [
                    for (final metric in ScriptStatisticsChartMetric.values)
                      _MenuOption(value: metric, label: _metricLabel(metric)),
                  ],
                  onChanged: controller.selectHistoryMetric,
                ),
                _MenuSelect<ScriptStatisticsChartSortField>(
                  icon: Icons.sort_rounded,
                  label: _sortFieldLabel(controller.historySortField.value),
                  value: controller.historySortField.value,
                  options: [
                    _MenuOption(
                      value: ScriptStatisticsChartSortField.data,
                      label: I18n.homeStatsSortByData.tr,
                    ),
                    if (canSortByTime)
                      _MenuOption(
                        value: ScriptStatisticsChartSortField.time,
                        label: I18n.homeStatsSortByTime.tr,
                      ),
                  ],
                  onChanged: controller.selectHistorySortField,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  /// 指标下拉的文案。
  String _metricLabel(ScriptStatisticsChartMetric metric) {
    return switch (metric) {
      ScriptStatisticsChartMetric.totalDuration =>
        I18n.homeStatsTotalDuration.tr,
      ScriptStatisticsChartMetric.runCount => I18n.homeStatsMetricRunCount.tr,
      ScriptStatisticsChartMetric.battleCount =>
        I18n.homeStatsMetricBattleCount.tr,
      ScriptStatisticsChartMetric.battleAvgDuration =>
        I18n.homeStatsMetricBattleAvgDuration.tr,
      ScriptStatisticsChartMetric.avgRunDuration =>
        I18n.homeStatsMetricAvgRunDuration.tr,
    };
  }

  /// 排序字段下拉的文案。
  String _sortFieldLabel(ScriptStatisticsChartSortField field) {
    return switch (field) {
      ScriptStatisticsChartSortField.data => I18n.homeStatsSortByData.tr,
      ScriptStatisticsChartSortField.time => I18n.homeStatsSortByTime.tr,
    };
  }
}

/// 实时流状态指示（仅「今天」+ 存在连接状态时出现）。
///
/// 历史日期的数据是静态的（后端解析本地日志一次性返回），
/// 没有需要用户知晓的持续状态，因此不显示任何指示器——
/// 原先那里的 cloud_done「云端已同步」语义是错的：数据从不上云。
class _StatusIndicator extends StatelessWidget {
  const _StatusIndicator({required this.controller});

  final HomeStatisticsController controller;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (!_shouldShow) {
      return const SizedBox.shrink();
    }
    return Tooltip(
      message: _label(),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4),
        child: Icon(_icon(), size: 20, color: _tone(scheme)),
      ),
    );
  }

  /// 只有「今天」且连接状态值得报告时才显示。
  bool get _shouldShow {
    if (controller.lastErrorMessage.value.isNotEmpty) {
      return true;
    }
    if (!controller.isTodaySelected) {
      return false;
    }
    return switch (controller.connectionState.value) {
      ScriptStatisticsConnectionState.connected => true,
      ScriptStatisticsConnectionState.reconnecting => true,
      ScriptStatisticsConnectionState.error => true,
      _ => false,
    };
  }

  String _label() {
    if (controller.lastErrorMessage.value.isNotEmpty) {
      return I18n.homeStatsStreamError.tr;
    }
    return switch (controller.connectionState.value) {
      ScriptStatisticsConnectionState.reconnecting =>
        I18n.homeStatsReconnecting.tr,
      ScriptStatisticsConnectionState.error => I18n.homeStatsStreamError.tr,
      _ => '',
    };
  }

  IconData _icon() {
    if (controller.lastErrorMessage.value.isNotEmpty) {
      return Icons.error_outline_rounded;
    }
    return switch (controller.connectionState.value) {
      ScriptStatisticsConnectionState.connected => Icons.wifi_tethering_rounded,
      ScriptStatisticsConnectionState.reconnecting => Icons.sync_rounded,
      ScriptStatisticsConnectionState.error => Icons.error_outline_rounded,
      _ => Icons.hourglass_top_rounded,
    };
  }

  Color _tone(ColorScheme scheme) {
    if (controller.lastErrorMessage.value.isNotEmpty ||
        controller.connectionState.value ==
            ScriptStatisticsConnectionState.error) {
      return scheme.error;
    }
    if (controller.connectionState.value ==
        ScriptStatisticsConnectionState.connected) {
      return scheme.primary;
    }
    return scheme.onSurfaceVariant;
  }
}

/// 菜单里的一项。
class _MenuOption<T> {
  const _MenuOption({required this.value, required this.label});

  final T value;
  final String label;
}

/// 基于 `MenuAnchor` 的下拉选择（Material 标准交互）。
class _MenuSelect<T> extends StatelessWidget {
  const _MenuSelect({
    required this.icon,
    required this.label,
    required this.value,
    required this.options,
    required this.onChanged,
  });

  final IconData icon;
  final String label;
  final T value;
  final List<_MenuOption<T>> options;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) {
    if (options.isEmpty) {
      return const SizedBox.shrink();
    }
    final scheme = Theme.of(context).colorScheme;
    return MenuAnchor(
      menuChildren: [
        for (final option in options)
          MenuItemButton(
            onPressed: () => onChanged(option.value),
            child: Row(
              children: [
                Expanded(
                  child: Text(option.label, overflow: TextOverflow.ellipsis),
                ),
                if (option.value == value) ...[
                  const SizedBox(width: 8),
                  Icon(Icons.check_rounded, size: 18, color: scheme.primary),
                ],
              ],
            ),
          ),
      ],
      builder: (context, menu, _) {
        return TextButton.icon(
          onPressed: () => menu.isOpen ? menu.close() : menu.open(),
          icon: Icon(icon, size: 18),
          label: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 120),
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const Icon(Icons.arrow_drop_down_rounded, size: 18),
            ],
          ),
        );
      },
    );
  }
}

/// 图表 + 任务明细卡片。
class _ChartCard extends StatelessWidget {
  const _ChartCard({
    required this.controller,
    required this.entries,
    required this.detailRuns,
  });

  final HomeStatisticsController controller;
  final List<MapEntry<String, ScriptTaskStatistics>> entries;
  final List<ScriptTaskRunRecord> detailRuns;

  @override
  Widget build(BuildContext context) {
    return Obx(() {
      final loading = controller.historyChartLoading.value;
      return Card.outlined(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (entries.isEmpty)
                const _InlineEmpty(icon: Icons.bar_chart_rounded)
              else ...[
                if (loading)
                  const LinearProgressIndicator(minHeight: 2),
                Opacity(
                  opacity: loading ? 0.4 : 1,
                  child: IgnorePointer(
                    ignoring: loading,
                    child: ScriptStatisticsHistoryChart(
                      entries: entries,
                      metric: controller.historyMetric.value,
                      focusedTaskName: controller.selectedTaskName.value,
                      onSelectTask: controller.selectHistoryTask,
                      flashingTaskName: controller.latestUpdatedTaskName.value,
                      flashToken: controller.latestUpdatedTaskPulse.value,
                      highlightRealtimeTask: controller.isTodaySelected,
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 12),
              ScriptStatisticsDetailSection(
                taskName: controller.selectedTaskName.value,
                runs: detailRuns,
              ),
            ],
          ),
        ),
      );
    });
  }
}

/// 卡片内的空状态。
class _InlineEmpty extends StatelessWidget {
  const _InlineEmpty({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 28),
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 32, color: scheme.outline),
            const SizedBox(height: 8),
            Text(
              I18n.homeStatsChartEmpty.tr,
              style: Theme.of(context)
                  .textTheme
                  .bodyMedium
                  ?.copyWith(color: scheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }
}

/// 整页占位（无数据 / 加载中）。
class _StatisticsPlaceholder extends StatelessWidget {
  const _StatisticsPlaceholder({
    required this.label,
    this.message = '',
    this.loading = false,
  });

  final String label;
  final String message;
  final bool loading;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (loading)
              const Padding(
                padding: EdgeInsets.only(bottom: 16),
                child: SizedBox(
                  width: 28,
                  height: 28,
                  child: CircularProgressIndicator(strokeWidth: 2.6),
                ),
              )
            else
              Padding(
                padding: const EdgeInsets.only(bottom: 16),
                child: Icon(Icons.insights_rounded,
                    size: 36, color: scheme.outline),
              ),
            Text(
              label,
              style: theme.textTheme.titleMedium,
              textAlign: TextAlign.center,
            ),
            if (message.trim().isNotEmpty) ...[
              const SizedBox(height: 8),
              Text(
                message,
                textAlign: TextAlign.center,
                style: theme.textTheme.bodySmall
                    ?.copyWith(color: scheme.onSurfaceVariant),
              ),
            ],
          ],
        ),
      ),
    );
  }
}