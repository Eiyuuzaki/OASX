package com.oas.oasx.mobile.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.CollectApi
import com.oas.oasx.mobile.data.ResourceAggregator
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.LedgerResp
import com.oas.oasx.mobile.net.ResourceDatesResp
import com.oas.oasx.mobile.net.ResourceDay
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasCard
import com.oas.oasx.mobile.ui.components.OasCardLevel
import com.oas.oasx.mobile.ui.components.OasError
import com.oas.oasx.mobile.ui.components.OasLabel
import com.oas.oasx.mobile.ui.components.OasMetric
import com.oas.oasx.mobile.ui.components.OasPageTitle
import com.oas.oasx.mobile.ui.components.OasStateBlock
import com.oas.oasx.mobile.ui.theme.Spacing
import java.io.InputStream

/// 资产页：账号维度，三视图（单日/7天/30天）+ 货币图标 + 采集控制。
/// 全部使用 M3 语义组件。
@Composable
fun AssetsPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    val acctKey = account?.key
    val context = LocalContext.current

    var mode by remember { mutableIntStateOf(0) }
    var rows by remember(acctKey, mode) { mutableStateOf<List<RowModel>?>(null) }
    var header by remember(acctKey, mode) { mutableStateOf("") }
    var error by remember(acctKey, mode) { mutableStateOf<String?>(null) }
    var loading by remember(acctKey, mode) { mutableStateOf(false) }
    var showPicker by remember { mutableStateOf(false) }

    var collecting by remember(acctKey) { mutableStateOf<Boolean?>(null) }
    var collectMsg by remember(acctKey) { mutableStateOf("") }
    var collectBusy by remember(acctKey) { mutableStateOf(false) }

    LaunchedEffect(acctKey, backend) {
        val acc = account ?: return@LaunchedEffect
        try {
            collecting = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                CollectApi.status(acc.config).running
            }
        } catch (e: Exception) {
            collecting = null
        }
    }

    fun runCollect(action: suspend (String) -> Unit, okMsg: String) {
        val acc = account ?: return
        collectBusy = true
        collectMsg = "执行中…"
        Thread {
            try {
                kotlinx.coroutines.runBlocking { action(acc.config) }
                android.os.Handler(android.os.Looper.getMainLooper()).post {
                    collectMsg = okMsg
                    collectBusy = false
                }
            } catch (e: Exception) {
                android.os.Handler(android.os.Looper.getMainLooper()).post {
                    collectMsg = "失败：${e.message?.take(80)}"
                    collectBusy = false
                }
            }
        }.start()
    }

    LaunchedEffect(acctKey, mode, backend) {
        val acc = account ?: return@LaunchedEffect
        loading = true
        error = null
        rows = null
        try {
            val result = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                if (mode == 0) {
                    val dates = Api.getJsonOrThrow<ResourceDatesResp>(
                        "/resource/${enc(acc.config)}/dates?role_key=" + enc(acc.roleKey))
                    val d = dates.dates.firstOrNull() ?: ""
                    if (d.isEmpty()) null else loadDay(context, acc.config, acc.roleKey, d)
                } else {
                    loadLedger(context, acc.config, acc.roleKey, if (mode == 1) 7 else 30)
                }
            }
            if (result == null) {
                error = "该账号没有采集记录"
            } else {
                rows = result.first
                header = result.second
            }
        } catch (e: Exception) {
            error = e.message?.take(120)
        }
        loading = false
    }

    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(Spacing.md),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm),
    ) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                OasPageTitle("资产", Modifier.weight(1f))
                if (acct.accounts.isNotEmpty()) {
                    TextButton(onClick = { showPicker = true }) {
                        Text("切换账号", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }
            OasCaption(
                account?.let { "${it.name} Lv${it.level} · ${it.serverName} · ${it.config}" }
                    ?: when {
                        acct.loading -> "账号加载中…"
                        acct.error != null -> "账号加载失败"
                        acct.loaded -> "无可用账号"
                        else -> "准备中…"
                    },
            )
        }

        // 采集控制
        if (account != null) {
            item {
                OasCard {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        OasLabel("采集")
                        Text(
                            when (collecting) {
                                true -> "● 采集中"
                                false -> "○ 未采集"
                                null -> "? 未知"
                            },
                            style = MaterialTheme.typography.bodyMedium,
                            color = if (collecting == true) MaterialTheme.colorScheme.primary
                                    else MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(start = Spacing.sm),
                        )
                    }
                    Row(horizontalArrangement = Arrangement.spacedBy(Spacing.xs)) {
                        TextButton(
                            enabled = !collectBusy,
                            onClick = {
                                runCollect({ c -> CollectApi.collectNow(c) }, "已请求采集")
                            },
                        ) { Text("立即采集", style = MaterialTheme.typography.labelLarge) }
                        TextButton(
                            enabled = !collectBusy,
                            onClick = { runCollect({ c -> CollectApi.start(c) }, "已启动采集") },
                        ) { Text("启动", style = MaterialTheme.typography.labelLarge) }
                        TextButton(
                            enabled = !collectBusy,
                            onClick = { runCollect({ c -> CollectApi.stop(c) }, "已停止采集") },
                        ) { Text("停止", style = MaterialTheme.typography.labelLarge) }
                    }
                    if (collectMsg.isNotBlank()) OasCaption(collectMsg)
                }
            }
        }

        // 视图模式
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(Spacing.sm)) {
                listOf("单日快照" to 0, "近 7 天" to 1, "近 30 天" to 2).forEach { (label, index) ->
                    FilterChip(
                        selected = mode == index,
                        onClick = { mode = index },
                        label = { Text(label) },
                    )
                }
            }
        }

        when {
            account == null -> item {
                OasStateBlock(
                    loading = acct.loading,
                    error = acct.error,
                    emptyText = if (acct.loaded) "没有可用账号" else null,
                    onRetry = if (acct.loading) null else {
                        { AccountContext.refreshInBackground() }
                    },
                )
            }
            loading -> item {
                OasStateBlock(loading = true, error = null)
            }
            error != null -> item {
                OasCard { OasError(error ?: "") }
            }
            rows.isNullOrEmpty() -> item {
                OasStateBlock(loading = false, error = null, emptyText = "该区间没有资源数据")
            }
            else -> {
                if (header.isNotBlank()) {
                    item { OasCaption(header) }
                }
                items(rows ?: emptyList()) { row ->
                    Row(
                        Modifier.fillMaxWidth().padding(vertical = Spacing.xs),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        CurrencyIcon(row.icon, modifier = Modifier.size(32.dp))
                        Column(Modifier.weight(1f).padding(horizontal = Spacing.sm)) {
                            OasBody(row.name)
                            if (mode != 0) {
                                OasCaption("起 ${fmt(row.base)}")
                            }
                        }
                        Column(horizontalAlignment = Alignment.End) {
                            Text(
                                fmt(row.count),
                                style = MaterialTheme.typography.titleMedium,
                                color = MaterialTheme.colorScheme.onSurface,
                            )
                            if (mode != 0) {
                                Text(
                                    if (row.delta == 0L) "持平"
                                    else (if (row.delta > 0L) "+" else "") + fmt(row.delta),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = if (row.delta >= 0L)
                                        MaterialTheme.colorScheme.primary
                                    else MaterialTheme.colorScheme.error,
                                )
                            }
                        }
                    }
                }
            }
        }
    }

    if (showPicker) {
        AccountSwitcherDialog(
            accounts = acct.accounts,
            current = account,
            loading = acct.loading,
            error = acct.error,
            onSelect = { acc ->
                AccountContext.select(acc)
                showPicker = false
            },
            onRefresh = {
                showPicker = false
                AccountContext.refreshInBackground()
            },
            onDismiss = { showPicker = false },
        )
    }
}

data class RowModel(
    val name: String,
    val count: Long,
    val delta: Long,
    val base: Long,
    val icon: String?,
)

private fun enc(s: String): String = java.net.URLEncoder.encode(s, "UTF-8")

private fun loadDay(
    context: android.content.Context,
    config: String,
    roleKey: String,
    date: String,
): Pair<List<RowModel>, String> {
    val day = Api.getJsonOrThrow<ResourceDay>(
        "/resource/${enc(config)}?date=$date&role_key=" + enc(roleKey))
    val cards = ResourceAggregator.aggregate(
        context = context,
        raw = day.items.map { Triple(it.id, it.name, it.count) },
        deltas = day.items.associate { it.name to (it.delta ?: 0L) },
        icons = day.items.associate { it.name to it.icon },
    )
    return cards.map { RowModel(it.name, it.count, 0, 0, it.icon) } to
        "${day.date} · 采集于 ${day.updatedAt.ifBlank { "—" }}"
}

private fun loadLedger(
    context: android.content.Context,
    config: String,
    roleKey: String,
    days: Int,
): Pair<List<RowModel>, String> {
    val ledger = Api.getJsonOrThrow<LedgerResp>(
        "/resource/${enc(config)}/ledger?days=$days&role_key=" + enc(roleKey))
    val cards = ResourceAggregator.aggregate(
        context = context,
        raw = ledger.items.map { Triple(it.id, it.name, it.count) },
        deltas = ledger.items.associate { it.name to (it.delta ?: 0L) },
        icons = ledger.items.associate { it.name to it.icon },
    )
    return cards.map {
        RowModel(it.name, it.count, it.delta, it.count - it.delta, it.icon)
    } to "近 $days 天 · 基线 ${ledger.baseDate.ifBlank { "缺失" }} → ${ledger.lastDate}"
}

/// 从 assets/currency/{name}.png 读取；缺失显示首字占位。
@Composable
fun CurrencyIcon(base: String?, modifier: Modifier = Modifier) {
    val baseName = base ?: ""
    val context = LocalContext.current
    var bitmap by remember(baseName) { mutableStateOf<android.graphics.Bitmap?>(null) }
    if (baseName.isNotBlank() && bitmap == null) {
        LaunchedEffect(baseName, context) {
            bitmap = try {
                val name = if (baseName.endsWith(".png"))
                    baseName.substring(0, baseName.length - 4) else baseName
                val stream: InputStream = context.assets.open("currency/$name.png")
                val bm = android.graphics.BitmapFactory.decodeStream(stream)
                stream.close()
                bm
            } catch (e: Exception) {
                null
            }
        }
    }
    val bm = bitmap
    if (bm != null) {
        Image(bitmap = bm.asImageBitmap(), contentDescription = null, modifier = modifier)
    } else {
        Column(
            modifier,
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Text(
                baseName.firstOrNull()?.toString() ?: "?",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}