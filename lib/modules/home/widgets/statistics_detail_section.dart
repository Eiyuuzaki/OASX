import 'package:flutter/material.dart';
import 'package:get/get.dart';
import 'package:oasx/modules/home/models/script_statistics_models.dart';
import 'package:oasx/modules/home/widgets/statistics_formatters.dart';
import 'package:oasx/translation/i18n_content.dart';

/// Task execution detail list rendered below the chart.
///
/// Material 3 规范：
/// - 列表项：标准两行列表（bodyLarge 时间戳 + bodyMedium 状态）
/// - Leading：Avatar 式圆形容器（primaryContainer 底 + onPrimaryContainer 数字），
///   替代原先“徽章”感的实心圆
/// - Trailing：等宽数字（tabular figures）右对齐时长，数字逐位对齐
/// - 分隔：项间用间距替代硬横线；hover 态 surfaceContainerHigh
class ScriptStatisticsDetailSection extends StatelessWidget {
  /// 创建任务执行明细区。
  const ScriptStatisticsDetailSection({
    super.key,
    required this.taskName,
    required this.runs,
  });

  /// 图表当前聚焦的任务名。
  final String taskName;

  /// 该任务可见的执行记录。
  final List<ScriptTaskRunRecord> runs;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (taskName.trim().isEmpty) {
      return _DetailPlaceholder(label: I18n.homeStatsNoTaskSelected.tr);
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4),
          child: Text(
            '${I18n.homeStatsRunDetails.tr} · ${taskName.tr}',
            style: theme.textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        if (runs.isEmpty)
          _DetailPlaceholder(label: I18n.homeStatsTaskDetailEmpty.tr)
        else
          for (var index = 0; index < runs.length; index++) ...[
            if (index > 0) const SizedBox(height: 4),
            _RunTile(
              taskName: taskName,
              run: runs[index],
              serialNumber: runs.length - index,
            ),
          ],
      ],
    );
  }
}

/// 空状态提示。
class _DetailPlaceholder extends StatelessWidget {
  const _DetailPlaceholder({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 20),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.inbox_outlined, size: 18, color: scheme.outline),
          const SizedBox(width: 8),
          Flexible(
            child: Text(
              label,
              style: Theme.of(context)
                  .textTheme
                  .bodyMedium
                  ?.copyWith(color: scheme.onSurfaceVariant),
            ),
          ),
        ],
      ),
    );
  }
}

/// 单条执行记录（M3 两行列表项）。
class _RunTile extends StatelessWidget {
  const _RunTile({
    required this.taskName,
    required this.run,
    required this.serialNumber,
  });

  final String taskName;
  final ScriptTaskRunRecord run;
  final int serialNumber;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return _HoverBuilder(
      borderRadius: BorderRadius.circular(12),
      hoverColor: scheme.surfaceContainerHigh,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            // Leading avatar：浅色圆形容器 + 序号
            // （bg-primary-container / text-on-primary-container 的对应实现）
            Container(
              width: 32,
              height: 32,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: scheme.primaryContainer,
                shape: BoxShape.circle,
              ),
              child: Text(
                '$serialNumber',
                style: theme.textTheme.labelMedium?.copyWith(
                  color: scheme.onPrimaryContainer,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            const SizedBox(width: 12),
            // 两行内容：时间戳（bodyLarge）+ 状态（bodyMedium, onSurfaceVariant）
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    formatStatisticsClockTimeRange(run.startTime, run.endTime),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.bodyLarge?.copyWith(
                      fontFeatures: const [
                        FontFeature.tabularFigures(),
                      ],
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    _statusText(),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: scheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            // Trailing：等宽数字（tabular figures）右对齐时长
            Text(
              formatStatisticsDuration(run.durationSeconds),
              style: theme.textTheme.labelLarge?.copyWith(
                fontWeight: FontWeight.w700,
                color: scheme.onSurface,
                fontFeatures: const [
                  FontFeature.tabularFigures(),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// 第二行状态文案。
  String _statusText() {
    if (run.battleCount > 0) {
      return '${I18n.homeStatsBattleCount.tr} ${run.battleCount}'
          ' · ${I18n.homeStatsBattleAvgDuration.tr} '
          '${formatStatisticsDuration(run.battleAvgDurationSeconds)}';
    }
    return I18n.homeStatsNoBattle.tr;
  }
}

/// 悬停时套一层圆角背景的包装（hover 状态层）。
class _HoverBuilder extends StatefulWidget {
  const _HoverBuilder({
    required this.hoverColor,
    required this.borderRadius,
    required this.child,
  });

  final Color hoverColor;
  final BorderRadius borderRadius;
  final Widget child;

  @override
  State<_HoverBuilder> createState() => _HoverBuilderState();
}

class _HoverBuilderState extends State<_HoverBuilder> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 120),
        decoration: BoxDecoration(
          color: _hovering ? widget.hoverColor : Colors.transparent,
          borderRadius: widget.borderRadius,
        ),
        child: widget.child,
      ),
    );
  }
}