package com.oas.oasx.mobile.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import com.oas.oasx.mobile.ui.theme.Spacing

/// M3 卡片色阶：surface 灰阶 + primary/secondary/tertiary 彩色容器。
///
/// M3 的视觉丰富度来自 color roles 的对比，不是灰度层次。
/// 关键指标用 primaryContainer，次级信息用 secondaryContainer，
/// 辅助信息用 tertiaryContainer——这样界面才有"色彩"而不只是"深浅"。
enum class OasCardTone {
    /// 默认容器（灰阶低）
    Neutral,
    /// 强调容器（灰阶高）
    NeutralHigh,
    /// 主色容器：关键指标、当前执行
    Primary,
    /// 次色容器：次要信息块
    Secondary,
    /// 三色容器：辅助/补充信息
    Tertiary,
    /// 错误容器：异常提示
    Error,
}

@Composable
fun OasCard(
    modifier: Modifier = Modifier,
    tone: OasCardTone = OasCardTone.Neutral,
    content: @Composable ColumnScope.() -> Unit,
) {
    val cs = MaterialTheme.colorScheme
    val container = when (tone) {
        OasCardTone.Neutral -> cs.surfaceContainerLow
        OasCardTone.NeutralHigh -> cs.surfaceContainerHigh
        OasCardTone.Primary -> cs.primaryContainer
        OasCardTone.Secondary -> cs.secondaryContainer
        OasCardTone.Tertiary -> cs.tertiaryContainer
        OasCardTone.Error -> cs.errorContainer
    }
    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = container),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
    ) {
        Column(Modifier.padding(Spacing.md), content = content)
    }
}

/// 卡片内容色：与容器配对（M3 要求 on-X 配 X）
@Composable
fun contentColorFor(tone: OasCardTone): Color {
    val cs = MaterialTheme.colorScheme
    return when (tone) {
        OasCardTone.Neutral, OasCardTone.NeutralHigh -> cs.onSurface
        OasCardTone.Primary -> cs.onPrimaryContainer
        OasCardTone.Secondary -> cs.onSecondaryContainer
        OasCardTone.Tertiary -> cs.onTertiaryContainer
        OasCardTone.Error -> cs.onErrorContainer
    }
}

@Composable
fun captionColorFor(tone: OasCardTone): Color {
    val cs = MaterialTheme.colorScheme
    return when (tone) {
        OasCardTone.Neutral, OasCardTone.NeutralHigh -> cs.onSurfaceVariant
        OasCardTone.Primary -> cs.onPrimaryContainer.copy(alpha = 0.75f)
        OasCardTone.Secondary -> cs.onSecondaryContainer.copy(alpha = 0.75f)
        OasCardTone.Tertiary -> cs.onTertiaryContainer.copy(alpha = 0.75f)
        OasCardTone.Error -> cs.onErrorContainer.copy(alpha = 0.75f)
    }
}

/// 圆形图标徽章：M3 用容器色 + 图标建立视觉锚点
@Composable
fun OasIconBadge(
    icon: ImageVector,
    modifier: Modifier = Modifier,
    tone: OasCardTone = OasCardTone.Primary,
    size: androidx.compose.ui.unit.Dp = 40.dp,
) {
    val cs = MaterialTheme.colorScheme
    val bg = when (tone) {
        OasCardTone.Neutral, OasCardTone.NeutralHigh -> cs.surfaceContainerHighest
        OasCardTone.Primary -> cs.primaryContainer
        OasCardTone.Secondary -> cs.secondaryContainer
        OasCardTone.Tertiary -> cs.tertiaryContainer
        OasCardTone.Error -> cs.errorContainer
    }
    Box(
        modifier = modifier.size(size).background(bg, CircleShape),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            icon,
            contentDescription = null,
            tint = contentColorFor(tone),
            modifier = Modifier.size(size * 0.5f),
        )
    }
}

/// 指标卡：图标 + 标签 + 大数值（M3 强调层级）
@Composable
fun OasStatCard(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    icon: ImageVector? = null,
    tone: OasCardTone = OasCardTone.Primary,
    supporting: String? = null,
) {
    OasCard(modifier = modifier, tone = tone) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            if (icon != null) {
                OasIconBadge(icon, tone = OasCardTone.Neutral, size = 36.dp)
                Spacer(Modifier.size(Spacing.sm))
            }
            Column(Modifier.weight(1f)) {
                Text(
                    label,
                    style = MaterialTheme.typography.labelMedium,
                    color = captionColorFor(tone),
                )
                Text(
                    value,
                    style = MaterialTheme.typography.headlineSmall,
                    color = contentColorFor(tone),
                )
                if (supporting != null) {
                    Text(
                        supporting,
                        style = MaterialTheme.typography.bodySmall,
                        color = captionColorFor(tone),
                    )
                }
            }
        }
    }
}

/// 页面标题（带可选图标徽章）
@Composable
fun OasPageHeader(
    title: String,
    modifier: Modifier = Modifier,
    subtitle: String? = null,
    icon: ImageVector? = null,
    action: (@Composable () -> Unit)? = null,
) {
    Row(
        modifier = modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (icon != null) {
            OasIconBadge(icon, tone = OasCardTone.Primary, size = 44.dp)
            Spacer(Modifier.size(Spacing.sm))
        }
        Column(Modifier.weight(1f)) {
            Text(
                title,
                style = MaterialTheme.typography.headlineSmall,
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (subtitle != null) {
                Text(
                    subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        action?.invoke()
    }
}

/// 页面标题（兼容旧调用）
@Composable
fun OasPageTitle(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.headlineSmall,
        color = MaterialTheme.colorScheme.onSurface,
        modifier = modifier,
    )
}

@Composable
fun OasSectionTitle(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.titleMedium,
        color = MaterialTheme.colorScheme.onSurface,
        modifier = modifier,
    )
}

@Composable
fun OasLabel(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.labelMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = modifier,
    )
}

@Composable
fun OasMetric(
    text: String,
    modifier: Modifier = Modifier,
    emphasize: Boolean = true,
) {
    Text(
        text = text,
        style = MaterialTheme.typography.headlineMedium,
        color = if (emphasize) MaterialTheme.colorScheme.primary
                else MaterialTheme.colorScheme.onSurface,
        modifier = modifier,
    )
}

@Composable
fun OasBody(
    text: String,
    modifier: Modifier = Modifier,
    color: Color = MaterialTheme.colorScheme.onSurface,
) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodyMedium,
        color = color,
        modifier = modifier,
    )
}

@Composable
fun OasCaption(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = modifier,
    )
}

@Composable
fun OasError(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.error,
        modifier = modifier,
    )
}

/// 统一空/加载/错误三态（空态带图标，符合 M3 引导性空态做法）
@Composable
fun OasStateBlock(
    loading: Boolean,
    error: String?,
    emptyText: String? = null,
    modifier: Modifier = Modifier,
    emptyIcon: ImageVector? = null,
    onRetry: (() -> Unit)? = null,
) {
    Column(
        modifier = modifier.fillMaxWidth().padding(Spacing.lg),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        when {
            loading -> {
                CircularProgressIndicator(strokeWidth = 2.dp)
                Text(
                    "加载中…",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = Spacing.md),
                )
            }
            error != null -> {
                OasError(error)
                if (onRetry != null) {
                    TextButton(onClick = onRetry) {
                        Text("重试", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }
            emptyText != null -> {
                if (emptyIcon != null) {
                    Icon(
                        emptyIcon,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f),
                        modifier = Modifier.size(48.dp),
                    )
                    Spacer(Modifier.size(Spacing.sm))
                }
                OasCaption(emptyText)
            }
        }
    }
}

@Composable
fun OasKeyValueRow(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    valueColor: Color? = null,
) {
    Row(
        modifier = modifier.fillMaxWidth().padding(vertical = Spacing.xs),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
        Text(
            value,
            style = MaterialTheme.typography.bodyMedium,
            color = valueColor ?: MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}