package com.oas.oasx.mobile.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Card
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
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
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.HuijuanApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/// 绘卷：区服棋盘进度（每个卷的达成度 + 个人贡献）。
///
/// 非活动期（所有卷 end_text 已过）显示明确提示，不展示上期遗留分数。
@Composable
fun HuijuanPage(onBack: () -> Unit) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    var board by remember(account?.config) { mutableStateOf<HuijuanApi.Board?>(null) }
    var error by remember(account?.config) { mutableStateOf<String?>(null) }
    var loading by remember(account?.config) { mutableStateOf(false) }
    var reload by remember { mutableStateOf(0) }

    LaunchedEffect(account?.config, reload) {
        val acc = account ?: return@LaunchedEffect
        loading = true
        error = null
        try {
            board = withContext(Dispatchers.IO) { HuijuanApi.board(acc.config) }
        } catch (e: Exception) {
            error = e.message?.take(120)
        }
        loading = false
    }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                "← 返回",
                Modifier.padding(end = 12.dp),
                fontSize = 14.sp,
                color = MaterialTheme.colorScheme.primary,
            )
            Text("绘卷", fontSize = 22.sp, modifier = Modifier.weight(1f))
            TextButton(onClick = { reload++ }) { Text("刷新", fontSize = 12.sp) }
        }
        Text(
            account?.let { "${it.name} · ${it.serverName}" } ?: "未选择账号",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        error?.let {
            Text(it, fontSize = 12.sp, color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(top = 8.dp))
        }

        val b = board
        when {
            loading && b == null -> Text("读取中…", fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 12.dp))
            b == null -> Text("暂无数据", fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 12.dp))
            !b.success -> Text(b.error ?: "读取失败", fontSize = 13.sp,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(top = 12.dp))
            !HuijuanApi.isActive(b) -> {
                // 非活动期：不展示上期遗留分数
                Text(
                    "当前不在绘卷活动期",
                    fontSize = 15.sp,
                    modifier = Modifier.padding(top = 16.dp),
                )
                Text(
                    "活动结束后不再显示上期分数。下一次活动开启后自动恢复。",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
            else -> {
                Text(
                    "第 ${b.issue} 期 · 目标 ${fmt(b.threshold)}",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 8.dp, bottom = 4.dp),
                )
                LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(b.scrolls) { sc ->
                        Card(Modifier.fillMaxWidth()) {
                            Column(Modifier.padding(12.dp)) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Text(
                                        sc.name.ifBlank { "卷 ${sc.id}" },
                                        Modifier.weight(1f),
                                        fontSize = 14.sp,
                                    )
                                    if (sc.finished) {
                                        Text("✓ 已达成", fontSize = 11.sp,
                                            color = MaterialTheme.colorScheme.primary)
                                    }
                                }
                                Text(
                                    "区服 ${fmt(sc.score)} · ${"%.2f".format(sc.percent)}%",
                                    fontSize = 11.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    modifier = Modifier.padding(top = 2.dp),
                                )
                                LinearProgressIndicator(
                                    progress = { (sc.percent / 100.0).toFloat().coerceIn(0f, 1f) },
                                    modifier = Modifier.fillMaxWidth().padding(top = 6.dp),
                                )
                                Row(Modifier.fillMaxWidth().padding(top = 6.dp)) {
                                    Text(
                                        "我的贡献 ${fmt(sc.selfScore)}",
                                        fontSize = 11.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                                        modifier = Modifier.weight(1f),
                                    )
                                    sc.selfRank?.let {
                                        Text("排名 $it", fontSize = 11.sp,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    }
                                }
                                Text(
                                    "${sc.beginText} → ${sc.endText}",
                                    fontSize = 10.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    modifier = Modifier.padding(top = 2.dp),
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}