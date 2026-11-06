package com.oas.oasx.mobile.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.LogStream

/// 实时日志：SSE 订阅当前账号调度器的日志流（自动滚动、保留最近 400 行）。
@Composable
fun LogsPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    val script = account?.config

    val lines = remember(script) { mutableStateListOf<String>() }
    var status by remember(script) {
        mutableStateOf(if (script == null) "未选择账号" else "连接中…")
    }
    val listState = rememberLazyListState()

    LaunchedEffect(script, backend) {
        if (script == null) {
            status = "未选择账号"
            return@LaunchedEffect
        }
        lines.clear()
        LogStream().connect(script, backend).collect { event ->
            when (event) {
                is LogStream.Event.Ready -> status = "已连接"
                is LogStream.Event.Lines -> {
                    lines.addAll(event.lines)
                    // 只保留最近 400 行，避免无限增长
                    if (lines.size > 400) {
                        lines.subList(0, lines.size - 400).clear()
                    }
                }
                is LogStream.Event.Closed -> status = "脚本已停止"
            }
        }
    }

    // 新日志到达自动滚到底
    LaunchedEffect(lines.size) {
        if (lines.isNotEmpty()) listState.animateScrollToItem(lines.size - 1)
    }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Text("日志", style = MaterialTheme.typography.headlineSmall)
        Text(
            "$script · $status · ${lines.size} 行",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 2.dp, bottom = 8.dp),
        )
        Card(Modifier.fillMaxSize()) {
            if (lines.isEmpty()) {
                Column(Modifier.fillMaxWidth().padding(24.dp)) {
                    Text(
                        if (script == null) "先在首页选择账号"
                        else "等待日志…（脚本运行时实时滚动）",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            } else {
                LazyColumn(state = listState, modifier = Modifier.fillMaxSize()) {
                    items(lines) { line ->
                        Text(
                            line,
                            style = MaterialTheme.typography.labelSmall,
                            fontFamily = FontFamily.Monospace,
                            color = MaterialTheme.colorScheme.onSurface,
                            modifier = Modifier.fillMaxWidth().padding(horizontal = 10.dp),
                        )
                    }
                }
            }
        }
    }
}