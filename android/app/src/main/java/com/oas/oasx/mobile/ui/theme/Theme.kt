package com.oas.oasx.mobile.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

/// 与 WPF 端一致的品牌色：accent 蓝、成功绿、危险红。
private val Accent = Color(0xFF2563EB)
private val SuccessColor = Color(0xFF16A34A)
private val DangerColor = Color(0xFFDC4545)
private val WarningColor = Color(0xFFD97706)

private val LightColors = lightColorScheme(
    primary = Accent,
    onPrimary = Color.White,
    primaryContainer = Color(0xFFDCE7FF),
    onPrimaryContainer = Color(0xFF0B2A6B),
    secondary = Color(0xFF475569),
    error = DangerColor,
    surface = Color(0xFFFFFFFF),
    onSurface = Color(0xFF1F1F1F),
    onSurfaceVariant = Color(0xFF616161),
    outline = Color(0xFFE5E7EB),
    surfaceVariant = Color(0xFFF3F4F6),
)

private val DarkColors = darkColorScheme(
    primary = Color(0xFF7FA7FF),
    onPrimary = Color(0xFF0B2A6B),
    primaryContainer = Color(0xFF1E3A8A),
    onPrimaryContainer = Color(0xFFDCE7FF),
    secondary = Color(0xFF94A3B8),
    error = Color(0xFFF87171),
    surface = Color(0xFF17181C),
    onSurface = Color(0xFFE5E7EB),
    onSurfaceVariant = Color(0xFF9CA3AF),
    outline = Color(0xFF374151),
    surfaceVariant = Color(0xFF22252B),
)

/// 语义色（非 Material 槽位）：成功/警告，供状态文字使用。
object OasColors {
    val Success = SuccessColor
    val Warning = WarningColor
    val Danger = DangerColor
}

@Composable
fun OasxTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        content = content,
    )
}