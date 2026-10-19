// 置顶排序逻辑测试：置顶项必须永远排在最前。
import 'package:flutter_test/flutter_test.dart';

/// 复刻 ResourceController 的排序逻辑（不依赖 UI/GetX）
class Item {
  Item(this.icon, this.name, this.delta);
  final String icon;
  final String name;
  final int? delta;
}

List<Item> sortItems(List<Item> items, Set<String> pinned) {
  final result = items.toList()
    ..sort((a, b) {
      final pa = pinned.contains(a.icon);
      final pb = pinned.contains(b.icon);
      if (pa != pb) return pa ? -1 : 1;

      final da = (a.delta ?? 0).abs();
      final db = (b.delta ?? 0).abs();
      if (da != db) return db.compareTo(da);
      return a.name.compareTo(b.name);
    });
  return result;
}

void main() {
  test('置顶项排在第一位（即使 delta 为 0）', () {
    final items = [
      Item('icon_jinbi', '金币', 50000),
      Item('icon_gouyu', '勾玉', 0), // delta=0，不置顶时排最后
      Item('icon_tili', '体力', 100),
    ];
    final sorted = sortItems(items, {'icon_gouyu'});
    expect(sorted.first.name, '勾玉');
  });

  test('多个置顶项之间仍按 |delta| 排序', () {
    final items = [
      Item('a', 'A', 10),
      Item('b', 'B', 100),
      Item('c', 'C', 50),
    ];
    final sorted = sortItems(items, {'a', 'b'});
    expect(sorted[0].name, 'B'); // delta 100 最大
    expect(sorted[1].name, 'A'); // delta 10
    expect(sorted[2].name, 'C'); // 未置顶
  });

  test('未置顶时按 |delta| 降序（原行为不变）', () {
    final items = [
      Item('a', 'A', 10),
      Item('b', 'B', 100),
      Item('c', 'C', 50),
    ];
    final sorted = sortItems(items, {});
    expect(sorted.map((e) => e.name).toList(), ['B', 'C', 'A']);
  });

  test('负 delta 按绝对值参与排序', () {
    final items = [
      Item('a', 'A', 10),
      Item('b', 'B', -80),
    ];
    final sorted = sortItems(items, {});
    expect(sorted.first.name, 'B'); // |-80| > |10|
  });

  test('取消置顶后回到正常排序', () {
    final items = [
      Item('icon_gouyu', '勾玉', 0),
      Item('icon_jinbi', '金币', 50000),
    ];
    final pinnedFirst = sortItems(items, {'icon_gouyu'});
    expect(pinnedFirst.first.name, '勾玉');

    final unpinned = sortItems(items, {});
    expect(unpinned.first.name, '金币'); // 恢复 delta 排序
  });
}
