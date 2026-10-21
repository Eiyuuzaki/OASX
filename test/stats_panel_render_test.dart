// 统计面板渲染测试：用伪造的控制器数据直接渲染面板。
// 若布局抛异常或渲染出空白，测试失败并打印原因。
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:get/get.dart';
import 'package:oasx/config/theme.dart';
import 'package:oasx/modules/home/controllers/dashboard_controller.dart';
import 'package:oasx/modules/home/controllers/statistics_controller.dart';
import 'package:oasx/modules/home/models/script_statistics_models.dart';
import 'package:oasx/modules/home/widgets/statistics_panel.dart';
import 'package:oasx/modules/settings/controllers/settings_controller.dart';
import 'package:oasx/service/script_service.dart';
import 'package:oasx/service/websocket_service.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('统计面板在有数据时渲染出三个指标块', (tester) async {
    // --- 准备依赖链（dashboard -> ScriptService/SettingsController）---
    Get.put(SettingsController(), permanent: true);
    Get.lazyPut<WebSocketService>(() => WebSocketService(), fenix: true);
    Get.put(ScriptService(), permanent: true);
    Get.put(HomeDashboardController());
    final controller = Get.put(HomeStatisticsController());

    // 伪造日期列表与当日数据（模拟后端 200 响应后的状态）
    controller.availableDateKeys.assignAll(['2026-10-19']);
    controller.selectedDateKey.value = '2026-10-19';
    controller.datesLoading.value = false;
    controller.statisticsLoading.value = false;
    controller.connectionState.value = ScriptStatisticsConnectionState.idle;
    controller.lastErrorMessage.value = '';

    // 直接注入模型实例（绕过 ApiClient）
    // ignore: invalid_use_of_protected_member
    controller.statistics.value = ScriptStatisticsDay(
      scriptName: 'oas1',
      dateKey: '2026-10-19',
      totalRuntimeSeconds: 7185.4,
      totalTaskRunCount: 12,
      totalBattleCount: 34,
      tasks: {},
    );

    // 模拟 dashboard 上下文：让 isStatsVisibleInCurrentLayout 为 true 且脚本激活，
    // 否则 onInit 的 syncStatisticsBinding 会把上面的注入清空。
    Get.find<HomeDashboardController>().activeScriptName.value = 'oas1';
    Get.find<HomeDashboardController>().activeWorkbenchTab.value =
        HomeWorkbenchTab.stats;

    await tester.pumpWidget(
      GetMaterialApp(
        theme: lightTheme,
        home: const Scaffold(
          body: SizedBox(
            width: 500,
            height: 800,
            child: ScriptStatisticsPanel(),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    // --- 调试：面板当前渲染了什么 ---
    debugPrint(
      'DEBUG statistics=${controller.statistics.value} '
      'dates=${controller.availableDateKeys} '
      'selected=${controller.selectedDateKey.value} '
      'loading=${controller.statisticsLoading.value} '
      'err=${controller.lastErrorMessage.value}',
    );
    final allTexts = find.byType(Text);
    for (var i = 0; i < allTexts.evaluate().length; i++) {
      final t = tester.widget<Text>(allTexts.at(i));
      debugPrint('DEBUG text[$i] = "${t.data}"');
    }

    // --- 断言：三个指标块都渲染出了文本 ---
    final durationText = find.textContaining('1小时');
    final runCountText = find.text('12');
    final battleCountText = find.text('34');
    expect(durationText, findsWidgets, reason: '应显示总时长');
    expect(runCountText, findsWidgets, reason: '应显示运行次数 12');
    expect(battleCountText, findsWidgets, reason: '应显示战斗次数 34');

    // --- 无溢出即通过；有异常会直接在 pump 阶段抛出 ---
  });
}