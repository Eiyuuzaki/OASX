package com.oas.oasx.mobile.ui

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
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.net.Api

/// 任务页：任务分组（/script_menu）+ 脚本启停。
/// 任务参数编辑与启用开关需要 /{script}/{task}/args 与 PUT value——后续版本。
@Composable
fun TasksPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    var menu by remember { mutableStateOf<Map<String, List<String>>?>(null) }
    var status by remember { mutableStateOf("") }
    var running by remember { mutableStateOf<Boolean?>(null) }
    var confirmStop by remember { mutableStateOf(false) }

    LaunchedEffect(account?.config) {
        val script = account?.config ?: return@LaunchedEffect
        try {
            menu = Api.getJsonOrThrow<Map<String, List<String>>>("/script_menu")
        } catch (e: Exception) {
            status = "任务菜单加载失败：${e.message?.take(60)}"
        }
    }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Text("任务", fontSize = 22.sp)
        Text(
            "调度器 ${account?.config ?: "—"} · 任务参数编辑后续版本支持",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(bottom = 12.dp),
        )

        Row(Modifier.fillMaxWidth().padding(bottom = 12.dp)) {
            Button(
                onClick = {
                    val script = account?.config ?: return@Button
                    Thread {
                        try {
                            Api.get("/$script/start")
                            status = "已发送启动"
                            running = true
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
            if (confirmStop) {
                androidx.compose.material3.AlertDialog(
                    onDismissRequest = { confirmStop = false },
                    title = { Text("停止脚本") },
                    text = { Text("确认停止 ${account?.config} 的脚本执行？") },
                    confirmButton = {
                        androidx.compose.material3.TextButton(onClick = {
                            confirmStop = false
                            val script = account?.config ?: return@TextButton
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
                    Text(group, fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.padding(top = 10.dp, bottom = 4.dp))
                }
                items(tasks) { task ->
                    Card(Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
                        Text(task, fontSize = 13.sp,
                            modifier = Modifier.padding(12.dp, 8.dp))
                    }
                }
            }
        }
    }
}