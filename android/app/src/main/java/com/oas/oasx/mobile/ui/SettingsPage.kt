package com.oas.oasx.mobile.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Devices
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Palette
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.SwapHoriz
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.oas.oasx.mobile.data.Prefs
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasCard
import com.oas.oasx.mobile.ui.components.OasCardTone
import com.oas.oasx.mobile.ui.components.OasIconBadge
import com.oas.oasx.mobile.ui.components.OasPageHeader
import com.oas.oasx.mobile.ui.components.OasSectionTitle
import com.oas.oasx.mobile.ui.components.contentColorFor
import com.oas.oasx.mobile.ui.theme.Spacing

/// 设置页：后端 / 外观 / 功能 / 关于，全部使用 M3 彩色容器。
@Composable
fun SettingsPage(
    currentBackend: String,
    onThemeChanged: (String) -> Unit,
    onDynamicColorChanged: (Boolean) -> Unit,
    onBackendSaved: (String) -> Unit,
) {
    var url by remember { mutableStateOf(currentBackend) }
    var status by remember { mutableStateOf("") }
    var theme by remember { mutableStateOf(Prefs.themeMode) }
    var dynamicColor by remember { mutableStateOf(Prefs.dynamicColor) }
    var showNotify by remember { mutableStateOf(false) }
    var showDevice by remember { mutableStateOf(false) }
    var showTransfer by remember { mutableStateOf(false) }
    val context = LocalContext.current

    if (showNotify) { NotifyPage(onBack = { showNotify = false }); return }
    if (showDevice) { DevicePage(onBack = { showDevice = false }); return }
    if (showTransfer) {
        TransferPage(onBack = { showTransfer = false }, context = context)
        return
    }

    Column(
        Modifier.fillMaxSize()
            .padding(horizontal = Spacing.md)
            .verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm),
    ) {
        OasPageHeader(
            title = "设置",
            subtitle = "连接、外观与功能",
            icon = Icons.Filled.Settings,
            modifier = Modifier.padding(top = Spacing.md),
        )

        // ── 后端连接 ──
        OasSectionTitle("后端连接")
        OasCard(tone = OasCardTone.Primary) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Filled.Wifi,
                    contentDescription = null,
                    tint = contentColorFor(OasCardTone.Primary),
                    modifier = Modifier.size(20.dp),
                )
                Spacer(Modifier.size(Spacing.sm))
                Text(
                    "OAS 后端地址",
                    style = MaterialTheme.typography.titleSmall,
                    color = contentColorFor(OasCardTone.Primary),
                )
            }
            OutlinedTextField(
                value = url,
                onValueChange = { url = it },
                modifier = Modifier.fillMaxWidth().padding(top = Spacing.sm),
                label = { Text("地址") },
                placeholder = { Text("http://192.168.x.x:22288") },
                singleLine = true,
            )
            TextButton(onClick = {
                val base = url.trim().trimEnd('/')
                status = "测试中…"
                Thread {
                    try {
                        Api.baseUrl = base
                        val list = Api.get("/config_list")
                        Prefs.backend = base
                        onBackendSaved(base)
                        android.os.Handler(android.os.Looper.getMainLooper()).post {
                            status = "连接成功：$list"
                        }
                    } catch (e: Exception) {
                        android.os.Handler(android.os.Looper.getMainLooper()).post {
                            status = "连接失败：${e.message?.take(80)}"
                        }
                    }
                }.start()
            }) { Text("保存并测试", style = MaterialTheme.typography.labelLarge) }
            if (status.isNotEmpty()) {
                Text(
                    status,
                    style = MaterialTheme.typography.bodySmall,
                    color = contentColorFor(OasCardTone.Primary),
                )
            }
        }

        // ── 外观 ──
        OasSectionTitle("外观")
        OasCard(tone = OasCardTone.Secondary) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Filled.Palette,
                    contentDescription = null,
                    tint = contentColorFor(OasCardTone.Secondary),
                    modifier = Modifier.size(20.dp),
                )
                Spacer(Modifier.size(Spacing.sm))
                Text(
                    "主题",
                    style = MaterialTheme.typography.titleSmall,
                    color = contentColorFor(OasCardTone.Secondary),
                )
            }
            listOf("system" to "跟随系统", "light" to "浅色", "dark" to "深色")
                .forEach { (mode, label) ->
                    Row(
                        Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        RadioButton(
                            selected = theme == mode,
                            onClick = {
                                theme = mode
                                Prefs.themeMode = mode
                                onThemeChanged(mode)
                            },
                        )
                        Text(
                            label,
                            style = MaterialTheme.typography.bodyMedium,
                            color = contentColorFor(OasCardTone.Secondary),
                        )
                    }
                }
            Row(
                Modifier.fillMaxWidth().padding(top = Spacing.sm),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(Modifier.weight(1f)) {
                    Text(
                        "动态取色",
                        style = MaterialTheme.typography.bodyMedium,
                        color = contentColorFor(OasCardTone.Secondary),
                    )
                    Text(
                        "Android 12+ 从壁纸取色（M3 核心特性）",
                        style = MaterialTheme.typography.bodySmall,
                        color = contentColorFor(OasCardTone.Secondary).copy(alpha = 0.75f),
                    )
                }
                Switch(
                    checked = dynamicColor,
                    onCheckedChange = {
                        dynamicColor = it
                        Prefs.dynamicColor = it
                        onDynamicColorChanged(it)
                    },
                )
            }
        }

        // ── 功能入口 ──
        OasSectionTitle("功能")
        listOf(
            Triple("导出 / 导入配置", "备份到手机文件，或从文件导入",
                Icons.Filled.SwapHoriz to 0),
            Triple("设备与实例", "切换当前调度器使用的模拟器",
                Icons.Filled.Devices to 1),
            Triple("通知推送", "结界寄养成熟提醒（onepush 渠道）",
                Icons.Filled.Notifications to 2),
        ).forEach { (title, sub, iconAndIdx) ->
            val (icon, which) = iconAndIdx
            OasCard(
                tone = OasCardTone.Neutral,
                modifier = Modifier.clickable {
                    when (which) {
                        0 -> showTransfer = true
                        1 -> showDevice = true
                        else -> showNotify = true
                    }
                },
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    OasIconBadge(icon, tone = OasCardTone.Tertiary, size = 36.dp)
                    Spacer(Modifier.size(Spacing.sm))
                    Column(Modifier.weight(1f)) {
                        OasBody(title)
                        OasCaption(sub)
                    }
                    Text(
                        "›",
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
            }
        }

        // ── 关于 ──
        OasSectionTitle("关于")
        OasCard(tone = OasCardTone.NeutralHigh) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                OasIconBadge(Icons.Filled.Info, tone = OasCardTone.Primary, size = 36.dp)
                Spacer(Modifier.size(Spacing.sm))
                Column(Modifier.weight(1f)) {
                    OasBody("OASX Android v0.1.0")
                    OasCaption("OAS 自动化脚本管理客户端")
                    OasCaption("github.com/Eiyuuzaki/OASX")
                }
            }
        }

        Spacer(Modifier.size(Spacing.md))
    }
}