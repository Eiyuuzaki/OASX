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
import androidx.compose.material3.Card
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
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
import com.oas.oasx.mobile.data.I18n
import com.oas.oasx.mobile.data.SchedulerSocket
import com.oas.oasx.mobile.net.ActivityBoard
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.FosterResp

/// 首页：账号头（可切换）+ 调度器实时状态 + 每日/活动子页签 + 结界寄养。
@Composable
fun HomePage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    val i18nMap by I18n.map.collectAsState()
    val t: (String) -> String = { key -> i18nMap[key] ?: key }

    var showSwitcher by remember { mutableStateOf(false) }
    var subTab by remember { mutableIntStateOf(0) }

    // 中文名映射（一次性）
    LaunchedEffect(backend) { I18n.ensureLoaded() }

    // 调度器实时状态（WS）
    var wsState by remember(account?.config) { mutableStateOf("连接中…") }
    var running by remember(account?.config) { mutableStateOf<String?>(null) }
    var queueCount by remember(account?.config) { mutableStateOf(0) }
    LaunchedEffect(account?.config, backend) {
        val script = account?.config ?: return@LaunchedEffect
        SchedulerSocket().connect(script, backend).collect { snap ->
            snap.state?.let { wsState = it }
            snap.schedule?.let {
                running = it.running?.name
                queueCount = it.pending.size + it.waiting.size
            }
        }
    }

    // 每日任务 + 寄养
    val acctKey = account?.let { "${it.config}|${it.roleKey}" }
    var board by remember(acctKey) { mutableStateOf<ActivityBoard?>(null) }
    var foster by remember(acctKey) { mutableStateOf<FosterResp?>(null) }
    var fosterError by remember(acctKey) { mutableStateOf<String?>(null) }
    var dataError by remember(acctKey) { mutableStateOf<String?>(null) }
    LaunchedEffect(acctKey, backend) {
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
            foster = null
            fosterError = e.message
        }
    }

    LazyColumn(
        Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        // 标题 + 账号
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("首页", fontSize = 22.sp, modifier = Modifier.weight(1f))
                if (acct.accounts.isNotEmpty()) {
                    Text(
                        "切换账号",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.clickable { showSwitcher = true },
                    )
                }
            }
        }

        item {
            Card(
                Modifier.fillMaxWidth().clickable { showSwitcher = true },
            ) {
                Column(Modifier.padding(16.dp)) {
                    Text(
                        account?.name ?: "未选择账号",
                        fontSize = 18.sp,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    Text(
                        account?.let { "Lv${it.level} · ${it.serverName} · 调度器 ${it.config}" }
                            ?: "点击选择账号（需先在各调度器下登录游戏）",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    // 活动数据的真实归属者与所选账号不一致时提示（同 WPF live_role 校验）
                    val live = board?.liveRole
                    if (live != null && account != null &&
                        live.roleKey.isNotBlank() &&
                        live.roleKey != account.roleKey
                    ) {
                        Text(
                            "⚠ 当前游戏内登录：${live.name}（与所选账号不一致）",
                            fontSize = 11.sp,
                            color = MaterialTheme.colorScheme.error,
                            modifier = Modifier.padding(top = 6.dp),
                        )
                    }
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
                        Text("正在执行：${t(it)}", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    if (queueCount > 0) {
                        Text("队列 $queueCount 项", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    board?.serverTimeText?.takeIf { it.isNotBlank() }?.let {
                        Text("服务器时间 $it", fontSize = 11.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(top = 4.dp))
                    }
                }
            }
        }

        // 子页签
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                FilterChip(
                    selected = subTab == 0,
                    onClick = { subTab = 0 },
                    label = { Text("每日") },
                )
                androidx.compose.foundation.layout.Spacer(Modifier.padding(4.dp))
                FilterChip(
                    selected = subTab == 1,
                    onClick = { subTab = 1 },
                    label = { Text("活动") },
                )
            }
        }

        val b = board
        when {
            dataError != null -> item {
                Text("数据错误：$dataError", fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.error)
            }
            b == null -> item {
                Text("读取中…", fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            !b.success -> item {
                Text(b.error ?: "读取失败", fontSize = 13.sp,
                    color = MaterialTheme.colorScheme.error)
                b.hint?.let {
                    Text(it, fontSize = 11.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            subTab == 0 -> {
                // ── 每日任务 ──
                item {
                    Card(Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(16.dp)) {
                            Text("每日任务", fontSize = 13.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Text("${b.dailyDone} / ${b.dailyTotal}", fontSize = 30.sp)
                        }
                    }
                }
                items(b.daily) { d ->
                    Row(Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                        Text(
                            if (d.done) "✓" else "○",
                            fontSize = 13.sp,
                            color = if (d.done) MaterialTheme.colorScheme.primary
                                    else MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        Text(
                            t(d.title.ifBlank { d.name }),
                            Modifier.weight(1f).padding(start = 8.dp),
                            fontSize = 13.sp,
                            color = if (d.done) MaterialTheme.colorScheme.onSurfaceVariant
                                    else MaterialTheme.colorScheme.onSurface,
                        )
                        Text(d.progressText, fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
                // ── 悬赏封印 ──
                item {
                    Card(Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(16.dp)) {
                            Text("悬赏封印", fontSize = 13.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Text("${b.monsterDone} / ${b.monster.size}", fontSize = 24.sp)
                        }
                    }
                }
                items(b.monster.filter { !it.fin }.take(8)) { m ->
                    Row(Modifier.fillMaxWidth().padding(vertical = 3.dp)) {
                        Text("·", fontSize = 13.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Text(
                            m.id,
                            Modifier.weight(1f).padding(start = 6.dp),
                            fontSize = 12.sp,
                        )
                        Text("${m.process}/${m.need}", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
            else -> {
                // ── 活动 ──
                item {
                    Text("当前参与活动", fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.primary)
                }
                if (b.activities.isEmpty()) {
                    item {
                        Text("无（或游戏内未打开活动页）", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                } else {
                    items(b.activities) { a ->
                        Card(Modifier.fillMaxWidth().padding(vertical = 3.dp)) {
                            Column(Modifier.padding(12.dp)) {
                                Text(t(a.name), fontSize = 14.sp)
                                Text(
                                    "${a.begin} → ${a.end}",
                                    fontSize = 11.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                    }
                }
                item {
                    Text("活动面板任务", fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.padding(top = 10.dp))
                }
                if (b.activity.isEmpty()) {
                    item {
                        Text("无（需在游戏内打开过活动页）", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                } else {
                    items(b.activity) { name ->
                        Text("· ${t(name)}", fontSize = 13.sp,
                            modifier = Modifier.padding(vertical = 2.dp))
                    }
                }
                item {
                    Text("进行中时段", fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.padding(top = 10.dp))
                }
                items(b.live) { w ->
                    Row(Modifier.fillMaxWidth().padding(vertical = 3.dp)) {
                        Text(t(w.key), Modifier.weight(1f), fontSize = 13.sp)
                        Text("至 ${w.endText}", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }

        // ── 结界寄养 ──
        item {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp)) {
                    Text("结界寄养", fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                    val fo = foster
                    val f = fo?.foster
                    when {
                        fo == null -> Text(
                            fosterError ?: "读取中…", fontSize = 13.sp,
                            color = if (fosterError != null) MaterialTheme.colorScheme.error
                                    else MaterialTheme.colorScheme.onSurfaceVariant)
                        !fo.success -> Text(
                            fo.error ?: "读取失败", fontSize = 13.sp,
                            color = MaterialTheme.colorScheme.error)
                        f == null || f.active == false ->
                            Text("当前未寄养", fontSize = 18.sp)
                        (f.remaining ?: 0) <= 0 ->
                            Text("已成熟，去收取", fontSize = 18.sp,
                                color = MaterialTheme.colorScheme.primary)
                        else -> {
                            val r = (f.remaining ?: 0) / 60
                            Text("${r / 60}小时${r % 60}分后成熟", fontSize = 18.sp,
                                color = MaterialTheme.colorScheme.primary)
                            Text("${f.cardName ?: f.cardItem} · ${f.endText} 收取",
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                    val o = fo?.own
                    when {
                        o == null || o.unknown == true -> {}
                        o.empty == true -> Text("自结界卡：空", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                        (o.remaining ?: 0) <= 0 -> Text("自结界卡：已成熟", fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.primary)
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
    }

    if (showSwitcher) {
        AccountSwitcherDialog(
            accounts = acct.accounts,
            current = account,
            loading = acct.loading,
            error = acct.error,
            onSelect = { acc ->
                AccountContext.select(acc)
                showSwitcher = false
            },
            onRefresh = {
                showSwitcher = false
                AccountContext.refreshInBackground()
            },
            onDismiss = { showSwitcher = false },
        )
    }
}