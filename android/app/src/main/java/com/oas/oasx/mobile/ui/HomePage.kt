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
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.I18n
import com.oas.oasx.mobile.data.SchedulerSocket
import com.oas.oasx.mobile.net.ActivityBoard
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.FosterResp
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasCard
import com.oas.oasx.mobile.ui.components.OasCardLevel
import com.oas.oasx.mobile.ui.components.OasError
import com.oas.oasx.mobile.ui.components.OasLabel
import com.oas.oasx.mobile.ui.components.OasMetric
import com.oas.oasx.mobile.ui.components.OasPageTitle
import com.oas.oasx.mobile.ui.components.OasSectionTitle
import com.oas.oasx.mobile.ui.components.OasStateBlock
import com.oas.oasx.mobile.ui.theme.OasColors
import com.oas.oasx.mobile.ui.theme.Spacing

/// 首页：账号 + 调度器实时状态 + 每日/活动 + 结界寄养。
/// 全部使用 M3 语义组件（OasCard/OasPageTitle/…），不再手写字号与色值。
@Composable
fun HomePage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val i18nMap by I18n.map.collectAsState()
    val t: (String) -> String = { key -> i18nMap[key] ?: key }

    var showSwitcher by remember { mutableStateOf(false) }
    var subTab by remember { mutableIntStateOf(0) }
    val account = acct.current
    val acctKey = account?.key

    LaunchedEffect(backend) { I18n.ensureLoaded() }

    // 调度器实时状态
    var wsState by remember(acctKey) { mutableStateOf("") }
    var running by remember(acctKey) { mutableStateOf<String?>(null) }
    var queueCount by remember(acctKey) { mutableStateOf(0) }
    LaunchedEffect(acctKey, backend) {
        val script = account?.config ?: run {
            wsState = ""
            return@LaunchedEffect
        }
        wsState = "连接中…"
        SchedulerSocket().connect(script, backend).collect { snap ->
            snap.state?.let { wsState = it }
            snap.schedule?.let {
                running = it.running?.name
                queueCount = it.pending.size + it.waiting.size
            }
        }
    }

    // 每日任务 + 寄养
    var board by remember(acctKey) { mutableStateOf<ActivityBoard?>(null) }
    var foster by remember(acctKey) { mutableStateOf<FosterResp?>(null) }
    var fosterError by remember(acctKey) { mutableStateOf<String?>(null) }
    var dataError by remember(acctKey) { mutableStateOf<String?>(null) }
    var dataLoading by remember(acctKey) { mutableStateOf(false) }
    LaunchedEffect(acctKey, backend) {
        val config = account?.config ?: return@LaunchedEffect
        dataLoading = true
        dataError = null
        board = null
        foster = null
        try {
            board = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.getJsonOrThrow<ActivityBoard>("/activity/$config?force=1")
            }
        } catch (e: Exception) {
            dataError = e.message
        }
        try {
            foster = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.postJsonOrThrow<FosterResp>("/notify/foster/refresh")
            }
            fosterError = null
        } catch (e: Exception) {
            foster = null
            fosterError = e.message
        }
        dataLoading = false
    }

    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(Spacing.md),
        verticalArrangement = Arrangement.spacedBy(Spacing.md),
    ) {
        // 页头
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                OasPageTitle("首页", Modifier.weight(1f))
                if (acct.accounts.isNotEmpty()) {
                    TextButton(onClick = { showSwitcher = true }) {
                        Text("切换账号", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }
        }

        // 账号状态
        when {
            acct.loading && acct.accounts.isEmpty() -> item {
                OasStateBlock(loading = true, error = null)
            }
            acct.error != null && acct.accounts.isEmpty() -> item {
                OasStateBlock(
                    loading = false,
                    error = acct.error,
                    onRetry = { AccountContext.refreshInBackground() },
                )
            }
            acct.loaded && acct.accounts.isEmpty() -> item {
                OasCard {
                    OasSectionTitle("没有可用账号")
                    OasCaption("需要先在某个调度器下登录游戏并产生一次存档")
                    TextButton(onClick = { AccountContext.refreshInBackground() }) {
                        Text("重新加载", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }
            else -> item {
                OasCard(
                    level = OasCardLevel.Emphasis,
                    modifier = Modifier.clickable { showSwitcher = true },
                ) {
                    OasBody(account?.name ?: "未选择账号")
                    OasCaption(
                        account?.let {
                            "Lv${it.level} · ${it.serverName} · 调度器 ${it.config}"
                        } ?: "点击选择账号",
                    )
                    val live = board?.liveRole
                    if (live != null && account != null &&
                        live.roleKey.isNotBlank() && live.roleKey != account.roleKey
                    ) {
                        OasError("⚠ 游戏内当前登录：${live.name}（与所选账号不一致）")
                    }
                }
            }
        }

        // 调度器状态
        if (account != null) {
            item {
                OasCard {
                    OasLabel("调度器")
                    OasMetric(wsState.ifBlank { "未连接" })
                    running?.let { OasCaption("正在执行：${t(it)}") }
                    if (queueCount > 0) OasCaption("队列 $queueCount 项")
                    board?.serverTimeText?.takeIf { it.isNotBlank() }?.let {
                        OasCaption("服务器时间 $it")
                    }
                }
            }

            // 子页签
            item {
                Row(horizontalArrangement = Arrangement.spacedBy(Spacing.sm)) {
                    FilterChip(
                        selected = subTab == 0,
                        onClick = { subTab = 0 },
                        label = { Text("每日") },
                    )
                    FilterChip(
                        selected = subTab == 1,
                        onClick = { subTab = 1 },
                        label = { Text("活动") },
                    )
                }
            }

            val b = board
            when {
                dataLoading && b == null -> item {
                    OasStateBlock(loading = true, error = null)
                }
                dataError != null -> item {
                    OasStateBlock(loading = false, error = dataError)
                }
                b == null -> item {
                    OasStateBlock(loading = false, error = null, emptyText = "暂无数据")
                }
                !b.success -> item {
                    OasCard {
                        OasError(b.error ?: "读取失败")
                        b.hint?.let { OasCaption(it) }
                    }
                }
                subTab == 0 -> {
                    item {
                        OasCard(level = OasCardLevel.Emphasis) {
                            OasLabel("每日任务")
                            OasMetric("${b.dailyDone} / ${b.dailyTotal}")
                        }
                    }
                    items(b.daily) { d ->
                        Row(
                            Modifier.fillMaxWidth().padding(vertical = Spacing.xs),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(
                                if (d.done) "✓" else "○",
                                style = MaterialTheme.typography.bodyMedium,
                                color = if (d.done) MaterialTheme.colorScheme.primary
                                        else MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Text(
                                t(d.title.ifBlank { d.name }),
                                style = MaterialTheme.typography.bodyMedium,
                                color = if (d.done) MaterialTheme.colorScheme.onSurfaceVariant
                                        else MaterialTheme.colorScheme.onSurface,
                                modifier = Modifier.weight(1f).padding(start = Spacing.sm),
                            )
                            OasCaption(d.progressText)
                        }
                    }
                    item {
                        OasCard {
                            OasLabel("悬赏封印")
                            OasMetric("${b.monsterDone} / ${b.monster.size}")
                        }
                    }
                    items(b.monster.filter { !it.fin }.take(8)) { m ->
                        Row(Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
                            OasCaption("·")
                            OasBody(
                                m.id,
                                Modifier.weight(1f).padding(start = Spacing.xs),
                            )
                            OasCaption("${m.process}/${m.need}")
                        }
                    }
                }
                else -> {
                    item { OasSectionTitle("当前参与活动") }
                    if (b.activities.isEmpty()) {
                        item { OasCaption("无（或游戏内未打开活动页）") }
                    } else {
                        items(b.activities) { a ->
                            OasCard {
                                OasBody(t(a.name))
                                OasCaption("${a.begin} → ${a.end}")
                            }
                        }
                    }
                    item { OasSectionTitle("活动面板任务") }
                    if (b.activity.isEmpty()) {
                        item { OasCaption("无（需在游戏内打开过活动页）") }
                    } else {
                        items(b.activity) { name ->
                            OasBody("· ${t(name)}", Modifier.padding(vertical = 2.dp))
                        }
                    }
                    item { OasSectionTitle("进行中时段") }
                    items(b.live) { w ->
                        Row(Modifier.fillMaxWidth().padding(vertical = Spacing.xs)) {
                            OasBody(t(w.key), Modifier.weight(1f))
                            OasCaption("至 ${w.endText}")
                        }
                    }
                }
            }

            // 结界寄养
            item {
                OasCard {
                    OasLabel("结界寄养")
                    val fo = foster
                    val f = fo?.foster
                    when {
                        fo == null && fosterError != null -> OasError(fosterError ?: "")
                        fo == null -> OasCaption("读取中…")
                        !fo.success -> OasError(fo.error ?: "读取失败")
                        f == null || f.active == false -> OasBody("当前未寄养")
                        (f.remaining ?: 0) <= 0 ->
                            OasMetric("已成熟，去收取")
                        else -> {
                            val r = (f.remaining ?: 0) / 60
                            OasMetric("${r / 60}小时${r % 60}分后成熟")
                            OasCaption("${f.cardName ?: f.cardItem} · ${f.endText} 收取")
                        }
                    }
                    val o = fo?.own
                    when {
                        o == null || o.unknown == true -> {}
                        o.empty == true -> OasCaption("自结界卡：空")
                        (o.remaining ?: 0) <= 0 -> OasCaption("自结界卡：已成熟")
                        else -> {
                            val r = (o.remaining ?: 0) / 60
                            OasCaption(
                                "自结界卡：${o.cardName ?: o.cardItem} ${r / 60}小时${r % 60}分",
                            )
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