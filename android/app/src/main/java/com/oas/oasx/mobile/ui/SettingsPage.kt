package com.oas.oasx.mobile.ui

import android.content.SharedPreferences
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.material3.Button
import androidx.compose.material3.OutlinedTextField
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

    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("设置", fontSize = 22.sp)

        Text("OAS 后端地址", fontSize = 13.sp)
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
            Text(status, fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }

        Text("主题", fontSize = 13.sp,
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

        Text("关于", fontSize = 13.sp, modifier = Modifier.padding(top = 12.dp))
        Text(
            "OASX Android v0.1.0 · OAS 自动化脚本管理客户端\n" +
                "github.com/Eiyuuzaki/OASX",
            fontSize = 11.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}