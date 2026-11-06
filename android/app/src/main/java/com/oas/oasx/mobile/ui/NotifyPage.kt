package com.oas.oasx.mobile.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.oas.oasx.mobile.data.NotifyApi
import com.oas.oasx.mobile.net.Api
import kotlinx.serialization.json.JsonPrimitive

/// 通知推送配置（对齐 WPF 设置页）：provider + 参数 + 启停 + 测试发送。
/// 后端把 provider/params 组装成 YAML 存进脚本配置，客户端只发结构化 JSON。
@Composable
fun NotifyPage(onBack: () -> Unit) {
    var providers by remember { mutableStateOf<Map<String, NotifyApi.Provider>>(emptyMap()) }
    var enable by remember { mutableStateOf(false) }
    var provider by remember { mutableStateOf("null") }
    var cycleHours by remember { mutableStateOf("6") }
    val paramValues = remember { mutableStateMapOf<String, String>() }
    var status by remember { mutableStateOf("") }
    var loading by remember { mutableStateOf(true) }
    var pickProvider by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) {
        loading = true
        try {
            val p = Api.getJsonOrThrow<NotifyApi.ProvidersResp>("/notify/providers")
            providers = p.providers
            val c = Api.getJsonOrThrow<NotifyApi.ConfigResp>("/notify/config")
            enable = c.notifyEnable
            cycleHours = c.cycleHours.toString()
            val (pv, params) = NotifyApi.parseYaml(c.notifyConfig)
            provider = pv
            paramValues.clear()
            params.forEach { (k, v) -> paramValues[k] = v }
            status = ""
        } catch (e: Exception) {
            status = "加载失败：${e.message?.take(100)}"
        }
        loading = false
    }

    val spec = providers[provider]
    val fieldNames = (spec?.required ?: emptyList()) + (spec?.optional ?: emptyList())

    Column(
        Modifier.fillMaxSize().padding(16.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Text(
            "← 返回",
            Modifier.clickable { onBack() },
            fontSize = 14.sp,
            color = MaterialTheme.colorScheme.primary,
        )
        Text("通知推送", fontSize = 22.sp)
        Text(
            "复用 OAS 的 onepush 渠道（结界寄养成熟提醒等）",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        if (loading) {
            Text("读取中…", fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            return@Column
        }

        Card(Modifier.fillMaxWidth()) {
            Row(
                Modifier.fillMaxWidth().padding(16.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(Modifier.weight(1f)) {
                    Text("启用通知", fontSize = 15.sp)
                    Text(
                        "关闭后所有推送静默",
                        fontSize = 11.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Switch(checked = enable, onCheckedChange = { enable = it })
            }
        }

        Text("推送渠道", fontSize = 13.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
        Card(
            Modifier.fillMaxWidth().clickable { pickProvider = true },
        ) {
            Row(
                Modifier.fillMaxWidth().padding(16.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    if (provider == "null" || provider.isBlank()) "未选择" else provider,
                    Modifier.weight(1f),
                    fontSize = 15.sp,
                )
                Text("选择 ›", fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.primary)
            }
        }

        if (provider != "null" && provider.isNotBlank()) {
            Text(
                "参数（标 * 为必填）",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            if (fieldNames.isEmpty()) {
                Text("该渠道无参数定义", fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            fieldNames.forEach { name ->
                val required = spec?.required?.contains(name) == true
                OutlinedTextField(
                    value = paramValues[name] ?: "",
                    onValueChange = { paramValues[name] = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text(if (required) "$name *" else name) },
                    singleLine = true,
                )
            }
            OutlinedTextField(
                value = cycleHours,
                onValueChange = { cycleHours = it.filter { ch -> ch.isDigit() } },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("检查周期（小时）") },
                singleLine = true,
            )
        }

        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Button(
                onClick = {
                    val params = LinkedHashMap<String, String>()
                    paramValues.forEach { (k, v) -> if (v.isNotBlank()) params[k] = v }
                    val missing = spec?.required?.filter { params[it].isNullOrBlank() }
                        ?: emptyList()
                    if (missing.isNotEmpty()) {
                        status = "缺少必填参数：${missing.joinToString(", ")}"
                        return@Button
                    }
                    val body = buildString {
                        append("{\"notify_enable\":").append(enable)
                        append(",\"provider\":\"").append(provider.replace("\"", "\\\""))
                        append("\",\"params\":{")
                        append(
                            params.entries.joinToString(",") { (k, v) ->
                                "\"" + k.replace("\"", "\\\"") + "\":\"" +
                                    v.replace("\\", "\\\\").replace("\"", "\\\"") + "\""
                            },
                        )
                        append("},\"cycle_hours\":")
                        append(cycleHours.toIntOrNull() ?: 6)
                        append("}")
                    }
                    status = "保存中…"
                    Thread {
                        try {
                            val resp = Api.putJsonOrThrow<NotifyApi.SimpleResp>(
                                "/notify/config", body)
                            val msg = if (resp.success) "已保存" else (resp.error ?: "保存失败")
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = msg
                            }
                        } catch (e: Exception) {
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "保存失败：${e.message?.take(100)}"
                            }
                        }
                    }.start()
                },
                modifier = Modifier.weight(1f),
            ) { Text("保存") }

            OutlinedButton(
                onClick = {
                    status = "发送中…"
                    Thread {
                        try {
                            val resp = Api.getJsonOrThrow<NotifyTestResp>("/notify/test")
                            val msg = resp.result ?: if (resp.success) "已发送" else "发送失败"
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = msg
                            }
                        } catch (e: Exception) {
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "测试失败：${e.message?.take(100)}"
                            }
                        }
                    }.start()
                },
                modifier = Modifier.weight(1f),
            ) { Text("测试发送") }
        }

        if (status.isNotEmpty()) {
            Text(status, fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }

    if (pickProvider) {
        AlertDialog(
            onDismissRequest = { pickProvider = false },
            title = { Text("选择推送渠道") },
            text = {
                LazyColumn(Modifier.heightIn(max = 420.dp)) {
                    item {
                        Row(
                            Modifier.fillMaxWidth()
                                .clickable {
                                    provider = "null"
                                    pickProvider = false
                                }
                                .padding(vertical = 10.dp),
                        ) { Text("不使用通知", fontSize = 14.sp) }
                    }
                    items(providers.keys.sorted().toList()) { name ->
                        Row(
                            Modifier.fillMaxWidth()
                                .clickable {
                                    provider = name
                                    pickProvider = false
                                }
                                .padding(vertical = 10.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(
                                name,
                                Modifier.weight(1f),
                                fontSize = 14.sp,
                                color = if (name == provider) MaterialTheme.colorScheme.primary
                                        else MaterialTheme.colorScheme.onSurface,
                            )
                            if (name == provider) {
                                Text("✓", fontSize = 14.sp,
                                    color = MaterialTheme.colorScheme.primary)
                            }
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { pickProvider = false }) { Text("关闭") }
            },
        )
    }
}

/// /notify/test 的返回：{success, result}
@kotlinx.serialization.Serializable
data class NotifyTestResp(
    val success: Boolean = false,
    val result: String? = null,
)