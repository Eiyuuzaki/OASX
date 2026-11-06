package com.oas.oasx.mobile.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.oas.oasx.mobile.ui.theme.Spacing

/// M3 语义卡片：按用途区分 container 层级（M3 用 tonal surface 表达 elevation）。
enum class OasCardLevel {
    /// 默认容器：列表项、普通信息块
    Default,
    /// 强调容器：关键指标、当前执行中
    Emphasis,
    /// 低层级：嵌套在卡片内的子区域
    Nested,
}

@Composable
fun OasCard(
    modifier: Modifier = Modifier,
    level: OasCardLevel = OasCardLevel.Default,
    content: @Composable ColumnScope.() -> Unit,
) {
    // M3 规范：elevation 靠 surface-container 色阶，不靠描边/阴影
    val color = when (level) {
        OasCardLevel.Default -> MaterialTheme.colorScheme.surfaceContainerLow
        OasCardLevel.Emphasis -> MaterialTheme.colorScheme.surfaceContainerHigh
        OasCardLevel.Nested -> MaterialTheme.colorScheme.surfaceContainer
    }
    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = color),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
    ) {
        Column(Modifier.padding(Spacing.md), content = content)
    }
}

/// 页面标题：headlineSmall 语义档
@Composable
fun OasPageTitle(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.headlineSmall,
        color = MaterialTheme.colorScheme.onSurface,
        modifier = modifier,
    )
}

/// 区块标题：titleMedium 语义档
@Composable
fun OasSectionTitle(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.titleMedium,
        color = MaterialTheme.colorScheme.onSurface,
        modifier = modifier,
    )
}

/// 卡片内的小标签：labelMedium + onSurfaceVariant
@Composable
fun OasLabel(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.labelMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = modifier,
    )
}

/// 主要数值：headlineMedium（强调）+ primary 色
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

/// 正文：bodyMedium
@Composable
fun OasBody(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.onSurface,
        modifier = modifier,
    )
}

/// 辅助说明：bodySmall + onSurfaceVariant
@Composable
fun OasCaption(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = modifier,
    )
}

/// 错误提示：bodySmall + error（M3 error role）
@Composable
fun OasError(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.error,
        modifier = modifier,
    )
}

/// 统一空/加载/错误三态。任何状态都有明确文案，不会永久转圈。
@Composable
fun OasStateBlock(
    loading: Boolean,
    error: String?,
    emptyText: String? = null,
    modifier: Modifier = Modifier,
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
                OasCaption(emptyText)
            }
        }
    }
}

/// 键值行：左侧 label，右侧 value（列表/详情通用）
@Composable
fun OasKeyValueRow(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    valueColor: androidx.compose.ui.graphics.Color? = null,
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