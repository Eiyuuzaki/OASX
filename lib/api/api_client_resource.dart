part of 'api_client.dart';

/// 资源（货币/道具）快照与历史。
///
/// 对应后端 `module/server/resource_router.py`：
///   GET /resource/{config}/roles
///   GET /resource/{config}/dates?role_key=
///   GET /resource/{config}?date=&role_key=
///   GET /resource/{config}/history?item_id=&role_key=&limit=
///   GET /resource/{config}/status
///   POST /resource/{config}/start|stop|collect
///
/// 数据按**角色**隔离：同一台设备切号后，不同角色各自的记录互不影响。
class ResourceItemModel {
  final String id;
  final String name;
  final String icon;
  final int count;
  final int? delta;
  final String deltaFrom;

  ResourceItemModel({
    required this.id,
    required this.name,
    required this.icon,
    required this.count,
    required this.delta,
    required this.deltaFrom,
  });

  factory ResourceItemModel.fromJson(Map<String, dynamic> json) {
    return ResourceItemModel(
      id: (json['id'] ?? '').toString(),
      name: (json['name'] ?? '').toString(),
      icon: (json['icon'] ?? '').toString(),
      count: _asInt(json['count']),
      delta: json['delta'] == null ? null : _asInt(json['delta']),
      deltaFrom: (json['delta_from'] ?? '').toString(),
    );
  }

  static int _asInt(dynamic v) {
    if (v is int) return v;
    if (v is num) return v.toInt();
    return int.tryParse(v?.toString() ?? '') ?? 0;
  }

  /// 是否应当高亮（有增量）
  bool get hasDelta => delta != null && delta != 0;

  /// 千分位 + 万/亿 缩写，与游戏内显示习惯一致
  String get displayCount => formatQuantity(count);

  /// 带符号的增量文本，如 `+1,234`
  String get displayDelta {
    if (delta == null) return '';
    final sign = delta! > 0 ? '+' : '';
    return '$sign${_thousands(delta!)}';
  }

  static String formatQuantity(int value) {
    final abs = value.abs();
    if (abs >= 100000000) {
      return '${(value / 100000000).toStringAsFixed(2)}亿';
    }
    if (abs >= 10000) {
      return '${(value / 10000).toStringAsFixed(2)}万';
    }
    return _thousands(value);
  }

  static String _thousands(int value) {
    final s = value.abs().toString();
    final buf = StringBuffer();
    for (int i = 0; i < s.length; i++) {
      if (i > 0 && (s.length - i) % 3 == 0) buf.write(',');
      buf.write(s[i]);
    }
    return value < 0 ? '-$buf' : buf.toString();
  }
}

/// 一个**角色**（账号下的单个服务器角色）。
class ResourceRoleModel {
  final String roleKey;
  final String name;
  final int level;
  final String serverId;
  final String serverName;
  final String shortId;
  final String avatarId;
  final String lastUpdated;
  final List<String> dates;

  ResourceRoleModel({
    required this.roleKey,
    required this.name,
    required this.level,
    required this.serverId,
    required this.serverName,
    required this.shortId,
    required this.avatarId,
    required this.lastUpdated,
    required this.dates,
  });

  factory ResourceRoleModel.fromJson(Map<String, dynamic> json) {
    final rawDates = json['dates'];
    return ResourceRoleModel(
      roleKey: (json['role_key'] ?? '').toString(),
      name: (json['name'] ?? '').toString(),
      level: ResourceItemModel._asInt(json['level']),
      serverId: (json['server_id'] ?? '').toString(),
      serverName: (json['server_name'] ?? '').toString(),
      shortId: (json['short_id'] ?? '').toString(),
      avatarId: (json['avatar_id'] ?? '').toString(),
      lastUpdated: (json['last_updated'] ?? '').toString(),
      dates: rawDates is List
          ? rawDates.map((e) => e.toString()).toList()
          : const [],
    );
  }

  /// 下拉里显示的名字：`英雄崎凛 (Lv60 · 春之樱)`。
  ///
  /// 服务器**名称**比编号好认——多个同名同等级账号时只看编号很难区分。
  /// 后端还没拿到映射时 server_name 与 server_id 相同（显示编号兜底）。
  String get displayLabel {
    final lv = level > 0 ? 'Lv$level' : '';
    final server = serverName.isNotEmpty ? serverName : serverId;
    final parts = [if (lv.isNotEmpty) lv, if (server.isNotEmpty) server];
    final suffix = parts.isEmpty ? '' : ' (${parts.join(' · ')})';
    return '${name.isEmpty ? roleKey : name}$suffix';
  }

  /// 更短的标签（工具栏收起态用）：名字 + 等级 + 服务器
  String get shortLabel {
    final lv = level > 0 ? 'Lv$level' : '';
    final server = serverName.isNotEmpty ? serverName : serverId;
    final parts = [if (lv.isNotEmpty) lv, if (server.isNotEmpty) server];
    final suffix = parts.isEmpty ? '' : ' (${parts.join(' · ')})';
    return '${name.isEmpty ? roleKey : name}$suffix';
  }
}

class ResourceDayModel {
  final String configName;
  final String roleKey;
  final Map<String, dynamic> role;
  final String date;
  final String updatedAt;
  final List<ResourceItemModel> items;
  final int sampleCount;

  ResourceDayModel({
    required this.configName,
    required this.roleKey,
    required this.role,
    required this.date,
    required this.updatedAt,
    required this.items,
    required this.sampleCount,
  });

  factory ResourceDayModel.empty(String config) => ResourceDayModel(
        configName: config,
        roleKey: '',
        role: const {},
        date: '',
        updatedAt: '',
        items: const [],
        sampleCount: 0,
      );

  factory ResourceDayModel.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];
    return ResourceDayModel(
      configName: (json['config_name'] ?? '').toString(),
      roleKey: (json['role_key'] ?? '').toString(),
      role: (json['role'] as Map?)?.cast<String, dynamic>() ??
          (json['player'] as Map?)?.cast<String, dynamic>() ??
          const {},
      date: (json['date'] ?? '').toString(),
      updatedAt: (json['updated_at'] ?? '').toString(),
      items: rawItems is List
          ? rawItems
              .whereType<Map>()
              .map((e) => ResourceItemModel.fromJson(e.cast<String, dynamic>()))
              .toList()
          : const [],
      sampleCount: ResourceItemModel._asInt(json['sample_count']),
    );
  }

  bool get isEmpty => items.isEmpty;

  String get roleName => (role['name'] ?? '').toString();
  int get roleLevel => ResourceItemModel._asInt(role['level']);
}

class ResourceHistoryPoint {
  final String date;
  final int count;

  ResourceHistoryPoint({required this.date, required this.count});

  factory ResourceHistoryPoint.fromJson(Map<String, dynamic> json) =>
      ResourceHistoryPoint(
        date: (json['date'] ?? '').toString(),
        count: ResourceItemModel._asInt(json['count']),
      );
}

/// 历史数据的日期跨度。
class ResourceSpanModel {
  final int totalDays;
  final String first;
  final String last;

  /// 超过 7 天才允许自选范围
  final bool canPickRange;

  ResourceSpanModel({
    required this.totalDays,
    required this.first,
    required this.last,
    required this.canPickRange,
  });

  factory ResourceSpanModel.fromJson(Map<String, dynamic> json) =>
      ResourceSpanModel(
        totalDays: ResourceItemModel._asInt(json['total_days']),
        first: (json['first'] ?? '').toString(),
        last: (json['last'] ?? '').toString(),
        canPickRange: json['can_pick_range'] == true,
      );
}

/// 挂在 ApiClient 上的资源接口扩展。
extension ApiClientResourceX on ApiClient {
  /// 该配置下出现过的角色（按最后更新时间倒序）
  Future<List<ResourceRoleModel>> getResourceRoles(String config) async {
    final res = await request(() => get('/resource/$config/roles'));
    if (!res.isSuccess || res.data is! Map) return [];
    final roles = res.data['roles'];
    if (roles is! List) return [];
    return roles
        .whereType<Map>()
        .map((e) => ResourceRoleModel.fromJson(e.cast<String, dynamic>()))
        .toList();
  }

  /// 可用日期（倒序）；roleKey 为空时后端用最近更新的角色
  Future<List<String>> getResourceDates(String config, {String? roleKey}) async {
    final q = (roleKey == null || roleKey.isEmpty)
        ? ''
        : '?role_key=${Uri.encodeQueryComponent(roleKey)}';
    final res = await request(() => get('/resource/$config/dates$q'));
    if (!res.isSuccess) return [];
    final dates = res.data is Map ? res.data['dates'] : null;
    return dates is List ? dates.map((e) => e.toString()).toList() : [];
  }

  /// 某一天的资源视图；date 为空表示今天
  Future<ResourceDayModel?> getResourceDay(
    String config, {
    String? date,
    String? roleKey,
  }) async {
    final params = <String>[
      if (date != null && date.isNotEmpty) 'date=$date',
      if (roleKey != null && roleKey.isNotEmpty)
        'role_key=${Uri.encodeQueryComponent(roleKey)}',
    ];
    final query = params.isEmpty ? '' : '?${params.join('&')}';
    final res = await request(() => get('/resource/$config$query'));
    if (!res.isSuccess || res.data is! Map) return null;
    return ResourceDayModel.fromJson(
        (res.data as Map).cast<String, dynamic>());
  }

  /// 单项历史序列
  ///
  /// * 数据不足 7 天 → `days: 7`
  /// * 超过 7 天 → 传 `start`/`end` 由用户自选
  /// * 周期性资产（绘卷碎片）→ 传 `itemName`，按名称跨期聚合
  Future<List<ResourceHistoryPoint>> getResourceHistory(
    String config,
    String itemId, {
    String? roleKey,
    int limit = 30,
    int days = 0,
    String? start,
    String? end,
    String? itemName,
  }) async {
    // 传 itemName 时改为按名称跨期聚合：绘卷碎片每期换 ID，
    // 且老碎片在新活动前会被回收，按单个 ID 查会「换期就断」。
    final params = <String>[
      if (itemName != null && itemName.isNotEmpty)
        'item_name=${Uri.encodeQueryComponent(itemName)}'
      else
        'item_id=$itemId',
      'limit=$limit',
    ];
    if (roleKey != null && roleKey.isNotEmpty) {
      params.add('role_key=${Uri.encodeQueryComponent(roleKey)}');
    }
    if (days > 0) params.add('days=$days');
    if (start != null && start.isNotEmpty) params.add('start=$start');
    if (end != null && end.isNotEmpty) params.add('end=$end');
    final res = await request(
        () => get('/resource/$config/history?${params.join('&')}'));
    if (!res.isSuccess || res.data is! Map) return [];
    final pts = res.data['points'];
    if (pts is! List) return [];
    return pts
        .whereType<Map>()
        .map((e) => ResourceHistoryPoint.fromJson(e.cast<String, dynamic>()))
        .toList();
  }

  /// 数据日期跨度，决定是否允许自选范围
  Future<ResourceSpanModel?> getResourceHistorySpan(
    String config, {
    String? roleKey,
  }) async {
    final q = (roleKey == null || roleKey.isEmpty)
        ? ''
        : '?role_key=${Uri.encodeQueryComponent(roleKey)}';
    final res = await request(() => get('/resource/$config/history/span$q'));
    if (!res.isSuccess || res.data is! Map) return null;
    return ResourceSpanModel.fromJson(
        (res.data as Map).cast<String, dynamic>());
  }

  /// 采集器是否在跑
  Future<bool> getResourceStatus(String config) async {
    final res = await request(() => get('/resource/$config/status'));
    return res.isSuccess && res.data is Map && res.data['running'] == true;
  }

  /// 立即采集一次，返回采集到的角色键（失败时为空串）
  Future<String> collectResourceNow(String config) async {
    final res = await request(() => post('/resource/$config/collect'));
    if (!res.isSuccess || res.data is! Map) return '';
    if (res.data['success'] != true) return '';
    return (res.data['role_key'] ?? '').toString();
  }

  /// 启停采集器
  Future<bool> setResourceCollector(String config,
      {required bool start, int interval = 300}) async {
    final path = start
        ? '/resource/$config/start?interval=$interval'
        : '/resource/$config/stop';
    final res = await request(() => post(path));
    return res.isSuccess && res.data is Map && res.data['running'] == start;
  }
}

/// 任务名 → 中文显示名（来自后端 i18n）。
extension ApiClientTranslateX on ApiClient {
  Future<Map<String, String>> getTaskNameTranslate() async {
    final res = await request(() => get('/home/task_name_translate'));
    if (!res.isSuccess || res.data is! Map) return {};
    final out = <String, String>{};
    (res.data as Map).forEach((k, v) {
      if (v is String) out[k.toString()] = v;
    });
    return out;
  }
}
