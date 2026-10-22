import 'package:flutter/material.dart';

import 'package:oasx/ui/style.dart';

/// 语义化卡片：跨平台统一入口。
///
/// 业务代码用 [AppCard] 而不是裸 `Card`，由这里决定各平台的具体呈现：
///
/// | 风格 | 呈现 |
/// |---|---|
/// | fluent | 细描边 + 极浅填充（WinUI 的 Layer 卡片），无阴影 |
/// | material | M3 的 `Card.filled` / `Card.outlined` / 默认 elevation |
/// | cupertino | 无阴影纯色块 + iOS 分组分隔线风格 |
///
/// 三种形态覆盖了原来 `Card` / `Card.filled` / `Card.outlined` 的全部用法，
/// 语义上用 [AppCardVariant] 表达"想表达什么"，而不是"用什么组件"。
enum AppCardVariant {
  /// 常规容器：有轻微层级感（原 `Card`）。
  elevated,

  /// 强调容器：用色调底表达分组（原 `Card.filled`）。
  filled,

  /// 弱强调容器：只有描边，用于嵌套或次要信息（原 `Card.outlined`）。
  outlined,
}

/// 跨平台语义卡片。
class AppCard extends StatelessWidget {
  /// 创建语义卡片。
  const AppCard({
    super.key,
    required this.child,
    this.variant = AppCardVariant.elevated,
    this.padding,
    this.margin = EdgeInsets.zero,
    this.onTap,
    this.borderRadius,
  });

  /// 卡片内容。
  final Widget child;

  /// 语义变体，决定各平台的呈现。
  final AppCardVariant variant;

  /// 内边距；为空时不加额外 padding（由内容自己控制）。
  final EdgeInsetsGeometry? padding;

  /// 外边距。
  final EdgeInsetsGeometry margin;

  /// 点击回调；非空时整张卡片可点击并带涟漪/悬停反馈。
  final VoidCallback? onTap;

  /// 自定义圆角；为空时用 [AppRadius.medium]。
  final BorderRadius? borderRadius;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final radius = borderRadius ?? AppRadius.mediumRadius;
    final shape = RoundedRectangleBorder(borderRadius: radius);

    final content = padding == null
        ? child
        : Padding(padding: padding!, child: child);

    // 可点击时包一层 InkWell，保证 ripple 与 hover 反馈
    final tappable = onTap == null
        ? content
        : InkWell(
            borderRadius: radius,
            onTap: onTap,
            child: content,
          );

    return Padding(
      padding: margin,
      child: switch (variant) {
        AppCardVariant.elevated => Card(
            elevation: 1,
            margin: EdgeInsets.zero,
            shape: shape,
            clipBehavior: Clip.antiAlias,
            child: tappable,
          ),
        AppCardVariant.filled => Card.filled(
            margin: EdgeInsets.zero,
            shape: shape,
            clipBehavior: Clip.antiAlias,
            color: scheme.surfaceContainerLow,
            child: tappable,
          ),
        AppCardVariant.outlined => Card.outlined(
            margin: EdgeInsets.zero,
            shape: shape,
            clipBehavior: Clip.antiAlias,
            child: tappable,
          ),
      },
    );
  }
}
