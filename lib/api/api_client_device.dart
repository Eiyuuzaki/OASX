part of 'api_client.dart';

/// 安卓实例（模拟器多开）信息。
///
/// 对应后端 `module/server/device_router.py`：
///   GET /device/instances         枚举本机可发现的实例
///   GET /device/current?config_name=  查询某配置绑定的设备
///   PUT /device/current           改写某配置的 device.serial（不重启脚本）
class DeviceInstanceModel {
  const DeviceInstanceModel({
    required this.instanceName,
    required this.displayName,
    required this.serial,
    required this.adbPort,
    required this.online,
    required this.model,
    required this.brand,
    required this.root,
    required this.emulator,
  });

  final String instanceName;
  final String displayName;
  final String? serial;
  final int? adbPort;
  final bool online;
  final String? model;
  final String? brand;
  final bool? root;
  final String emulator;

  factory DeviceInstanceModel.fromJson(Map<String, dynamic> json) {
    return DeviceInstanceModel(
      instanceName: (json['instance_name'] ?? '').toString(),
      displayName: (json['display_name'] ?? '').toString(),
      serial: json['serial']?.toString(),
      adbPort: json['adb_port'] is num ? (json['adb_port'] as num).toInt() : null,
      online: json['online'] == true,
      model: json['model']?.toString(),
      brand: json['brand']?.toString(),
      root: json['root'] is bool ? json['root'] as bool : null,
      emulator: (json['emulator'] ?? 'unknown').toString(),
    );
  }

  /// 副标题：机型 + 端口（缺的字段自动省略）。
  String get subtitle {
    final parts = <String>[];
    final device = [brand, model].where((e) => e != null && e.isNotEmpty).join(' ');
    if (device.isNotEmpty) parts.add(device);
    if (adbPort != null) parts.add(':$adbPort');
    return parts.join(' · ');
  }
}

/// 某配置当前绑定的设备。
class CurrentDeviceModel {
  const CurrentDeviceModel({
    required this.configName,
    required this.serial,
    required this.emulatorName,
    required this.emulatorType,
    required this.online,
  });

  final String configName;
  final String serial;
  final String? emulatorName;
  final String? emulatorType;
  final bool online;

  factory CurrentDeviceModel.fromJson(Map<String, dynamic> json) {
    return CurrentDeviceModel(
      configName: (json['config_name'] ?? '').toString(),
      serial: (json['serial'] ?? '').toString(),
      emulatorName: json['emulator_name']?.toString(),
      emulatorType: json['emulator_type']?.toString(),
      online: json['online'] == true,
    );
  }

  /// 展示名：优先实例目录名，回退 serial。
  String get displayName {
    final name = emulatorName?.trim() ?? '';
    return name.isNotEmpty ? name : serial;
  }
}

extension ApiClientDeviceX on ApiClient {
  /// 枚举本机可发现的安卓实例。
  Future<List<DeviceInstanceModel>> getDeviceInstances() async {
    final res = await request(() => get('/device/instances'));
    if (!res.isSuccess) {
      return const [];
    }
    final data = res.data;
    if (data is! Map) return const [];
    final list = data['instances'];
    if (list is! List) return const [];
    return list
        .whereType<Map>()
        .map((e) => DeviceInstanceModel.fromJson(Map<String, dynamic>.from(e)))
        .toList(growable: false);
  }

  /// 查询某配置当前绑定的设备。
  Future<CurrentDeviceModel?> getCurrentDevice(String configName) async {
    final res = await request(
      () => get('/device/current', queryParameters: {'config_name': configName}),
    );
    if (!res.isSuccess || res.data is! Map) {
      return null;
    }
    return CurrentDeviceModel.fromJson(
      Map<String, dynamic>.from(res.data as Map),
    );
  }

  /// 切换某配置绑定的设备（只改配置，不重启脚本）。
  Future<CurrentDeviceModel?> switchDevice({
    required String configName,
    required String serial,
  }) async {
    final res = await request(
      () => put(
        '/device/current',
        data: {'config_name': configName, 'serial': serial},
      ),
    );
    if (!res.isSuccess || res.data is! Map) {
      return null;
    }
    return CurrentDeviceModel.fromJson(
      Map<String, dynamic>.from(res.data as Map),
    );
  }
}
