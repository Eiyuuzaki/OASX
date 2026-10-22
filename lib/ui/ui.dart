/// 语义组件层：业务代码只依赖这里的组件，不直接使用平台专属 widget。
///
/// 设计目标：
/// - **语义优先**：用 `AppCard.filled` 表达"这是一个强调容器"，
///   而不是直接写 `Card.filled`（后者把 Material 实现泄露到业务层）
/// - **单点换肤**：各平台的呈现（Fluent / Material / Cupertino）在组件内部决定，
///   业务代码不判断平台
/// - **渐进迁移**：新代码直接用 App* 组件；老代码可逐个替换，不必一次性改完
///
/// 用法：
/// ```dart
/// import 'package:oasx/ui/ui.dart';
///
/// AppCard(
///   variant: AppCardVariant.filled,
///   padding: AppSpacing.cardPadding,
///   child: Text('内容'),
/// )
/// ```
library;

export 'style.dart';
export 'widgets/app_card.dart';
