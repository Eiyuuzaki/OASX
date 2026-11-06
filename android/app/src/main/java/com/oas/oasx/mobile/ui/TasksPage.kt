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
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.I18n
import com.oas.oasx.mobile.data.SchedulerSocket
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasCard
import com.oas.oasx.mobile.ui.components.OasPageTitle
import com.oas.oasx.mobile.ui.components.OasSectionTitle
import com.oas.oasx.mobile.ui.components.OasStateBlock
import com.oas.oasx.mobile.ui.theme.Spacing

/// 任务页：任务分组 + 启停（停止有确认）+ 运行高亮 + 参数编辑入口。
@Composable
fun TasksPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    val script = account?.config
    val i18nMap by I18n.map.collectAsState()
    val t: (String) -> String = { key -> i18nMap[key] ?: key }

    var menu by remember(script) { mutableStateOf<Map<String, List<String>>?>(null) }
    var menuLoading by remember(script) { mutableStateOf(false) }
    var status by remember(script) { mutableStateOf("") }
    var confirmStop by remember(script) { mutableStateOf(false) }
    var argsTask by remember(script) { mutableStateOf<String?>(null) }

    var runningTask by remember(script) { mutableStateOf<String?>(null) }

    LaunchedEffect(Unit) { I18n.ensureLoaded() }

    LaunchedEffect(script, backend) {
        if (script == null) return@LaunchedEffect
        SchedulerSocket().connect(script, backend).collect { snap ->
            runningTask = snap.schedule?.running?.name
        }
    }

    LaunchedEffect(script) {
        if (script == null) return@LaunchedEffect
        menuLoading = true
        try {
            menu = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.getJsonOrThrow<Map<String, List<String>>>("/script_menu")
            }
        } catch (e: Exception) {
            status = "任务菜单加载失败：${e.message?.take(60)}"
        }
        menuLoading = false
    }

    // 参数编辑子页
    if (argsTask != null && script != null) {
        ArgsEditor(
            script = script,
            task = argsTask ?: "",
            onBack = { argsTask = null },
        )
        return
    }

    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(Spacing.md),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm),
    ) {
        item {
            OasPageTitle("任务")
            OasCaption("调度器 ${script ?: "—"} · 点任务名编辑参数")
        }

        item {
            Row(horizontalArrangement = Arrangement.spacedBy(Spacing.sm)) {
                Button(
                    onClick = {
                        if (script == null) return@Button
                        Thread {
                            try {
                                Api.get("/$script/start")
                                android.os.Handler(android.os.Looper.getMainLooper()).post {
                                    status = "已发送启动"
                                }
                            } catch (e: Exception) {
                                android.os.Handler(android.os.Looper.getMainLooper()).post {
                                    status = "启动失败：${e.message?.take(60)}"
                                }
                            }
                        }.start()
                    },
                    modifier = Modifier.weight(1f),
                    enabled = script != null,
                ) { Text("启动") }
                OutlinedButton(
                    onClick = { confirmStop = true },
                    modifier = Modifier.weight(1f),
                    enabled = script != null,
                ) { Text("停止") }
            }
            if (status.isNotEmpty()) {
                OasCaption(status, Modifier.padding(top = Spacing.xs))
            }
        }

        when {
            script == null -> item {
                OasStateBlock(
                    loading = false,
                    error = null,
                    emptyText = "先在首页选择账号",
                )
            }
            menuLoading -> item { OasStateBlock(loading = true, error = null) }
            menu == null -> item {
                OasStateBlock(loading = false, error = null, emptyText = "任务菜单加载失败")
            }
            else -> {
                (menu ?: emptyMap()).forEach { (group, tasks) ->
                    item {
                        OasSectionTitle(t(group), Modifier.padding(top = Spacing.sm))
                    }
                    items(tasks) { task ->
                        val isRunning = runningTask == task
                        OasCard(
                            modifier = Modifier.clickable { argsTask = task },
                        ) {
                            Row(
                                Modifier.fillMaxWidth(),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                OasBody(
                                    t(task),
                                    Modifier.weight(1f),
                                    color = if (isRunning) MaterialTheme.colorScheme.primary
                                            else MaterialTheme.colorScheme.onSurface,
                                )
                                if (isRunning) {
                                    Text(
                                        "● 运行中",
                                        style = MaterialTheme.typography.labelSmall,
                                        color = MaterialTheme.colorScheme.primary,
                                    )
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    if (confirmStop && script != null) {
        AlertDialog(
            onDismissRequest = { confirmStop = false },
            title = { Text("停止脚本") },
            text = { Text("确认停止 $script 的脚本执行？") },
            confirmButton = {
                TextButton(onClick = {
                    confirmStop = false
                    Thread {
                        try {
                            Api.get("/$script/stop")
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "已发送停止"
                            }
                        } catch (e: Exception) {
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "停止失败：${e.message?.take(60)}"
                            }
                        }
                    }.start()
                }) { Text("确认停止") }
            },
            dismissButton = {
                TextButton(onClick = { confirmStop = false }) { Text("取消") }
            },
        )
    }
}