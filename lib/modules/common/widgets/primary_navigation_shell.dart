import 'dart:async';

import 'package:flutter/material.dart';
import 'package:get/get.dart';
import 'package:oasx/modules/common/widgets/appbar.dart';
import 'package:oasx/modules/common/widgets/frosted_bars.dart';
import 'package:oasx/modules/home/index.dart';
import 'package:oasx/modules/home/models/home_workbench_layout.dart';
import 'package:oasx/modules/settings/index.dart';
import 'package:oasx/service/acrylic_service.dart';
import 'package:oasx/translation/i18n_content.dart';

const double kPrimaryNavigationRailWidth = 80;

class PrimaryNavigationShell extends StatefulWidget {
  const PrimaryNavigationShell({
    super.key,
    required this.initialRoutePath,
  });

  final String initialRoutePath;

  @override
  State<PrimaryNavigationShell> createState() => _PrimaryNavigationShellState();
}

class _PrimaryNavigationShellState extends State<PrimaryNavigationShell> {
  late String _routePath;
  late final Set<int> _builtIndexes;

  @override
  void initState() {
    super.initState();
    _routePath = _normalizeRoutePath(widget.initialRoutePath);
    _builtIndexes = <int>{_selectedIndexForRoute(_routePath)};
  }

  @override
  void didUpdateWidget(covariant PrimaryNavigationShell oldWidget) {
    super.didUpdateWidget(oldWidget);
    final nextRoutePath = _normalizeRoutePath(widget.initialRoutePath);
    if (nextRoutePath == _routePath) {
      return;
    }
    final previousRoutePath = _routePath;
    _routePath = nextRoutePath;
    _builtIndexes.add(_selectedIndexForRoute(nextRoutePath));
    _handleRouteExit(previousRoutePath, nextRoutePath);
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final selectedIndex = _selectedIndexForRoute(_routePath);
        final showRail = _shouldShowRail(constraints.maxWidth);
        final content = _PrimaryNavigationContent(
          selectedIndex: selectedIndex,
          builtIndexes: _builtIndexes,
        );
        // 移动端 scaffold 底设为透明（让 FrostedBars 能糊到东西），
        // 但透明需要有"可被模糊的内容"托底，否则会露出黑色。
        // 这里铺一层主题渐变色作为模糊源：
        //   Scaffold(透明) → 渐变背景 → 内容 → 毛玻璃 AppBar/底栏
        final frosted = !showRail && AcrylicService.mobileFrostedBackground;
        return Scaffold(
          appBar: buildPlatformAppBar(context, routePath: _routePath),
          resizeToAvoidBottomInset: false,
          // 毛玻璃底栏/AppBar：body 延伸到栏后，内容滚动时透出模糊底色
          extendBody: showRail ? false : true,
          extendBodyBehindAppBar: !showRail,
          body: frosted
              ? DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                      colors: [
                        Theme.of(context).colorScheme.surface,
                        Theme.of(context).colorScheme.surfaceContainerHighest,
                      ],
                    ),
                  ),
                  child: showRail
                      ? Row(
                          children: [
                            _PrimaryNavigationRail(
                              selectedIndex: selectedIndex,
                              onSelected: _handleDestinationSelected,
                            ),
                            const VerticalDivider(width: 1),
                            Expanded(child: content),
                          ],
                        )
                      : content,
                )
              : showRail
                  ? Row(
                      children: [
                        _PrimaryNavigationRail(
                          selectedIndex: selectedIndex,
                          onSelected: _handleDestinationSelected,
                        ),
                        const VerticalDivider(width: 1),
                        Expanded(child: content),
                      ],
                    )
                  : content,
          bottomNavigationBar: showRail
              ? null
              : FrostedBars.bottomBar(
                  context,
                  child: NavigationBar(
                    selectedIndex: selectedIndex,
                    onDestinationSelected: (index) {
                      Haptics.selection();
                      _handleDestinationSelected(index);
                    },
                    // 关键：NavigationBar 的 M3 默认背景是不透明的
                    // surfaceContainer，会完全盖住 FrostedBars 的模糊层，
                    // 看起来就像完全没有毛玻璃。必须显式透明。
                    backgroundColor: Colors.transparent,
                    elevation: 0,
                    surfaceTintColor: Colors.transparent,
                    shadowColor: Colors.transparent,
                    destinations: _destinations(),
                  ),
                ),
        );
      },
    );
  }

  bool _shouldShowRail(double maxWidth) {
    const twoPaneShellWidth = kHomeWorkbenchMinCollectionWidth +
        kHomeWorkbenchMinDetailsWidth +
        kHomeWorkbenchDividerWidth +
        kPrimaryNavigationRailWidth;
    return maxWidth >= twoPaneShellWidth;
  }

  String _normalizeRoutePath(String value) {
    return value == '/settings' ? '/settings' : '/home';
  }

  int _selectedIndexForRoute(String value) {
    return value == '/settings' ? 1 : 0;
  }

  String _routePathForIndex(int index) {
    return index == 1 ? '/settings' : '/home';
  }

  void _handleDestinationSelected(int index) {
    final nextRoutePath = _routePathForIndex(index);
    if (nextRoutePath == _routePath) {
      return;
    }
    final previousRoutePath = _routePath;
    setState(() {
      _routePath = nextRoutePath;
      _builtIndexes.add(index);
    });
    _handleRouteExit(previousRoutePath, nextRoutePath);
  }

  void _handleRouteExit(String previousRoutePath, String nextRoutePath) {
    if (previousRoutePath == '/settings' && nextRoutePath != '/settings') {
      unawaited(handleSettingsLeaveEffect());
    }
  }

  List<Widget> _destinations() {
    return [
      NavigationDestination(
        icon: const Icon(Icons.home_rounded),
        label: I18n.home.tr,
      ),
      NavigationDestination(
        icon: const Icon(Icons.settings_rounded),
        label: I18n.setting.tr,
      ),
    ];
  }
}

class _PrimaryNavigationContent extends StatelessWidget {
  const _PrimaryNavigationContent({
    required this.selectedIndex,
    required this.builtIndexes,
  });

  final int selectedIndex;
  final Set<int> builtIndexes;

  @override
  Widget build(BuildContext context) {
    return IndexedStack(
      index: selectedIndex,
      children: [
        builtIndexes.contains(0)
            ? const HomeView(standalone: false)
            : const SizedBox.shrink(),
        builtIndexes.contains(1)
            ? const SettingsView(standalone: false)
            : const SizedBox.shrink(),
      ],
    );
  }
}

class _PrimaryNavigationRail extends StatelessWidget {
  const _PrimaryNavigationRail({
    required this.selectedIndex,
    required this.onSelected,
  });

  final int selectedIndex;
  final ValueChanged<int> onSelected;

  @override
  Widget build(BuildContext context) {
    return NavigationRail(
      selectedIndex: selectedIndex,
      onDestinationSelected: onSelected,
      labelType: NavigationRailLabelType.all,
      destinations: [
        NavigationRailDestination(
          icon: const Icon(Icons.home_rounded),
          label: Text(I18n.home.tr),
        ),
        NavigationRailDestination(
          icon: const Icon(Icons.settings_rounded),
          label: Text(I18n.setting.tr),
        ),
      ],
    );
  }
}
