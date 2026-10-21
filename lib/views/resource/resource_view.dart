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
        final scheme = Theme.of(context).colorScheme;
        // 不套页面级背景色：本页嵌在侧栏 Card 里，Card 已有自己的底，
        // 再叠 surfaceContainerLowest（浅色主题下≈纯白）会与周围割裂。
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Toolbar(controller: c),
            // 分隔：非贯穿式浅分割线（indent 16px + outlineVariant 低对比）
            Divider(
              height: 1,
              thickness: 1,
              indent: 16,
              endIndent: 16,
              color: scheme.outlineVariant.withValues(alpha: 0.6),
            ),
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

/// 采集状态：小圆点 + 文本，颜色即状态（运行=绿，未采集=灰）。
class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.controller});

  final ResourceController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final running = controller.collectorRunning.value;
    return Tooltip(
      message: running
          ? I18n.resource_collector_on.tr
          : I18n.resource_collector_off.tr,
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        // 状态圆点：未采集时用低饱和的 onSurfaceVariant 表「非活动」，
        // 运行时用 primary 表「活动」（比纯绿更贴 M3 色板）
        Icon(Icons.circle,
            size: 8,
            color: running ? scheme.primary : scheme.onSurfaceVariant),
        const SizedBox(width: 6),
        Text(
          running
              ? I18n.resource_collector_on.tr
              : I18n.resource_collector_off.tr,
          style: theme.textTheme.labelMedium?.copyWith(
            color: scheme.onSurfaceVariant,
          ),
        ),
      ]),
    );
  }
}

/// 角色下拉：多账号时用来切换查看对象。
///
/// M3：胶囊形下拉按钮（MenuAnchor），带下拉箭头、悬停 tonal 底、
/// 点开为标准菜单；替代旧的无边框 DropdownButton。
class _RoleSelector extends StatelessWidget {
  const _RoleSelector({required this.controller});

  final ResourceController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final roles = controller.roles;
    final current = controller.selectedRoleKey.value;
    final exists = roles.any((r) => r.roleKey == current);
    // 单角色且无服务器名可省略时也保留按钮（切换入口不能消失），
    // 但禁用箭头提示单选项
    final single = roles.length <= 1 && exists;

    return Align(
      alignment: Alignment.centerLeft,
      child: MenuAnchor(
        alignmentOffset: const Offset(0, 4),
        menuChildren: roles.isEmpty
            ? [
                MenuItemButton(
                  onPressed: null,
                  child: Text(I18n.resource_role_unknown.tr),
                ),
              ]
            : [
                for (final r in roles)
                  MenuItemButton(
                    leadingIcon: r.roleKey == current
                        ? Icon(Icons.check_rounded,
                            size: 18, color: scheme.primary)
                        : null,
                    onPressed: () => controller.changeRole(r.roleKey),
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 320),
                      child: Text(
                        r.displayLabel,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ),
              ],
        builder: (context, menu, _) {
          return TextButton.icon(
            style: TextButton.styleFrom(
              visualDensity: VisualDensity.compact,
              shape: const StadiumBorder(),
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            ),
            onPressed:
                single ? null : () => menu.isOpen ? menu.close() : menu.open(),
            icon: Icon(Icons.person_outline,
                size: 18, color: scheme.primary),
            label: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                ConstrainedBox(
                  // displayLabel 可能很长（名字+等级+服务器），限宽省略
                  constraints: const BoxConstraints(maxWidth: 320),
                  child: Text(
                    current.isEmpty || !exists
                        ? I18n.resource_role_unknown.tr
                        : _shortLabel,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.labelLarge?.copyWith(
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
                const SizedBox(width: 2),
                Icon(Icons.arrow_drop_down_rounded,
                    size: 18, color: scheme.onSurfaceVariant),
              ],
            ),
          );
        },
      ),
    );
  }

  /// 收起状态的角色显示名：名字 + 等级，不含服务器与完整 role_key
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
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            // M3：tonal 容器托底，让图标有分量（浅紫圆形 + primary 图标）
            Container(
              width: 72,
              height: 72,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: scheme.primaryContainer.withValues(alpha: 0.5),
                shape: BoxShape.circle,
              ),
              child: Icon(
                Icons.inbox_outlined,
                size: 36,
                color: scheme.primary,
              ),
            ),
            const SizedBox(height: 16),
            Text(
              I18n.resource_empty.tr,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: scheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: 20),
            // Filled Button：胶囊、primary 底、自带 ripple 与按压/禁用态
            FilledButton.icon(
              style: FilledButton.styleFrom(
                shape: const StadiumBorder(),
                padding: const EdgeInsets.symmetric(
                  horizontal: 24,
                  vertical: 12,
                ),
              ),
              onPressed:
                  controller.collecting.value ? null : controller.collectNow,
              icon: const Icon(Icons.download_rounded, size: 18),
              label: Text(I18n.resource_collect_now.tr),
            ),
          ],
        ),
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
