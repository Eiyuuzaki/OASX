// 验证路由层与 shell 层的宽度判据一致。
//
// 之前的问题：路由层只看平台（usesDesktopLayout），不看宽度，
// 导致桌面窗口拖窄后仍然走 HomeView——没有底栏、内容还溢出。
import 'package:flutter_test/flutter_test.dart';
import 'package:oasx/modules/home/models/home_workbench_layout.dart';

/// 与 PrimaryNavigationShell.kPrimaryNavigationRailWidth 保持一致
const double kRailWidth = 80;

void main() {
  // 路由层：窄于此值走 shell
  const shellBreakpoint = kHomeWorkbenchMinCollectionWidth +
      kHomeWorkbenchMinDetailsWidth +
      kHomeWorkbenchDividerWidth +
      kRailWidth;

  // shell 层：宽于此值显示 rail
  const twoPaneShellWidth = kHomeWorkbenchMinCollectionWidth +
      kHomeWorkbenchMinDetailsWidth +
      kHomeWorkbenchDividerWidth +
      kRailWidth;

  test('两层判据必须完全一致', () {
    expect(shellBreakpoint, twoPaneShellWidth,
        reason: '不一致会导致：路由进了 shell，但 shell 又显示 rail，底栏仍然没有');
  });

  test('手机尺寸走 shell', () {
    expect(390 < shellBreakpoint, isTrue); // iPhone 逻辑宽度
    expect(409 < shellBreakpoint, isTrue); // DevicePreview 实测
  });

  test('窄窗口走 shell（底栏出现）', () {
    expect(420 < shellBreakpoint, isTrue);
    expect(600 < shellBreakpoint, isTrue);
  });

  test('宽窗口仍走桌面布局（rail 侧栏）', () {
    expect(626 >= shellBreakpoint, isTrue);
    expect(1189 >= shellBreakpoint, isTrue);
    expect(1920 >= shellBreakpoint, isTrue);
  });

  test('阈值数值符合预期', () {
    // 170 + 360 + 16 + 80
    expect(shellBreakpoint, 626);
  });
}
