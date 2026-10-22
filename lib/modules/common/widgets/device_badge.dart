import 'dart:async';

import 'package:flutter/material.dart';
import 'package:get/get.dart';

import 'package:oasx/api/api_client.dart';
import 'package:oasx/modules/home/controllers/dashboard_controller.dart';

/// 标题栏里的设备徽标：显示当前连接的安卓实例，点击可切换。
///
/// 放在自绘标题栏（`WindowsCaptionBar`）里，因为那是唯一常驻可见、
/// 又不依赖系统标题栏的位置。无边框窗口下原生的系统标题栏不可见，
/// 所以设备信息必须由自绘区域承载。
///
/// 交互：
/// - 常态：`● MuMu安卓设备`（圆点颜色表示在线状态）
/// - 点击：弹出 `MenuAnchor` 菜单，列出后端枚举到的全部实例
/// - 选中：调 `PUT /device/current` 改写当前配置的 `device.serial`
///   （只改配置，不重启正在跑的脚本）
class DeviceBadge extends StatefulWidget {
  /// 创建设备徽标。
  const DeviceBadge({super.key, this.compact = false});

  /// 紧凑模式：只显示状态图标，不显示实例名（窄窗口用）。
  final bool compact;

  @override
  State<DeviceBadge> createState() => _DeviceBadgeState();
}

class _DeviceBadgeState extends State<DeviceBadge> {
  List<DeviceInstanceModel> _instances = const [];
  CurrentDeviceModel? _current;
  bool _loading = true;
  String _boundConfig = '';

  @override
  void initState() {
    super.initState();
    unawaited(_refresh());
  }

  /// 当前激活的配置名（统计/资产等页面都按它取数据）。
  String get _activeConfig {
    if (!Get.isRegistered<HomeDashboardController>()) {
      return '';
    }
    return Get.find<HomeDashboardController>()
        .activeScriptName
        .value
        .trim();
  }

  Future<void> _refresh() async {
    final config = _activeConfig;
    if (config.isEmpty) {
      // 还没选中脚本配置（例如移动端刚进首页）。
      // 不隐藏徽标——退化为「显示实例、但不可切换」，
      // 否则用户在移动端完全看不到设备信息。
      if (!mounted) return;
      final instances = await ApiClient().getDeviceInstances();
      if (!mounted) return;
      setState(() {
        _instances = instances;
        _current = null;
        _boundConfig = '';
        _loading = false;
      });
      return;
    }
    final api = ApiClient();
    final results = await Future.wait([
      api.getDeviceInstances(),
      api.getCurrentDevice(config),
    ]);
    if (!mounted) return;
    setState(() {
      _instances = results[0] as List<DeviceInstanceModel>;
      _current = results[1] as CurrentDeviceModel?;
      _boundConfig = config;
      _loading = false;
    });
  }

  Future<void> _switchTo(DeviceInstanceModel instance) async {
    final serial = instance.serial;
    if (serial == null || serial.isEmpty) return;
    final config = _boundConfig;
    if (config.isEmpty) return;
    final updated = await ApiClient().switchDevice(
      configName: config,
      serial: serial,
    );
    if (!mounted) return;
    if (updated != null) {
      setState(() => _current = updated);
    }
    // 切设备只改配置、不重启脚本。这里不主动刷新其它页面——
    // 脚本下次启动才生效，现在刷新反而会显示旧实例的数据。
    // 用户若要立即生效，去工作台重启脚本即可。
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    if (_loading) {
      return const SizedBox(
        width: 16,
        height: 16,
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: 8),
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
      );
    }

    // 没有绑定配置时（移动端刚进入首页），用第一个在线实例做展示，
    // 但禁用切换——避免在不知道目标配置的情况下误改文件。
    final fallback = _instances.where((e) => e.online).firstOrNull ??
        _instances.firstOrNull;
    final displayName = _current?.displayName ??
        fallback?.displayName ??
        '';
    if (displayName.isEmpty) {
      return const SizedBox.shrink();
    }
    final online = _current?.online ?? (fallback?.online ?? false);
    final serial = _current?.serial ?? fallback?.serial ?? '';
    final canSwitch = _boundConfig.isNotEmpty && _instances.isNotEmpty;
    final tone = online ? scheme.primary : scheme.onSurfaceVariant;

    return MenuAnchor(
      alignmentOffset: const Offset(0, 6),
      menuChildren: [
        if (_instances.isEmpty)
          const MenuItemButton(
            onPressed: null,
            child: Text('未发现可选实例'),
          )
        else
          for (final instance in _instances)
            MenuItemButton(
              leadingIcon: Icon(
                instance.online ? Icons.circle : Icons.circle_outlined,
                size: 10,
                color: instance.online
                    ? scheme.primary
                    : scheme.onSurfaceVariant,
              ),
              // 没有绑定配置时不能切换（不知道要改哪个配置文件）
              onPressed: (instance.serial == null || !canSwitch)
                  ? null
                  : () => _switchTo(instance),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Row(
                    children: [
                      Text(
                        instance.displayName,
                        style: theme.textTheme.bodyMedium?.copyWith(
                          fontWeight: instance.serial == serial
                              ? FontWeight.w700
                              : FontWeight.w400,
                        ),
                      ),
                      if (instance.serial == serial) ...[
                        const SizedBox(width: 6),
                        Icon(Icons.check_rounded,
                            size: 14, color: scheme.primary),
                      ],
                    ],
                  ),
                  if (instance.subtitle.isNotEmpty)
                    Text(
                      instance.subtitle,
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                ],
              ),
            ),
      ],
      builder: (context, menu, _) {
        return Tooltip(
          message: online
              ? '$displayName\n$serial · 已连接'
              : '$displayName\n$serial · 未连接',
          child: InkWell(
            borderRadius: BorderRadius.circular(999),
            onTap: () => menu.isOpen ? menu.close() : menu.open(),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    online ? Icons.smartphone_rounded : Icons.smartphone_outlined,
                    size: 14,
                    color: tone,
                  ),
                  if (!widget.compact) ...[
                    const SizedBox(width: 6),
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 180),
                      child: Text(
                        displayName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.labelMedium?.copyWith(
                          color: scheme.onSurfaceVariant,
                        ),
                      ),
                    ),
                  ],
                  Icon(Icons.arrow_drop_down_rounded,
                      size: 16, color: scheme.onSurfaceVariant),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}
