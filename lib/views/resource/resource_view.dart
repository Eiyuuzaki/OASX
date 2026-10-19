library resource;

import 'dart:async';
import 'dart:math' as math;

import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show AssetManifest, rootBundle;
import 'package:get/get.dart';
import 'package:get_storage/get_storage.dart';

import 'package:oasx/api/api_client.dart';
import 'package:oasx/modules/common/widgets/frosted_bars.dart';
import 'package:oasx/translation/i18n_content.dart';
import 'package:oasx/modules/home/controllers/dashboard_controller.dart';

part './icon_registry.dart';
part './resource_controller.dart';
part './resource_card.dart';
part './resource_history.dart';
part './toolbar_view.dart';

/// 「资产」页：按日展示游戏内资源，并与前一次采样 / 前一日对比。
///
/// **只展示本地有图标的资源**：资源共 190+ 项，绝大多数是活动代币/过期材料，
/// 全列出来没有意义。想增减展示项，往 `assets/currency/` 放/删图片即可。
class ResourceView extends StatelessWidget {
  const ResourceView({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    // 参考版架构：当前脚本名由 HomeDashboardController 维护
    final tag = Get.find<HomeDashboardController>().activeScriptName.value;
    // 每个脚本一个独立控制器，切换脚本时互不干扰
    if (!Get.isRegistered<ResourceController>(tag: tag)) {
      Get.put(ResourceController(name: tag), tag: tag, permanent: true);
    }

    // 用 GetX 而不是 GetBuilder：控制器内部改的是 Rx 值（selectedDate.value 等），
    // GetBuilder 只响应显式 update()，会导致切日期后界面不刷新。
    return GetX<ResourceController>(
      tag: tag,
      builder: (c) {
        if (c.loading.value && c.day.value.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Toolbar(controller: c),
            const Divider(height: 1),
            Expanded(child: _body(c)),
          ],
        );
      },
    );
  }

  /// 依次判断：没放图标 → 有图标但今天没数据 → 正常网格
  Widget _body(ResourceController c) {
    if (IconRegistry.instance.isEmpty) {
      return const _Hint(
        icon: Icons.image_outlined,
        text: '还没有配置图标。\n把图标放进 OASX/assets/currency/ 后重新构建。',
      );
    }
    if (c.day.value.isEmpty) {
      return _Empty(controller: c);
    }
    if (c.visibleItems.isEmpty) {
      return _Hint(
        icon: Icons.filter_alt_off_outlined,
        text: '这一天没有已配置图标的资源记录。\n'
            '共识别 ${IconRegistry.instance.count} 个图标，'
            '但当前快照里都不存在（可能尚未采集，或都是 0）。',
      );
    }
    return _Grid(controller: c);
  }
}

/// 纯提示页（无操作按钮）
class _Hint extends StatelessWidget {
  const _Hint({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 48, color: Theme.of(context).disabledColor),
          const SizedBox(height: 12),
          Text(text,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyMedium),
        ],
      ),
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.controller});

  final ResourceController controller;

  @override
  Widget build(BuildContext context) {
    final running = controller.collectorRunning.value;
    return Tooltip(
      message: running
          ? I18n.resource_collector_on.tr
          : I18n.resource_collector_off.tr,
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(Icons.circle,
            size: 10, color: running ? Colors.green : Colors.grey),
        const SizedBox(width: 4),
        Text(
          running
              ? I18n.resource_collector_on.tr
              : I18n.resource_collector_off.tr,
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ]),
    );
  }
}

/// 角色下拉：多账号时用来切换查看对象。
class _RoleSelector extends StatelessWidget {
  const _RoleSelector({required this.controller});

  final ResourceController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final roles = controller.roles;
    final current = controller.selectedRoleKey.value;
    final exists = roles.any((r) => r.roleKey == current);

    return Tooltip(
      message: I18n.resource_role_hint.tr,
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(Icons.person_outline,
            size: 18, color: theme.colorScheme.primary),
        const SizedBox(width: 4),
        // isDense + 限宽：displayLabel 可能很长（名字+等级+服务器），
        // 不约束时 DropdownButton 按最宽 item 撑开，把整行 Row 挤溢出，
        // 最大化窗口后宽度更宽但右侧元素也随之增多，同样会溢出。
        DropdownButton<String>(
          isDense: true,
          underline: const SizedBox.shrink(),
          value: exists ? current : null,
          hint: ConstrainedBox(
            // 三行布局下行1 独占 360px：320 放得下
            // 「英雄崎凛 (Lv60 · 海外加速区)」；还放不下就换两行，不省略
            constraints: const BoxConstraints(maxWidth: 320),
            child: Text(
              current.isEmpty ? I18n.resource_role_unknown.tr : _shortLabel,
              maxLines: 2,
              overflow: TextOverflow.fade,
              softWrap: true,
            ),
          ),
          items: roles
              .map((r) => DropdownMenuItem(
                    value: r.roleKey,
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 320),
                      child: Text(r.displayLabel,
                          maxLines: 2, overflow: TextOverflow.fade),
                    ),
                  ))
              .toList(),
          onChanged: (v) {
            if (v != null) controller.changeRole(v);
          },
        ),
        // 多角色时给个数量提示，提醒用户数据是分开的
        if (roles.length > 1)
          Padding(
            padding: const EdgeInsets.only(left: 4),
            child: Text(
              '(${roles.length})',
              style: theme.textTheme.labelSmall
                  ?.copyWith(color: theme.disabledColor),
            ),
          ),
      ]),
    );
  }

  /// 收起状态的角色显示名（hint 用）：名字 + 等级，不含服务器与完整 role_key
  String get _shortLabel {
    final matched = controller.roles
        .where((r) => r.roleKey == controller.selectedRoleKey.value)
        .toList();
    if (matched.isEmpty) return controller.selectedRoleKey.value;
    return matched.first.shortLabel;
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.controller});

  final ResourceController controller;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.inbox_outlined,
              size: 48, color: Theme.of(context).disabledColor),
          const SizedBox(height: 12),
          Text(I18n.resource_empty.tr,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyMedium),
          const SizedBox(height: 16),
          FilledButton.icon(
            onPressed:
                controller.collecting.value ? null : controller.collectNow,
            icon: const Icon(Icons.download_rounded),
            label: Text(I18n.resource_collect_now.tr),
          ),
        ],
      ),
    );
  }
}

/// 资源卡片网格：只渲染有图标的项。
///
/// 排序：**置顶项永远最前**，其余按 |delta| 降序（变动大的优先）。
class _Grid extends StatelessWidget {
  const _Grid({required this.controller});

  final ResourceController controller;

  @override
  Widget build(BuildContext context) {
    final items = controller.visibleItems;
    return LayoutBuilder(builder: (context, constraints) {
      // 自适应列数：面板里可能只有 380px，卡片最小 230px 才能放下名称+数量+增量
      final columns = (constraints.maxWidth / 230).floor().clamp(1, 6);
      return GridView.builder(
        padding: const EdgeInsets.all(10),
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: columns,
          // 加了图钉按钮，卡片略高一点避免挤压
          mainAxisExtent: 74,
          crossAxisSpacing: 8,
          mainAxisSpacing: 8,
        ),
        itemCount: items.length,
        itemBuilder: (context, index) {
          final item = items[index];
          return ResourceCard(
            item: item,
            pinned: controller.isPinned(item),
            onTogglePin: () => controller.togglePin(item),
            onTap: () => controller.showHistory(item),
          );
        },
      );
    });
  }
}
