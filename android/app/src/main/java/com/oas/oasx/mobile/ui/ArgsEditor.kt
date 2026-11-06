package com.oas.oasx.mobile.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.oas.oasx.mobile.data.I18n
import com.oas.oasx.mobile.net.Api
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

/// 任务参数编辑器（对齐 WPF ArgsPage）。
///
/// 数据源 `GET /{script}/{task}/args` → `{组名: [{name,title,description,value,type}]}`。
/// 写入 `PUT /{script}/{task}/{group}/{arg}/value?types=<type>&value=<urlencoded>`。
/// 后端一次只收一个参数，所以按字段逐个提交。
@Composable
fun ArgsEditor(script: String, task: String, onBack: () -> Unit) {
    var argsJson by remember { mutableStateOf<String?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    var status by remember { mutableStateOf("") }
    var reload by remember { mutableStateOf(0) }
    var editing by remember { mutableStateOf<FieldEdit?>(null) }

    LaunchedEffect(script, task, reload) {
        try {
            argsJson = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.get("/$script/$task/args")
            }
            error = null
        } catch (e: Exception) {
            error = e.message?.take(100)
        }
    }

    val parsed = remember(argsJson) {
        argsJson?.let {
            try {
                Api.json.decodeFromString<Map<String, List<JsonObject>>>(it)
            } catch (e: Exception) {
                null
            }
        }
    }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Text(
            "← 返回",
            Modifier.clickable { onBack() },
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.primary,
        )
        Text(
            I18n.t(task),
            style = MaterialTheme.typography.headlineSmall,
            modifier = Modifier.padding(top = 8.dp),
        )
        Text(
            "参数 · $script · 点任意项修改",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        if (status.isNotEmpty()) {
            Text(status, style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(bottom = 6.dp))
        }

        when {
            error != null -> Text("加载失败：$error", style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.error)
            parsed == null -> Text("解析中…", style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            else -> LazyColumn(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                parsed.forEach { (group, fields) ->
                    item {
                        Text(
                            I18n.t(group),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.padding(top = 10.dp, bottom = 4.dp),
                        )
                    }
                    items(fields) { f ->
                        val name = jStr(f, "name")
                        val title = jStr(f, "title").ifBlank { name }
                        val type = jStr(f, "type")
                        val value = jStr(f, "value")
                        Card(
                            Modifier.fillMaxWidth().clickable {
                                editing = FieldEdit(group, name, title, type, value)
                            },
                        ) {
                            Row(
                                Modifier.fillMaxWidth().padding(12.dp, 10.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Column(Modifier.weight(1f)) {
                                    Text(I18n.t(title), style = MaterialTheme.typography.bodyMedium)
                                    if (type.isNotBlank()) {
                                        Text(type, style = MaterialTheme.typography.labelSmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    }
                                }
                                Text(
                                    displayValue(type, value),
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                    }
                }
            }
        }
    }

    val edit = editing
    if (edit != null) {
        EditFieldDialog(
            edit = edit,
            onDismiss = { editing = null },
            onConfirm = { newValue ->
                editing = null
                status = "保存中…"
                Thread {
                    try {
                        val path = "/$script/$task/${enc(edit.group)}/${enc(edit.name)}/value" +
                            "?types=${enc(edit.type)}&value=${enc(newValue)}"
                        Api.put(path, "")
                        android.os.Handler(android.os.Looper.getMainLooper()).post {
                            status = "已保存：${I18n.t(edit.title)}"
                            reload++
                        }
                    } catch (e: Exception) {
                        android.os.Handler(android.os.Looper.getMainLooper()).post {
                            status = "保存失败：${e.message?.take(100)}"
                        }
                    }
                }.start()
            },
        )
    }
}

data class FieldEdit(
    val group: String,
    val name: String,
    val title: String,
    val type: String,
    val value: String,
)

private fun jStr(o: JsonObject, key: String): String {
    val el = o[key] ?: return ""
    return if (el is JsonPrimitive) el.content else el.toString().trim('"')
}

private fun enc(s: String): String = java.net.URLEncoder.encode(s, "UTF-8")

/// 布尔显示为 开/关，其余直出。
private fun displayValue(type: String, value: String): String = when {
    type == "boolean" -> if (value.equals("true", true) || value == "1") "开" else "关"
    value.isBlank() -> "—"
    else -> value
}

@Composable
private fun EditFieldDialog(
    edit: FieldEdit,
    onDismiss: () -> Unit,
    onConfirm: (String) -> Unit,
) {
    var text by remember(edit) { mutableStateOf(edit.value) }
    val isBool = edit.type == "boolean"
    var boolValue by remember(edit) {
        mutableStateOf(edit.value.equals("true", true) || edit.value == "1")
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(I18n.t(edit.title)) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                if (edit.type.isNotBlank()) {
                    Text(edit.type, style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (isBool) {
                    Row(
                        Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Text("启用", Modifier.weight(1f), style = MaterialTheme.typography.bodyMedium)
                        Switch(
                            checked = boolValue,
                            onCheckedChange = { boolValue = it },
                        )
                    }
                } else {
                    OutlinedTextField(
                        value = text,
                        onValueChange = { text = it },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        supportingText = {
                            val hint = when (edit.type) {
                                "date_time" -> "格式 2026-01-01 00:00:00"
                                "time" -> "格式 00:00:00"
                                "time_delta" -> "格式 T0:00:00:00"
                                "integer", "number" -> "数字"
                                else -> ""
                            }
                            if (hint.isNotEmpty()) Text(hint, style = MaterialTheme.typography.labelSmall)
                        },
                    )
                }
            }
        },
        confirmButton = {
            TextButton(onClick = {
                onConfirm(if (isBool) boolValue.toString() else text)
            }) { Text("保存") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("取消") }
        },
    )
}