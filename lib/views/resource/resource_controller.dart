part of resource;

/// 「资产」页状态管理。
///
/// 数据按**角色**隔离：切换角色会重新拉取该角色自己的日期与快照，
/// 不同账号的记录不会互相串。
class ResourceController extends GetxController {
  ResourceController({required this.name});

  final String name;

  final loading = false.obs;
  final collecting = false.obs;
  final collectorRunning = false.obs;

  /// 该配置下出现过的所有角色
  final roles = <ResourceRoleModel>[].obs;

  /// 当前查看的角色键（空串 = 交给后端选最近更新的）
  final selectedRoleKey = ''.obs;

  final dates = <String>[].obs;
  final selectedDate = ''.obs;
  final day = ResourceDayModel.empty('').obs;

  /// 过滤后的资源（只保留本地有图标的项），供界面直接渲染
  final visibleItems = <ResourceItemModel>[].obs;

  /// 置顶的资源键（形如 `icon_gouyu`），置顶项永远排在网格最前。
  ///
  /// 用**图标名**作为键而不是 ID：资产本身由 (图标, 名称) 聚合得到，
  /// 图标名稳定（跨期不变），换号/换期都不会失效。
  final pinnedKeys = <String>{}.obs;

  Timer? _pollTimer;

  /// 置顶状态的持久化 key（按配置隔离，不同脚本各自记住）
  String get _pinStorageKey => 'resource.pinned.$name';

  @override
  void onInit() {
    super.onInit();
    _loadPinned();
    bootstrap();
    // 采集器在后台跑，前端每 30 秒静默刷新一次
    _pollTimer = Timer.periodic(const Duration(seconds: 30), (_) {
      if (!loading.value) _loadDay();
    });
  }

  // ------------------------------------------------------------------ 置顶
  void _loadPinned() {
    try {
      final raw = GetStorage().read(_pinStorageKey);
      if (raw is List) {
        pinnedKeys.assignAll(raw.map((e) => e.toString()).toSet());
      }
    } catch (_) {
      // 读取失败不影响主流程
    }
  }

  void _savePinned() {
    try {
      GetStorage().write(_pinStorageKey, pinnedKeys.toList());
    } catch (_) {}
  }

  /// 切换某项的置顶状态
  void togglePin(ResourceItemModel item) {
    final key = stripImageExt(item.icon);
    if (pinnedKeys.contains(key)) {
      pinnedKeys.remove(key);
    } else {
      pinnedKeys.add(key);
    }
    _savePinned();
    _applyIconFilter(); // 重新排序
  }

  /// 某项是否置顶
  bool isPinned(ResourceItemModel item) =>
      pinnedKeys.contains(stripImageExt(item.icon));

  @override
  void onClose() {
    _pollTimer?.cancel();
    _pollTimer = null;
    super.onClose();
  }

  Future<void> bootstrap() async {
    loading.value = true;
    try {
      // 先拿到本地可用图标清单，后续过滤要用
      await IconRegistry.instance.ensureLoaded();

      await _loadRoles();

      // 首次进入：选最近更新的角色
      if (selectedRoleKey.value.isEmpty && roles.isNotEmpty) {
        selectedRoleKey.value = roles.first.roleKey;
      }

      await _loadDates();
      await _loadDay();
      collectorRunning.value = await ApiClient().getResourceStatus(name);
    } catch (e) {
      printError(info: 'ResourceController.bootstrap: $e');
    } finally {
      loading.value = false;
    }
  }

  Future<void> reload() async {
    await bootstrap();
  }

  /// 切换角色：日期与快照都要重拉，避免串数据
  Future<void> changeRole(String roleKey) async {
    if (roleKey == selectedRoleKey.value) return;
    selectedRoleKey.value = roleKey;
    loading.value = true;
    try {
      // 角色变了，日期列表也变了
      await _loadDates();
      await _loadDay();
    } finally {
      loading.value = false;
    }
  }

  Future<void> changeDate(String date) async {
    selectedDate.value = date;
    loading.value = true;
    try {
      await _loadDay();
    } finally {
      loading.value = false;
    }
  }

  Future<void> _loadRoles() async {
    final list = await ApiClient().getResourceRoles(name);
    roles.assignAll(list);
  }

  Future<void> _loadDates() async {
    final list = await ApiClient()
        .getResourceDates(name, roleKey: selectedRoleKey.value);
    dates.assignAll(list);
    // 当前选中日期不在新列表里（切角色），回落到最新一天
    if (!dates.contains(selectedDate.value)) {
      selectedDate.value = list.isEmpty ? _todayKey() : list.first;
    }
  }

  Future<void> _loadDay() async {
    final date = selectedDate.value;
    if (date.isEmpty) return;
    final result = await ApiClient().getResourceDay(
      name,
      date: date,
      roleKey: selectedRoleKey.value,
    );
    if (result != null) {
      day.value = result;
      // 后端可能把空 role_key 解析成具体角色，回填以保持下拉一致
      if (selectedRoleKey.value.isEmpty && result.roleKey.isNotEmpty) {
        selectedRoleKey.value = result.roleKey;
      }
      _applyIconFilter();
    }
  }

  /// 决定显示哪些资产。
  ///
  /// 规则（按「一个图标 = 一个资产」）：
  /// 1. **只保留本地有图标的** —— 图标即"用户关心哪些资产"的白名单；
  /// 2. **剔除数量为 0 的** —— 过期道具/往期活动遗留会一直躺在 `currency` 里，
  ///    例如绘卷碎片有 188 个历史活动 ID；
  /// 3. **同一图标只留一个** —— 游戏里大量道具共用图标
  ///    （`icon_gouyu` 被 32 个 ID 共用、`icon_tili` 被 4 个共用）。
  ///
  /// 第 3 步的选择策略见 [_pickForIcon]。
  ///
  /// 效果：新号 Lv60 从 5708 项 → 14 项（正好对应 14 个图标）。
  void _applyIconFilter() {
    final all = day.value.items;

    // 1 + 2：有图标 且 数量 > 0
    final candidates = all.where((item) =>
        IconRegistry.instance.has(item.icon) && item.count > 0);

    // 3：按 (图标, 名称) 聚合 —— 同名视为同一种资产的多期版本，数量相加
    final groups = <String, List<ResourceItemModel>>{};
    for (final item in candidates) {
      final key = '${stripImageExt(item.icon)}\u0000${item.name}';
      groups.putIfAbsent(key, () => []).add(item);
    }
    final merged = <String, ResourceItemModel>{};   // icon -> 聚合后的代表
    groups.forEach((key, group) {
      final icon = key.split('\u0000').first;
      final agg = _aggregate(group);
      final prev = merged[icon];
      // 同一图标下多个名称分组（如金币 vs 吉运值）：优先常驻档，其次数量大
      if (prev == null || _preferGroup(agg, prev)) {
        merged[icon] = agg;
      }
    });

    // 排序规则：
    // 1. **置顶项永远最前**（用户主动钉住的，如勾玉）
    // 2. 其余按 |delta| 降序（变动大的优先）
    // 3. 最后按名称，保证顺序稳定
    final result = merged.values.toList()
      ..sort((a, b) {
        final pa = isPinned(a);
        final pb = isPinned(b);
        if (pa != pb) return pa ? -1 : 1;

        final da = (a.delta ?? 0).abs();
        final db = (b.delta ?? 0).abs();
        if (da != db) return db.compareTo(da);
        return a.name.compareTo(b.name);
      });
    visibleItems.assignAll(result);
  }

  /// 把同一 (图标, 名称) 下的多个 ID 合并成一张卡片。
  ///
  /// **这是「一劳永逸」的关键**：绘卷碎片每期活动换新 ID（实测 55~64 期），
  /// 名称却始终是「绘卷碎片·小/中/大」。合并后：
  /// - 不需要知道任何 ID，新活动开了自动纳入求和；
  /// - 不会因为「挑了旧的那期」而漏掉新碎片；
  /// - 用户看到的是「我还有 N 个碎片」这个真正关心的数字。
  ///
  /// 显示的 ID 取最新一期，用于点开看历史走势。
  static ResourceItemModel _aggregate(List<ResourceItemModel> group) {
    if (group.length == 1) return group.single;

    var total = 0;
    int? totalDelta;
    var hasDelta = false;
    var newest = group.first;

    for (final item in group) {
      total += item.count;
      if (item.delta != null) {
        hasDelta = true;
        totalDelta = (totalDelta ?? 0) + item.delta!;
      }
      if (_idOf(item) > _idOf(newest)) newest = item;
    }

    return ResourceItemModel(
      id: newest.id,
      name: newest.name,
      icon: newest.icon,
      count: total,
      delta: hasDelta ? totalDelta : null,
      deltaFrom: newest.deltaFrom,
    );
  }

  /// 同一图标下、名称不同的两个聚合项，选哪个显示。
  ///
  /// 优先常驻档（`ID < 901000`，如金币 900001 对应 icon_jinbi），
  /// 避免「吉运值」这类小众计数挤掉金币；
  /// 同档内取数量大的，平局取 ID 小的。
  static bool _preferGroup(ResourceItemModel a, ResourceItemModel b) {
    final aCore = _idOf(a) < _coreIdCutoff;
    final bCore = _idOf(b) < _coreIdCutoff;
    if (aCore != bCore) return aCore;
    if (a.count != b.count) return a.count > b.count;
    return _idOf(a) < _idOf(b);
  }

  /// 常驻资产与周期性资产的 ID 分界。
  ///
  /// 实测依据（Lv60 账号）：
  /// - 常驻：金币 900001、勾玉 900002、体力 900003、勋章 900016、皮肤券 900023…
  /// - 周期：绘卷碎片 901525~903551、活动币 902247~907010、赠品 1434107…
  static const int _coreIdCutoff = 901000;

  /// 资源 ID 通常是纯数字；非数字时退化为 0，保证比较不抛异常。
  static int _idOf(ResourceItemModel item) =>
      int.tryParse(item.id) ?? 0;

  /// 立即采集一次，然后刷新；若采集到的是新角色，自动切过去
  Future<void> collectNow() async {
    collecting.value = true;
    try {
      final collectedRole = await ApiClient().collectResourceNow(name);
      if (collectedRole.isEmpty) {
        Get.snackbar(
            I18n.resource_collect_now.tr, I18n.resource_collect_failed.tr,
            duration: const Duration(seconds: 3));
        return;
      }

      await _loadRoles();
      selectedRoleKey.value = collectedRole;
      await _loadDates();
      selectedDate.value = _todayKey();
      await _loadDay();
      collectorRunning.value = true;

      Haptics.medium(); // 移动端采集完成轻震
      Get.snackbar(I18n.resource_collect_now.tr, I18n.resource_collect_ok.tr,
          duration: const Duration(seconds: 2));
    } catch (e) {
      Get.snackbar(I18n.resource_collect_now.tr,
          I18n.resource_collect_failed.tr,
          duration: const Duration(seconds: 3));
    } finally {
      collecting.value = false;
    }
  }

  /// 点卡片看历史折线图。
  ///
  /// 时间范围规则：
  /// * 数据 ≤ 7 天 → 固定展示全部（其实就只有这几天）
  /// * 数据 > 7 天 → 默认最近 7 天，并提供范围选择
  Future<void> showHistory(ResourceItemModel item) async {
    final span = await ApiClient().getResourceHistorySpan(
      name,
      roleKey: selectedRoleKey.value,
    );

    if (!Get.isRegistered<ResourceHistoryController>(tag: 'history-$name')) {
      Get.put(ResourceHistoryController(name: name, item: item),
          tag: 'history-$name', permanent: false);
    }
    final controller = Get.find<ResourceHistoryController>(tag: 'history-$name');
    controller.span.value = span;
    controller.applyDefaultRange(span);

    // 数据太少时不必开弹窗，直接提示
    if (span != null && span.totalDays <= 1) {
      Get.snackbar(
        item.name,
        I18n.resource_no_history.tr,
        duration: const Duration(seconds: 2),
      );
      return;
    }

    final role = day.value.roleName;
    Get.dialog(
      ResourceHistoryDialog(
        controllerTag: 'history-$name',
        title: role.isEmpty ? item.name : '$role · ${item.name}',
      ),
      barrierDismissible: true,
    );
  }

  static String _todayKey() {
    final now = DateTime.now();
    final m = now.month.toString().padLeft(2, '0');
    final d = now.day.toString().padLeft(2, '0');
    return '${now.year}-$m-$d';
  }
}

/// 单项资源历史的弹窗控制器。
class ResourceHistoryController extends GetxController {
  ResourceHistoryController({required this.name, required this.item});

  final String name;
  final ResourceItemModel item;

  final loading = false.obs;
  final points = <ResourceHistoryPoint>[].obs;
  final span = Rxn<ResourceSpanModel>();

  /// 当前选择的天数（0 表示使用自定义区间）
  final selectedDays = 7.obs;

  /// 自定义区间
  final customStart = ''.obs;
  final customEnd = ''.obs;

  @override
  void onInit() {
    super.onInit();
    load();
  }

  @override
  void onClose() {
    // 弹窗关掉就销毁，避免占着旧数据
    Future.microtask(() {
      if (Get.isRegistered<ResourceHistoryController>(tag: 'history-$name')) {
        Get.delete<ResourceHistoryController>(tag: 'history-$name', force: true);
      }
    });
    super.onClose();
  }

  bool get canPickRange => span.value?.canPickRange ?? false;

  /// 数据不足 7 天时，展示全部；否则默认最近 7 天
  void applyDefaultRange(ResourceSpanModel? s) {
    if (s == null) {
      selectedDays.value = 7;
      return;
    }
    if (s.totalDays <= 7) {
      // 全部展示
      selectedDays.value = s.totalDays > 0 ? s.totalDays : 7;
    } else {
      selectedDays.value = 7;
    }
  }

  Future<void> load() async {
    loading.value = true;
    try {
      // 按名称查：绘卷碎片每期换 ID，老碎片在新活动前会被回收，
      // 按单个 ID 查会在换期时断掉，看不到跨期曲线。
      // 名称在同图标内唯一（实测 61 个名称、9 个重名组，每组仅对应一个图标），
      // 所以按名称聚合是安全的。
      List<ResourceHistoryPoint> result;
      if (customStart.value.isNotEmpty || customEnd.value.isNotEmpty) {
        result = await ApiClient().getResourceHistory(
          name,
          item.id,
          roleKey: _roleKey,
          itemName: item.name,
          start: customStart.value,
          end: customEnd.value,
        );
      } else {
        result = await ApiClient().getResourceHistory(
          name,
          item.id,
          roleKey: _roleKey,
          itemName: item.name,
          days: selectedDays.value,
        );
      }
      points.assignAll(result);
    } catch (e) {
      printError(info: 'ResourceHistoryController.load: $e');
    } finally {
      loading.value = false;
    }
  }

  /// 切换「最近 N 天」
  Future<void> selectDays(int days) async {
    selectedDays.value = days;
    customStart.value = '';
    customEnd.value = '';
    await load();
  }

  /// 应用自定义区间
  Future<void> applyRange(String start, String end) async {
    customStart.value = start;
    customEnd.value = end;
    await load();
  }

  String get _roleKey {
    // 从当前的 ResourceController 拿角色，避免历史数据跨角色
    final tag = name;
    if (Get.isRegistered<ResourceController>(tag: tag)) {
      return Get.find<ResourceController>(tag: tag).selectedRoleKey.value;
    }
    return '';
  }
}
