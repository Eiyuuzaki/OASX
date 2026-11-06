package com.oas.oasx.mobile.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.oas.oasx.mobile.data.Prefs
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasCard
import com.oas.oasx.mobile.ui.components.OasPageTitle
import com.oas.oasx.mobile.ui.components.OasSectionTitle
import com.oas.oasx.mobile.ui.theme.Spacing

/// 设置页：后端地址、主题、动态取色、子页面入口、关于。
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

    if (showNotify) {
        NotifyPage(onBack = { showNotify = false })
        return
    }
    if (showDevice) {
        DevicePage(onBack = { showDevice = false })
        return
    }
    if (showTransfer) {
        TransferPage(onBack = { showTransfer = false }, context = context)
        return
    }

    Column(
        Modifier.fillMaxSize().padding(horizontal = Spacing.md).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm),
    ) {
        OasPageTitle("设置", Modifier.padding(top = Spacing.md))

        // 后端地址
        OasSectionTitle("后端连接")
        OasCard {
            OutlinedTextField(
                value = url,
                onValueChange = { url = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("OAS 后端地址") },
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
            if (status.isNotEmpty()) OasCaption(status)
        }

        // 外观
        OasSectionTitle("外观")
        OasCard {
            OasCaption("主题")
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
                        OasBody(label)
                    }
                }
            Row(
                Modifier.fillMaxWidth().padding(top = Spacing.sm),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(Modifier.weight(1f)) {
                    OasBody("动态取色")
                    OasCaption("Android 12+ 从壁纸取色（M3 核心特性）")
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

        // 功能入口
        OasSectionTitle("功能")
        listOf(
            Triple("导出 / 导入配置", "备份到手机文件，或从文件导入", 0),
            Triple("设备与实例", "切换当前调度器使用的模拟器", 1),
            Triple("通知推送", "结界寄养成熟提醒（onepush 渠道）", 2),
        ).forEach { (title, sub, which) ->
            OasCard(
                modifier = Modifier.clickable {
                    when (which) {
                        0 -> showTransfer = true
                        1 -> showDevice = true
                        else -> showNotify = true
                    }
                },
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
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

        // 关于
        OasSectionTitle("关于")
        OasCard {
            OasBody("OASX Android v0.1.0")
            OasCaption("OAS 自动化脚本管理客户端")
            OasCaption("github.com/Eiyuuzaki/OASX")
        }

        Text("", Modifier.padding(bottom = Spacing.md))
    }
}