package com.oas.oasx.mobile.ui

import android.content.Context
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.oas.oasx.mobile.data.TransferApi
import com.oas.oasx.mobile.net.Api
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File

/// 配置传输：导出（整份/单任务、备份/分享）与导入（选择 .json 文件）。
///
/// 对齐 WPF TransferPage。后端约定用 JSON 而非 multipart。
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TransferPage(onBack: () -> Unit, context: Context) {
    var caps by remember { mutableStateOf<TransferApi.Capabilities?>(null) }
    var configs by remember { mutableStateOf<List<String>>(emptyList()) }
    var config by remember { mutableStateOf<String?>(null) }
    var mode by remember { mutableStateOf("backup") }
    var byTask by remember { mutableIntStateOf(0) }   // 0=整份 1=单任务
    var tasks by remember { mutableStateOf<List<String>>(emptyList()) }
    var task by remember { mutableStateOf<String?>(null) }
    var preview by remember { mutableStateOf("") }
    var status by remember { mutableStateOf("") }
    var busy by remember { mutableStateOf(false) }
    var pickConfig by remember { mutableStateOf(false) }
    var pickTask by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) {
        try {
            caps = withContext(Dispatchers.IO) { TransferApi.capabilities() }
            configs = withContext(Dispatchers.IO) { Api.configList() }
            config = configs.firstOrNull()
        } catch (e: Exception) {
            status = "初始化失败：${e.message?.take(80)}"
        }
    }

    LaunchedEffect(byTask, config) {
        if (byTask == 1 && tasks.isEmpty()) {
            try {
                tasks = withContext(Dispatchers.IO) {
                    val raw = Api.get("/script_menu")
                    val map = Api.json.decodeFromString<Map<String, List<String>>>(raw)
                    map.values.flatten().distinct().sorted()
                }
                task = tasks.firstOrNull()
            } catch (e: Exception) {
                status = "任务列表加载失败：${e.message?.take(80)}"
            }
        }
    }

    val capOk = caps?.let { it.version == 1 && it.configImport && it.taskImport } == true

    Column(
        Modifier.fillMaxSize().padding(16.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Text(
            "← 返回",
            Modifier.padding(end = 8.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.primary,
        )
        Text("配置传输", style = MaterialTheme.typography.headlineSmall)
        Text(
            caps?.let { "后端支持 v${it.version} · 上限 ${it.maxBytes / 1024} KB" }
                ?: "读取后端能力…",
            style = MaterialTheme.typography.bodySmall,
            color = if (capOk) MaterialTheme.colorScheme.onSurfaceVariant
                    else MaterialTheme.colorScheme.error,
        )

        // ── 配置选择 ──
        ExposedDropdownMenuBox(expanded = pickConfig, onExpandedChange = { pickConfig = !pickConfig }) {
            OutlinedTextField(
                value = config ?: "（未选择）",
                onValueChange = {},
                readOnly = true,
                label = { Text("配置") },
                trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = pickConfig) },
                modifier = Modifier.menuAnchor().fillMaxWidth(),
                singleLine = true,
            )
            ExposedDropdownMenu(expanded = pickConfig, onDismissRequest = { pickConfig = false }) {
                configs.forEach { c ->
                    DropdownMenuItem(
                        text = { Text(c) },
                        onClick = { config = c; pickConfig = false },
                    )
                }
            }
        }

        // ── 范围 + 模式 ──
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(selected = byTask == 0, onClick = { byTask = 0 },
                label = { Text("整份配置", style = MaterialTheme.typography.bodySmall) })
            FilterChip(selected = byTask == 1, onClick = { byTask = 1 },
                label = { Text("单个任务", style = MaterialTheme.typography.bodySmall) })
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(selected = mode == "backup", onClick = { mode = "backup" },
                label = { Text("备份（含敏感）", style = MaterialTheme.typography.bodySmall) })
            FilterChip(selected = mode == "share", onClick = { mode = "share" },
                label = { Text("分享（脱敏）", style = MaterialTheme.typography.bodySmall) })
        }

        if (byTask == 1) {
            ExposedDropdownMenuBox(expanded = pickTask, onExpandedChange = { pickTask = !pickTask }) {
                OutlinedTextField(
                    value = task ?: "（未选择任务）",
                    onValueChange = {},
                    readOnly = true,
                    label = { Text("任务") },
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = pickTask) },
                    modifier = Modifier.menuAnchor().fillMaxWidth(),
                    singleLine = true,
                )
                ExposedDropdownMenu(expanded = pickTask, onDismissRequest = { pickTask = false }) {
                    tasks.forEach { t ->
                        DropdownMenuItem(
                            text = { Text(t) },
                            onClick = { task = t; pickTask = false },
                        )
                    }
                }
            }
        }

        // ── 导出 ──
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            TextButton(
                enabled = capOk && !busy && config != null,
                onClick = {
                    val cfg = config ?: return@TextButton
                    busy = true
                    status = "生成中…"
                    Thread {
                        try {
                            val raw = kotlinx.coroutines.runBlocking {
                                if (byTask == 1) {
                                    TransferApi.exportTask(cfg, task ?: "", mode)
                                } else {
                                    TransferApi.exportConfig(cfg, mode)
                                }
                            }
                            val name = if (byTask == 1) {
                                "${cfg}-${task}-${mode}.json"
                            } else {
                                "${cfg}-${mode}.json"
                            }
                            val path = saveToDownloads(context, name, raw)
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "已导出：$path"
                                preview = raw.take(2000)
                                busy = false
                            }
                        } catch (e: Exception) {
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "导出失败：${e.message?.take(80)}"
                                busy = false
                            }
                        }
                    }.start()
                },
            ) { Text("导出到文件", style = MaterialTheme.typography.bodyMedium) }

            TextButton(
                enabled = capOk && !busy && config != null,
                onClick = {
                    val cfg = config ?: return@TextButton
                    busy = true
                    Thread {
                        try {
                            val raw = kotlinx.coroutines.runBlocking {
                                if (byTask == 1) TransferApi.exportTask(cfg, task ?: "", mode)
                                else TransferApi.exportConfig(cfg, mode)
                            }
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                preview = raw.take(2000)
                                status = "预览（前 2000 字符）"
                                busy = false
                            }
                        } catch (e: Exception) {
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "预览失败：${e.message?.take(80)}"
                                busy = false
                            }
                        }
                    }.start()
                },
            ) { Text("预览", style = MaterialTheme.typography.bodyMedium) }
        }

        // ── 预览区 ──
        if (preview.isNotBlank()) {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(10.dp)) {
                    Text(preview, style = MaterialTheme.typography.labelSmall,
                        fontFamily = FontFamily.Monospace,
                        color = MaterialTheme.colorScheme.onSurface)
                }
            }
        }

        // ── 导入 ──
        Text("导入", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(top = 8.dp))
        Text(
            if (byTask == 1) "把任务参数写入所选配置"
            else "整份导入会新建配置（不覆盖已有）",
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        TextButton(
            enabled = capOk && !busy && config != null,
            onClick = {
                val cfg = config ?: return@TextButton
                val file = pickJsonFile(context)
                if (file == null) {
                    status = "未找到可导入的 .json（请先放到 Download/OASX 目录）"
                    return@TextButton
                }
                busy = true
                status = "导入中…"
                Thread {
                    try {
                        val raw = file.readText()
                        val res = kotlinx.coroutines.runBlocking {
                            if (byTask == 1) {
                                TransferApi.importTask(cfg, task ?: "", raw)
                            } else {
                                TransferApi.importConfig(raw)
                            }
                        }
                        val msg = buildString {
                            if (res.detail != null) append("失败：${res.detail}")
                            else append("导入成功")
                            res.name?.let { append(" · 配置名 $it") }
                            res.warnings.forEach { append("\n· $it") }
                        }
                        android.os.Handler(android.os.Looper.getMainLooper()).post {
                            status = msg
                            busy = false
                        }
                    } catch (e: Exception) {
                        android.os.Handler(android.os.Looper.getMainLooper()).post {
                            status = "导入失败：${e.message?.take(80)}"
                            busy = false
                        }
                    }
                }.start()
            },
        ) { Text("选择文件并导入", style = MaterialTheme.typography.bodyMedium) }

        if (status.isNotBlank()) {
            Text(status, style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

/// 保存到应用外部文件目录（无需存储权限）：Android/data/<pkg>/files/OASX/
private fun saveToDownloads(context: Context, name: String, content: String): String {
    val dir = File(context.getExternalFilesDir(null), "OASX")
    if (!dir.exists()) dir.mkdirs()
    val f = File(dir, name)
    f.writeText(content)
    return f.absolutePath
}

/// 从应用目录里找第一个 .json（受限于 Android 分区存储，不做系统文件选择器）
private fun pickJsonFile(context: Context): File? {
    val dir = File(context.getExternalFilesDir(null), "OASX")
    if (!dir.exists()) return null
    return dir.listFiles { f -> f.isFile && f.name.endsWith(".json") }
        ?.maxByOrNull { it.lastModified() }
}