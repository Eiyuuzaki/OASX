import 'package:flutter/material.dart';
import 'package:get/get.dart';

import 'package:oasx/modules/common/widgets/primary_navigation_shell.dart';
import 'package:oasx/modules/home/home_binding.dart';
import 'package:oasx/modules/home/index.dart';
import 'package:oasx/modules/home/models/home_workbench_layout.dart';
import 'package:oasx/modules/server/index.dart';
import 'package:oasx/modules/settings/index.dart';
import 'package:oasx/utils/platform_utils.dart';

class Routes {
  static const initial = '/home';

  static final routes = [
    GetPage(
      name: '/home',
      page: () => _buildPrimaryPage('/home'),
      binding: HomeBinding(),
    ),
    GetPage(
      name: '/settings',
      page: () => _buildPrimaryPage('/settings'),
      binding: HomeBinding(),
    ),
    GetPage(
      name: '/server',
      page: () => const ServerView(),
      binding: BindingsBuilder(() {
        Get.put<ServerController>(ServerController());
      }),
    ),
  ];

  /// 桌面布局下改用 shell（底栏导航）的宽度阈值。
  ///
  /// 必须和 `PrimaryNavigationShell._shouldShowRail` 的判据一致：
  /// 低于它时 rail 会换成 bottomNavigationBar。如果这里仍然直接返回
  /// `HomeView`，桌面分支会铺满整个窗口——侧栏被挤没、内容溢出，
  /// 而且因为没有 shell 所以连底栏都没有。
  static const double _shellBreakpoint = kHomeWorkbenchMinCollectionWidth +
      kHomeWorkbenchMinDetailsWidth +
      kHomeWorkbenchDividerWidth +
      kPrimaryNavigationRailWidth;

  static Widget _buildPrimaryPage(String routePath) {
    if (!PlatformUtils.usesDesktopLayout) {
      return PrimaryNavigationShell(initialRoutePath: routePath);
    }
    // 桌面平台也要看窗口宽度：拖窄了就和手机一样走 shell
    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth >= _shellBreakpoint) {
          return switch (routePath) {
            '/settings' => const SettingsView(),
            _ => const HomeView(),
          };
        }
        return PrimaryNavigationShell(initialRoutePath: routePath);
      },
    );
  }
}
