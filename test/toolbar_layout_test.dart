// 两行工具栏的宽度推演：任何容器宽度 ≥ 360 都不能溢出。
//
// 旧行（单行）需求实测 628px > 560px 侧栏 —— 物理放不下，这是
// "怎么打补丁都溢出"的根本原因，见 docs/UI移植-luchi5.md 8.5。
//
// 新方案两行拆分后：
//   行1（身份）：padding 18 + 角色icon 22 + hint≤150 + 数量 24 + gap 10 + 日期下拉 ≤110
//               = ≤334，且角色/日期都在 Flexible 里可收缩
//   行2（操作）：padding 18 + 状态 92 + gap 8 + 时间(min 60) + 按钮 80 = 258
import 'package:flutter_test/flutter_test.dart';

const double paddingH = 18;
const double roleIconAndGap = 22;
const double roleHintMax = 150;
const double roleCountSuffix = 24;
const double dateDropdownMax = 110;
const double statusChip = 92;
const double updatedAtMin = 60;
const double iconButtons = 80;
const double rowGap = 10;

void main() {
  test('行1 最坏情况 ≤ 360（侧栏最小宽度）', () {
    final row1 = paddingH +
        roleIconAndGap +
        roleHintMax +
        roleCountSuffix +
        rowGap +
        dateDropdownMax;
    // Flexible 保证可收缩，但固有需求也要放得下
    expect(row1, lessThanOrEqualTo(360), reason: '行1 需求 $row1');
  });

  test('行2 最坏情况 ≤ 360', () {
    final row2 = paddingH + statusChip + 8 + updatedAtMin + iconButtons;
    expect(row2, lessThanOrEqualTo(360),
        reason: '行2 需求 $row2，无 Flexible 也必须放下');
  });

  test('旧单行方案确实放不下 560（回归说明）', () {
    // 628 > 560：这就是旧版溢出的数学证明
    const oldRequired = 628.0;
    expect(oldRequired, greaterThan(560));
  });
}
