package com.oas.oasx.mobile.ui

import android.content.SharedPreferences
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.material3.Button
import androidx.compose.material3.OutlinedTextField
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.oas.oasx.mobile.data.Prefs
import com.oas.oasx.mobile.net.Api

/// 设置页：后端地址、主题模式、关于。
/// 主题与后端地址由 MainActivity 持有（状态上浮），本页仅回调。
@Composable
fun SettingsPage(
    currentBackend: String,
    onThemeChanged: (String) -> Unit,
    onBackendSaved: (String) -> Unit,
) {
    var url by remember { mutableStateOf(currentBackend) }
    var status by remember { mutableStateOf("") }
    var theme by remember { mutableStateOf(Prefs.themeMode) }
    var showNotify by remember { mutableStateOf(false) }
    var showDevice by remember { mutableStateOf(false) }
    var showTransfer by remember { mutableStateOf(false) }

    if (showNotify) {
        NotifyPage(onBack = { showNotify = false })
        return
    }
    if (showDevice) {
        DevicePage(onBack = { showDevice = false })
        return
    }
    if (showTransfer) {
        TransferPage(
            onBack = { showTransfer = false },
            context = androidx.compose.ui.platform.LocalContext.current,
        )
        return
    }

    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("设置", style = MaterialTheme.typography.headlineSmall)

        Text("OAS 后端地址", style = MaterialTheme.typography.bodyMedium)
        OutlinedTextField(
            value = url,
            onValueChange = { url = it },
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text("http://192.168.x.x:22288") },
            singleLine = true,
        )
        Button(onClick = {
            val base = url.trim().trimEnd('/')
            status = "测试中…"
            Thread {
                try {
                    Api.baseUrl = base
                    val list = Api.get("/config_list")
                    Prefs.backend = base
                    onBackendSaved(base)
                    status = "连接成功：$list"
                } catch (e: Exception) {
                    status = "连接失败：${e.message?.take(80)}"
                }
            }.start()
        }) { Text("保存并测试") }
        if (status.isNotEmpty()) {
            Text(status, style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }

        Text("主题", style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(top = 12.dp))
        listOf(
            "system" to "跟随系统",
            "light" to "浅色",
            "dark" to "深色",
        ).forEach { (mode, label) ->
            Row(Modifier.fillMaxWidth()) {
                androidx.compose.material3.RadioButton(
                    selected = theme == mode,
                    onClick = {
                        theme = mode
                        Prefs.themeMode = mode
                        onThemeChanged(mode)
                    },
                )
                Text(label, modifier = Modifier.padding(top = 14.dp))
            }
        }

        Text("模拟器", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(top = 12.dp))
        Card(Modifier.fillMaxWidth().clickable { showDevice = true }) {
            Row(
                Modifier.fillMaxWidth().padding(16.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(Modifier.weight(1f)) {
                    Text("设备与实例", style = MaterialTheme.typography.titleSmall)
                    Text(
                        "切换当前调度器使用的模拟器",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Text("›", style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.primary)
            }
        }

        Text("配置传输", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(top = 12.dp))
        Card(Modifier.fillMaxWidth().clickable { showTransfer = true }) {
            Row(
                Modifier.fillMaxWidth().padding(16.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(Modifier.weight(1f)) {
                    Text("导出 / 导入配置", style = MaterialTheme.typography.titleSmall)
                    Text(
                        "备份到手机文件，或从文件导入",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Text("›", style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.primary)
            }
        }

        Text("通知推送", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(top = 12.dp))
        Card(Modifier.fillMaxWidth().clickable { showNotify = true }) {
            Row(
                Modifier.fillMaxWidth().padding(16.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(Modifier.weight(1f)) {
                    Text("推送渠道与参数", style = MaterialTheme.typography.titleSmall)
                    Text(
                        "结界寄养成熟提醒（onepush 渠道）",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Text("›", style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.primary)
            }
        }

        Text("关于", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(top = 12.dp))
        Text(
            "OASX Android v0.1.0 · OAS 自动化脚本管理客户端\n" +
                "github.com/Eiyuuzaki/OASX",
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}