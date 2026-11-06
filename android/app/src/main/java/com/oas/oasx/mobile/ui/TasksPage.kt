package com.oas.oasx.mobile.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.I18n
import com.oas.oasx.mobile.data.SchedulerSocket
import com.oas.oasx.mobile.net.Api

/// 任务页：任务分组（/script_menu）+ 脚本启停 + 运行中高亮 + 参数只读查看。
@Composable
fun TasksPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    val script = account?.config
    val i18nMap by I18n.map.collectAsState()
    val t: (String) -> String = { key -> i18nMap[key] ?: key }

    LaunchedEffect(Unit) { I18n.ensureLoaded() }

    var menu by remember(script) { mutableStateOf<Map<String, List<String>>?>(null) }
    var status by remember(script) { mutableStateOf("") }
    var confirmStop by remember(script) { mutableStateOf(false) }
    var argsTask by remember(script) { mutableStateOf<String?>(null) }

    // 运行中任务（WS 实时）
    var runningTask by remember(script) { mutableStateOf<String?>(null) }
    LaunchedEffect(script, backend) {
        if (script == null) return@LaunchedEffect
        SchedulerSocket().connect(script, backend).collect { snap ->
            runningTask = snap.schedule?.running?.name
        }
    }

    LaunchedEffect(script) {
        if (script == null) return@LaunchedEffect
        try {
            menu = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.getJsonOrThrow<Map<String, List<String>>>("/script_menu")
            }
        } catch (e: Exception) {
            status = "任务菜单加载失败：${e.message?.take(60)}"
        }
    }

    // 参数只读视图（点任务名进入）
    if (argsTask != null && script != null) {
        ArgsEditor(
            script = script,
            task = argsTask ?: "",
            onBack = { argsTask = null },
        )
        return
    }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Text("任务", fontSize = 22.sp)
        Text(
            "调度器 ${script ?: "—"} · 点任务名查看参数（只读）",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(bottom = 12.dp),
        )

        Row(Modifier.fillMaxWidth().padding(bottom = 12.dp)) {
            Button(
                onClick = {
                    if (script == null) return@Button
                    Thread {
                        try {
                            Api.get("/$script/start")
                            status = "已发送启动"
                        } catch (e: Exception) {
                            status = "启动失败：${e.message?.take(60)}"
                        }
                    }.start()
                },
                modifier = Modifier.weight(1f),
            ) { Text("启动") }
            androidx.compose.foundation.layout.Spacer(Modifier.padding(6.dp))
            OutlinedButton(
                onClick = { confirmStop = true },
                modifier = Modifier.weight(1f),
            ) { Text("停止") }
        }

        if (status.isNotEmpty()) {
            Text(status, fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(bottom = 8.dp))
        }

        val m = menu
        if (m == null) {
            Text("任务菜单加载中…", fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            return@Column
        }

        LazyColumn {
            m.forEach { (group, tasks) ->
                item {
                    Text(t(group), fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.padding(top = 10.dp, bottom = 4.dp))
                }
                items(tasks) { task ->
                    val isRunning = runningTask == task
                    Card(
                        Modifier.fillMaxWidth().padding(vertical = 2.dp)
                            .clickable { argsTask = task },
                    ) {
                        Row(
                            Modifier.fillMaxWidth().padding(12.dp, 8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(
                                t(task),
                                Modifier.weight(1f),
                                fontSize = 13.sp,
                                color = if (isRunning) MaterialTheme.colorScheme.primary
                                        else MaterialTheme.colorScheme.onSurface,
                            )
                            if (isRunning) {
                                Text("● 运行中", fontSize = 11.sp,
                                    color = MaterialTheme.colorScheme.primary)
                            }
                        }
                    }
                }
            }
        }
    }

    // 停止确认对话框
    if (confirmStop && script != null) {
        androidx.compose.material3.AlertDialog(
            onDismissRequest = { confirmStop = false },
            title = { Text("停止脚本") },
            text = { Text("确认停止 $script 的脚本执行？") },
            confirmButton = {
                androidx.compose.material3.TextButton(onClick = {
                    confirmStop = false
                    Thread {
                        try {
                            Api.get("/$script/stop")
                            status = "已发送停止"
                        } catch (e: Exception) {
                            status = "停止失败：${e.message?.take(60)}"
                        }
                    }.start()
                }) { Text("确认停止") }
            },
            dismissButton = {
                androidx.compose.material3.TextButton(onClick = { confirmStop = false }) {
                    Text("取消")
                }
            },
        )
    }
}
