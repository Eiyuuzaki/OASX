package com.oas.oasx.mobile.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountCircle
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Dns
import androidx.compose.material.icons.filled.HourglassEmpty
import androidx.compose.material.icons.filled.Inbox
import androidx.compose.material.icons.filled.PlayCircle
import androidx.compose.material.icons.filled.RadioButtonUnchecked
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
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
import com.oas.oasx.mobile.ui.components.OasCardTone
import com.oas.oasx.mobile.ui.components.OasError
import com.oas.oasx.mobile.ui.components.OasIconBadge
import com.oas.oasx.mobile.ui.components.OasPageHeader
import com.oas.oasx.mobile.ui.components.OasSectionTitle
import com.oas.oasx.mobile.ui.components.OasStatCard
import com.oas.oasx.mobile.ui.components.OasStateBlock
import com.oas.oasx.mobile.ui.components.captionColorFor
import com.oas.oasx.mobile.ui.components.contentColorFor
import com.oas.oasx.mobile.ui.theme.Spacing

/// 首页：账号 + 调度器 + 每日/活动 + 结界寄养。
/// 使用 M3 彩色容器（primary/secondary/tertiary）与图标徽章建立视觉层次。
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
        item {
            OasPageHeader(
                title = "首页",
                subtitle = "OAS 自动化运行总览",
                icon = Icons.Filled.AccountCircle,
                action = if (acct.accounts.isNotEmpty()) {
                    {
                        TextButton(onClick = { showSwitcher = true }) {
                            Text("切换", style = MaterialTheme.typography.labelLarge)
                        }
                    }
                } else null,
            )
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
                OasCard(tone = OasCardTone.Tertiary) {
                    OasSectionTitle("没有可用账号")
                    OasCaption("需要先在某个调度器下登录游戏并产生一次存档")
                    TextButton(onClick = { AccountContext.refreshInBackground() }) {
                        Text("重新加载", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }
            else -> item {
                OasCard(
                    tone = OasCardTone.Primary,
                    modifier = Modifier.clickable { showSwitcher = true },
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        OasIconBadge(
                            Icons.Filled.AccountCircle,
                            tone = OasCardTone.Neutral,
                            size = 44.dp,
                        )
                        Spacer(Modifier.size(Spacing.sm))
                        Column(Modifier.weight(1f)) {
                            Text(
                                account?.name ?: "未选择账号",
                                style = MaterialTheme.typography.titleMedium,
                                color = contentColorFor(OasCardTone.Primary),
                            )
                            Text(
                                account?.let {
                                    "Lv${it.level} · ${it.serverName} · ${it.config}"
                                } ?: "点击选择账号",
                                style = MaterialTheme.typography.bodySmall,
                                color = captionColorFor(OasCardTone.Primary),
                            )
                        }
                    }
                    val live = board?.liveRole
                    if (live != null && account != null &&
                        live.roleKey.isNotBlank() && live.roleKey != account.roleKey
                    ) {
                        Text(
                            "⚠ 游戏内当前登录：${live.name}（与所选账号不一致）",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.error,
                            modifier = Modifier.padding(top = Spacing.xs),
                        )
                    }
                }
            }
        }

        if (account != null) {
            // 调度器状态：primary 容器 + 图标
            item {
                OasCard(tone = OasCardTone.Secondary) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        OasIconBadge(
                            Icons.Filled.Dns,
                            tone = OasCardTone.Neutral,
                            size = 40.dp,
                        )
                        Spacer(Modifier.size(Spacing.sm))
                        Column(Modifier.weight(1f)) {
                            Text(
                                "调度器",
                                style = MaterialTheme.typography.labelMedium,
                                color = captionColorFor(OasCardTone.Secondary),
                            )
                            Text(
                                wsState.ifBlank { "未连接" },
                                style = MaterialTheme.typography.titleMedium,
                                color = contentColorFor(OasCardTone.Secondary),
                            )
                        }
                    }
                    running?.let {
                        Text(
                            "正在执行：${t(it)}",
                            style = MaterialTheme.typography.bodySmall,
                            color = captionColorFor(OasCardTone.Secondary),
                            modifier = Modifier.padding(top = Spacing.xs),
                        )
                    }
                    if (queueCount > 0) {
                        Text(
                            "队列 $queueCount 项",
                            style = MaterialTheme.typography.bodySmall,
                            color = captionColorFor(OasCardTone.Secondary),
                        )
                    }
                    board?.serverTimeText?.takeIf { it.isNotBlank() }?.let {
                        Text(
                            "服务器时间 $it",
                            style = MaterialTheme.typography.labelSmall,
                            color = captionColorFor(OasCardTone.Secondary),
                        )
                    }
                }
            }

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
                    OasStateBlock(
                        loading = false, error = null,
                        emptyText = "暂无数据", emptyIcon = Icons.Filled.Inbox,
                    )
                }
                !b.success -> item {
                    OasCard(tone = OasCardTone.Error) {
                        Text(
                            b.error ?: "读取失败",
                            style = MaterialTheme.typography.bodyMedium,
                            color = contentColorFor(OasCardTone.Error),
                        )
                        b.hint?.let {
                            Text(
                                it,
                                style = MaterialTheme.typography.bodySmall,
                                color = captionColorFor(OasCardTone.Error),
                            )
                        }
                    }
                }
                subTab == 0 -> {
                    // 每日任务：主色容器 + 大数值
                    item {
                        OasStatCard(
                            label = "每日任务",
                            value = "${b.dailyDone} / ${b.dailyTotal}",
                            icon = Icons.Filled.CheckCircle,
                            tone = OasCardTone.Primary,
                            supporting = "已完成 ${b.dailyDone} 项",
                        )
                    }
                    items(b.daily) { d ->
                        Row(
                            Modifier.fillMaxWidth().padding(vertical = Spacing.xs),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Icon(
                                if (d.done) Icons.Filled.CheckCircle
                                else Icons.Filled.RadioButtonUnchecked,
                                contentDescription = null,
                                tint = if (d.done) MaterialTheme.colorScheme.primary
                                       else MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.size(18.dp),
                            )
                            Text(
                                t(d.title.ifBlank { d.name }),
                                style = MaterialTheme.typography.bodyMedium,
                                color = if (d.done) MaterialTheme.colorScheme.onSurfaceVariant
                                        else MaterialTheme.colorScheme.onSurface,
                                modifier = Modifier.weight(1f).padding(start = Spacing.sm),
                            )
                            Text(
                                d.progressText,
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    }
                    item {
                        OasStatCard(
                            label = "悬赏封印",
                            value = "${b.monsterDone} / ${b.monster.size}",
                            icon = Icons.Filled.Inbox,
                            tone = OasCardTone.Tertiary,
                        )
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
                        item {
                            OasStateBlock(
                                loading = false, error = null,
                                emptyText = "无（或游戏内未打开活动页）",
                                emptyIcon = Icons.Filled.Inbox,
                            )
                        }
                    } else {
                        items(b.activities) { a ->
                            OasCard(tone = OasCardTone.Tertiary) {
                                Text(
                                    t(a.name),
                                    style = MaterialTheme.typography.titleSmall,
                                    color = contentColorFor(OasCardTone.Tertiary),
                                )
                                Text(
                                    "${a.begin} → ${a.end}",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = captionColorFor(OasCardTone.Tertiary),
                                )
                            }
                        }
                    }
                    item { OasSectionTitle("活动面板任务") }
                    if (b.activity.isEmpty()) {
                        item {
                            OasStateBlock(
                                loading = false, error = null,
                                emptyText = "无（需在游戏内打开过活动页）",
                            )
                        }
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

            // 结界寄养：两张卡片，寄养用 primary、自结界卡用 tertiary
            item {
                val fo = foster
                val f = fo?.foster
                OasCard(
                    tone = when {
                        fo == null && fosterError != null -> OasCardTone.Error
                        f != null && (f.remaining ?: 0) <= 0 -> OasCardTone.Primary
                        else -> OasCardTone.NeutralHigh
                    },
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        OasIconBadge(
                            Icons.Filled.HourglassEmpty,
                            tone = OasCardTone.Primary,
                            size = 40.dp,
                        )
                        Spacer(Modifier.size(Spacing.sm))
                        Column(Modifier.weight(1f)) {
                            OasCaption("结界寄养")
                            when {
                                fo == null && fosterError != null ->
                                    OasError(fosterError ?: "")
                                fo == null -> OasCaption("读取中…")
                                !fo.success -> OasError(fo.error ?: "读取失败")
                                f == null || f.active == false ->
                                    OasBody("当前未寄养")
                                (f.remaining ?: 0) <= 0 ->
                                    Text(
                                        "已成熟，去收取",
                                        style = MaterialTheme.typography.titleMedium,
                                        color = MaterialTheme.colorScheme.primary,
                                    )
                                else -> {
                                    val r = (f.remaining ?: 0) / 60
                                    Text(
                                        "${r / 60}小时${r % 60}分后成熟",
                                        style = MaterialTheme.typography.titleMedium,
                                        color = MaterialTheme.colorScheme.onSurface,
                                    )
                                    OasCaption("${f.cardName ?: f.cardItem} · ${f.endText} 收取")
                                }
                            }
                        }
                    }
                }
            }

            item {
                val o = foster?.own
                OasCard(tone = OasCardTone.Tertiary) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        OasIconBadge(
                            Icons.Filled.PlayCircle,
                            tone = OasCardTone.Neutral,
                            size = 36.dp,
                        )
                        Spacer(Modifier.size(Spacing.sm))
                        Column(Modifier.weight(1f)) {
                            Text(
                                "自结界卡",
                                style = MaterialTheme.typography.labelMedium,
                                color = captionColorFor(OasCardTone.Tertiary),
                            )
                            when {
                                o == null || o.unknown == true ->
                                    Text(
                                        "未读取",
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = contentColorFor(OasCardTone.Tertiary),
                                    )
                                o.empty == true ->
                                    Text(
                                        "空",
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = contentColorFor(OasCardTone.Tertiary),
                                    )
                                (o.remaining ?: 0) <= 0 ->
                                    Text(
                                        "已成熟",
                                        style = MaterialTheme.typography.titleSmall,
                                        color = MaterialTheme.colorScheme.primary,
                                    )
                                else -> {
                                    val r = (o.remaining ?: 0) / 60
                                    Text(
                                        "${o.cardName ?: o.cardItem}",
                                        style = MaterialTheme.typography.titleSmall,
                                        color = contentColorFor(OasCardTone.Tertiary),
                                    )
                                    Text(
                                        "${r / 60}小时${r % 60}分",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = captionColorFor(OasCardTone.Tertiary),
                                    )
                                }
                            }
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