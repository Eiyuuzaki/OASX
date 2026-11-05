package com.oas.oasx.mobile.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.SchedulerSocket
import com.oas.oasx.mobile.net.ActivityBoard
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.FosterResp


/// 首页：账号头 + 调度器实时状态（WS）+ 寄养 + 每日任务。
@Composable
fun HomePage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current

    // 调度器实时状态（WS）
    var wsState by remember(account?.config) { mutableStateOf("连接中…") }
    var running by remember(account?.config) { mutableStateOf<String?>(null) }
    var queueCount by remember(account?.config) { mutableStateOf(0) }
    androidx.compose.runtime.LaunchedEffect(account?.config, backend) {
        val script = account?.config ?: return@LaunchedEffect
        SchedulerSocket().connect(script, backend).collect { snap ->
            snap.state?.let { wsState = it }
            snap.schedule?.let {
                running = it.running?.name
                queueCount = it.pending.size + it.waiting.size
            }
        }
    }

    // 每日任务 + 寄养（轮询由下拉/重进页面触发）
    var board by remember(account?.config) { mutableStateOf<ActivityBoard?>(null) }
    var foster by remember(account?.config) { mutableStateOf<FosterResp?>(null) }
    var fosterError by remember(account?.config) { mutableStateOf<String?>(null) }
    var dataError by remember(account?.config) { mutableStateOf<String?>(null) }
    androidx.compose.runtime.LaunchedEffect(account?.config, backend) {
        val config = account?.config ?: return@LaunchedEffect
        dataError = null
        try {
            board = Api.getJsonOrThrow<ActivityBoard>("/activity/$config?force=1")
            dataError = null
        } catch (e: Exception) {
            board = null
            dataError = e.message
        }
        try {
            foster = Api.postJsonOrThrow<FosterResp>("/notify/foster/refresh")
            fosterError = null
        } catch (e: Exception) {
            // 失败即清空旧值——规范：失败时不得展示"看似实时"的旧数据
            foster = null
            fosterError = e.message
        }
    }

    LazyColumn(
        Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = androidx.compose.foundation.layout.Arrangement.spacedBy(12.dp),
    ) {
        item { PageTitle("首页") }

        // 账号头
        item {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp)) {
                    Text(account?.name ?: "未选择账号", fontSize = 18.sp,
                        color = MaterialTheme.colorScheme.onSurface)
                    Text(
                        account?.let { "Lv${it.level} · ${it.serverName} · 调度器 ${it.config}" }
                            ?: "在下方账号列表中选择",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }

        // 调度器实时状态
        item {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp)) {
                    Text("调度器", fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(wsState, fontSize = 18.sp,
                        color = MaterialTheme.colorScheme.primary)
                    running?.let {
                        Text("正在执行：$it", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    if (queueCount > 0) {
                        Text("队列 $queueCount 项", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }

        // 每日任务
        item {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp)) {
                    Text("每日任务", fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                    val b = board
                    if (b != null && b.success) {
                        Text("${b.dailyDone} / ${b.dailyTotal}", fontSize = 30.sp)
                        Text("悬赏封印  ${b.monsterDone} / ${b.monster.size}",
                            fontSize = 13.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    } else if (b != null) {
                        Text(b.error ?: "读取失败", fontSize = 13.sp,
                            color = MaterialTheme.colorScheme.error)
                    }
                }
            }
        }

        // 寄养
        item {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp)) {
                    Text("结界寄养", fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                    val fo = foster
                    val f = fo?.foster
                    when {
                        fo == null -> Text(
                            fosterError ?: "读取失败", fontSize = 13.sp,
                            color = MaterialTheme.colorScheme.error)
                        !fo.success -> Text(
                            fo.error ?: "读取失败", fontSize = 13.sp,
                            color = MaterialTheme.colorScheme.error)
                        f == null || f.active == false ->
                            Text("当前未寄养", fontSize = 18.sp)
                        (f.remaining ?: 0) <= 0 ->
                            Text("已成熟，去收取", fontSize = 18.sp,
                                color = OasSuccessColor())
                        else -> {
                            val r = (f.remaining ?: 0) / 60
                            Text("${r / 60}小时${r % 60}分后成熟", fontSize = 18.sp,
                                color = OasSuccessColor())
                            Text("${f.cardName ?: f.cardItem} · ${f.endText} 收取",
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                    val o = foster?.own
                    when {
                        o == null || o.unknown == true -> {}
                        o.empty == true -> Text("自结界卡：空", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                        (o.remaining ?: 0) <= 0 -> Text("自结界卡：已成熟", fontSize = 12.sp,
                            color = OasSuccessColor())
                        else -> {
                            val r = (o.remaining ?: 0) / 60
                            Text("自结界卡：${o.cardName ?: o.cardItem} ${r / 60}小时${r % 60}分",
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
            }
        }

        // 连接错误（数据层）
        dataError?.let { err ->
            item { Text("数据错误：$err", fontSize = 12.sp,
                color = MaterialTheme.colorScheme.error) }
        }
    }
}

@Composable
private fun OasSuccessColor() = MaterialTheme.colorScheme.primary