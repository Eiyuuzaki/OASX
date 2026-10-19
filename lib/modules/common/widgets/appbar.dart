import 'package:flutter/material.dart';
import 'package:styled_widget/styled_widget.dart';

import 'package:oasx/modules/common/widgets/frosted_bars.dart';
import 'package:oasx/modules/common/widgets/title.dart';
import 'package:oasx/modules/common/widgets/windows_caption_bar.dart';
import 'package:oasx/utils/platform_utils.dart';

PreferredSizeWidget buildPlatformAppBar(
  BuildContext context, {
  bool isCollapsed = false,
  VoidCallback? onMenuPressed,
  String? routePath,
  List<Widget> trailingActions = const [],
}) {
  final platform = PlatformUtils.platfrom();
  return switch (platform) {
    PlatformType.windows => _windowAppbar(
      context,
      onMenuPressed: isCollapsed ? onMenuPressed : null,
      routePath: routePath,
      trailingActions: trailingActions,
    ),
    PlatformType.linux => _desktopAppbar(
      context,
      routePath: routePath,
      trailingActions: trailingActions,
    ),
    PlatformType.macOS => _desktopAppbar(
      context,
      routePath: routePath,
      trailingActions: trailingActions,
    ),
    PlatformType.android => _mobileTabletAppbar(context, routePath: routePath),
    PlatformType.iOS => _mobileTabletAppbar(context, routePath: routePath),
    PlatformType.web => _webAppbar(
      context,
      routePath: routePath,
      trailingActions: trailingActions,
    ),
    _ => _webAppbar(
      context,
      routePath: routePath,
      trailingActions: trailingActions,
    ),
  };
}

PreferredSizeWidget _windowAppbar(
  BuildContext context, {
  VoidCallback? onMenuPressed,
  String? routePath,
  List<Widget> trailingActions = const [],
}) {
  return WindowsCaptionBar(
    brightness: Theme.of(context).brightness,
    onMenuPressed: onMenuPressed,
    routePath: routePath,
    trailingActions: trailingActions,
  );
}

PreferredSizeWidget _desktopAppbar(
  BuildContext context, {
  String? routePath,
  List<Widget> trailingActions = const [],
}) {
  return AppBar(
    title: getTitle(context, routePath: routePath),
    automaticallyImplyLeading: _shouldAutoImplyLeading(),
    actions: trailingActions.isEmpty ? null : trailingActions,
  );
}

PreferredSizeWidget _webAppbar(
  BuildContext context, {
  String? routePath,
  List<Widget> trailingActions = const [],
}) {
  return PreferredSize(
    preferredSize: const Size.fromHeight(90),
    child: Row(
      children: [
        Expanded(
          child: getTitle(
            context,
            routePath: routePath,
          ).padding(left: 16, top: 10, bottom: 10),
        ),
        ...trailingActions,
        if (trailingActions.isNotEmpty) const SizedBox(width: 8),
      ],
    ),
  );
}

PreferredSizeWidget _mobileTabletAppbar(
  BuildContext context, {
  String? routePath,
}) {
  // 移动端毛玻璃 AppBar：Scaffold 需配合 extendBodyBehindAppBar，
  // 内容从标题栏下滚过时透出模糊底色（与毛玻璃底栏成对）。
  return PreferredSize(
    preferredSize: const Size.fromHeight(kToolbarHeight),
    child: FrostedBars.appBar(
      context,
      child: AppBar(
        title: getTitle(context, routePath: routePath),
        automaticallyImplyLeading: _shouldAutoImplyLeading(),
        backgroundColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
      ),
    ),
  );
}

bool _shouldAutoImplyLeading() => true;
