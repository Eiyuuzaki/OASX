package com.oas.oasx.mobile.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Card
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.data.StatsResponse

/// 记录页：子页签「统计」/「实时日志」（规范五入口下聚合运行记录类信息）。
@Composable
fun RecordsPage(backend: String) {
    var subTab by remember { mutableIntStateOf(0) }
    Column(Modifier.fillMaxSize()) {
        Row(
            Modifier.fillMaxWidth().padding(16.dp, 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text("记录", fontSize = 22.sp, modifier = Modifier.weight(1f))
            FilterChip(
                selected = subTab == 0,
                onClick = { subTab = 0 },
                label = { Text("统计") },
            )
            androidx.compose.foundation.layout.Spacer(Modifier.padding(4.dp))
            FilterChip(
                selected = subTab == 1,
                onClick = { subTab = 1 },
                label = { Text("实时日志") },
            )
            androidx.compose.foundation.layout.Spacer(Modifier.padding(4.dp))
            FilterChip(
                selected = subTab == 2,
                onClick = { subTab = 2 },
                label = { Text("错误") },
            )
            androidx.compose.foundation.layout.Spacer(Modifier.padding(4.dp))
            FilterChip(
                selected = subTab == 3,
                onClick = { subTab = 3 },
                label = { Text("绘卷") },
            )
        }
        when (subTab) {
            1 -> LogsPage(backend)
            2 -> ErrorsPage(onBack = { subTab = 0 })
            3 -> HuijuanPage(onBack = { subTab = 0 })
            else -> StatsView(backend)
        }
    }
}

/// 统计视图：日期切换 + 总览磁贴 + 各任务 runs。
@Composable
fun StatsView(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    var dates by remember(account?.config) { mutableStateOf<List<String>>(emptyList()) }
    var selected by remember(account?.config) { mutableStateOf<String?>(null) }
    var stats by remember(account?.config) { mutableStateOf<StatsResponse?>(null) }
    var error by remember(account?.config) { mutableStateOf<String?>(null) }

    LaunchedEffect(account?.config, backend) {
        val script = account?.config ?: return@LaunchedEffect
        try {
            val resp = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.getJsonOrThrow<com.oas.oasx.mobile.data.StatsDatesResponse>(
                    "/stats/$script/dates")
            }
            dates = resp.dates
            selected = resp.dates.firstOrNull()
        } catch (e: Exception) {
            error = e.message
        }
    }

    LaunchedEffect(account?.config, selected) {
        val script = account?.config ?: return@LaunchedEffect
        val date = selected ?: return@LaunchedEffect
        try {
            stats = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.getJsonOrThrow<StatsResponse>("/stats/$script?date=$date")
            }
            error = null
        } catch (e: Exception) {
            error = e.message
        }
    }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        if (dates.isNotEmpty()) {
            Text(
                "日期：${selected ?: "—"}（共 ${dates.size} 天）",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            androidx.compose.material3.Button(onClick = {
                val idx = dates.indexOf(selected)
                if (idx >= 0 && idx < dates.size - 1) selected = dates[idx + 1]
                else selected = dates.firstOrNull()
            }, modifier = Modifier.padding(top = 4.dp, bottom = 8.dp)) { Text("切换日期") }
        }

        error?.let {
            Text(it, fontSize = 12.sp, color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(top = 8.dp))
        }

        val s = stats
        if (s != null) {
            Row(Modifier.fillMaxWidth()) {
                StatTile("总时长", formatDuration(s.totalRuntimeSeconds), Modifier.weight(1f))
                StatTile("任务次数", "${s.totalTaskRunCount}", Modifier.weight(1f))
                StatTile("战斗场次", "${s.totalBattleCount}", Modifier.weight(1f))
            }
            Text("各任务", fontSize = 14.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 14.dp, bottom = 4.dp))
            LazyColumn {
                items(s.tasks.entries.toList()) { (name, t) ->
                    Card(Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                        Column(Modifier.padding(12.dp)) {
                            Text(name, fontSize = 14.sp)
                            Text(
                                "执行 ${t.runCount} 次 · 共 " +
                                    formatDuration(t.totalDurationSeconds),
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            t.runs.takeLast(3).reversed().forEach { run ->
                                Text(
                                    "  ${run.startTime.takeLast(8)} · " +
                                        formatDuration(run.durationSeconds),
                                    fontSize = 11.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                    }
                }
            }
        } else if (error == null) {
            Text("加载中…", fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp))
        }
    }
}

@Composable
fun StatTile(label: String, value: String, modifier: Modifier = Modifier) {
    Card(modifier.fillMaxWidth()) {
        Column(Modifier.padding(10.dp)) {
            Text(label, fontSize = 11.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(value, fontSize = 17.sp)
        }
    }
}

fun formatDuration(seconds: Double): String {
    val total = seconds.toInt()
    val h = total / 3600
    val m = (total % 3600) / 60
    return when {
        h > 0 -> "${h}小时${m}分"
        m > 0 -> "${m}分${total % 60}秒"
        else -> "${total}秒"
    }
}