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
import androidx.compose.material3.Card
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
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.ResourceAggregator
import com.oas.oasx.mobile.data.CollectApi
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.LedgerResp
import com.oas.oasx.mobile.net.ResourceDatesResp
import com.oas.oasx.mobile.net.ResourceDay
import java.io.InputStream

/// 资产页：账号维度，三种模式（单日快照 / 7天 / 30天）+ 货币图标。
/// 账号统一来自 AccountContext（单一来源），本页不自行拉角色列表。
@Composable
fun AssetsPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    val acctKey = account?.key

    var mode by remember { mutableIntStateOf(0) }
    var rows by remember(acctKey, mode) { mutableStateOf<List<RowModel>?>(null) }
    var header by remember(acctKey, mode) { mutableStateOf("") }
    var error by remember(acctKey, mode) { mutableStateOf<String?>(null) }
    var loading by remember(acctKey, mode) { mutableStateOf(false) }
    var showPicker by remember { mutableStateOf(false) }
    val context = androidx.compose.ui.platform.LocalContext.current
    // ── 采集控制 ──
    var collecting by remember(acctKey) { mutableStateOf<Boolean?>(null) }
    var collectMsg by remember(acctKey) { mutableStateOf("") }
    var collectBusy by remember(acctKey) { mutableStateOf(false) }

    LaunchedEffect(acctKey, backend) {
        val acc = account ?: return@LaunchedEffect
        try {
            collecting = kotlinx.coroutines.withContext(
                kotlinx.coroutines.Dispatchers.IO,
            ) { CollectApi.status(acc.config).running }
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
                    if (d.isEmpty()) {
                        null
                    } else {
                        loadDay(context, acc.config, acc.roleKey, d)
                    }
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

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("资产", fontSize = 22.sp, modifier = Modifier.weight(1f))
            if (acct.accounts.isNotEmpty()) {
                Text(
                    "切换账号",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.clickable { showPicker = true },
                )
            }
        }

        // 账号行
        Text(
            account?.let { "${it.name} Lv${it.level} · ${it.serverName} · ${it.config}" }
                ?: when {
                    acct.loading -> "账号加载中…"
                    acct.error != null -> "账号加载失败"
                    acct.loaded -> "无可用账号"
                    else -> "准备中…"
                },
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        // ── 采集控制行 ──
        Row(
            Modifier.fillMaxWidth().padding(top = 8.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                when (collecting) {
                    true -> "● 采集中"
                    false -> "○ 未采集"
                    null -> "? 状态未知"
                },
                fontSize = 12.sp,
                color = if (collecting == true) MaterialTheme.colorScheme.primary
                        else MaterialTheme.colorScheme.onSurfaceVariant,
            )
            TextButton(
                enabled = !collectBusy,
                onClick = { runCollect({ c -> CollectApi.collectNow(c) }, "已请求采集") },
            ) { Text("立即采集", fontSize = 12.sp) }
            TextButton(
                enabled = !collectBusy,
                onClick = { runCollect({ c -> CollectApi.start(c) }, "已启动采集") },
            ) { Text("启动", fontSize = 12.sp) }
            TextButton(
                enabled = !collectBusy,
                onClick = { runCollect({ c -> CollectApi.stop(c) }, "已停止采集") },
            ) { Text("停止", fontSize = 12.sp) }
        }
        if (collectMsg.isNotBlank()) {
            Text(collectMsg, fontSize = 11.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }

        // 模式 chips
        Row(
            Modifier.fillMaxWidth().padding(top = 8.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            listOf("单日快照" to 0, "近 7 天" to 1, "近 30 天" to 2).forEach { (label, index) ->
                FilterChip(
                    selected = mode == index,
                    onClick = { mode = index },
                    label = { Text(label, fontSize = 12.sp) },
                )
            }
        }
        if (header.isNotBlank()) {
            Text(header, fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp))
        }

        when {
            account == null -> {
                Card(Modifier.fillMaxWidth().padding(top = 12.dp)) {
                    Column(Modifier.padding(16.dp)) {
                        Text(
                            when {
                                acct.loading -> "正在加载账号…"
                                acct.error != null -> "账号加载失败"
                                acct.loaded -> "没有可用账号"
                                else -> "准备中…"
                            },
                            fontSize = 15.sp,
                            color = if (acct.error != null) MaterialTheme.colorScheme.error
                                    else MaterialTheme.colorScheme.onSurface,
                        )
                        acct.error?.let {
                            Text(it, fontSize = 11.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        if (!acct.loading) {
                            TextButton(onClick = {
                                AccountContext.refreshInBackground()
                            }) {
                                Text(if (acct.error != null) "重试" else "重新加载")
                            }
                        }
                    }
                }
            }
            error != null -> {
                Card(Modifier.fillMaxWidth().padding(top = 12.dp)) {
                    Column(Modifier.padding(16.dp)) {
                        Text("读取失败", fontSize = 14.sp,
                            color = MaterialTheme.colorScheme.error)
                        Text(error ?: "", fontSize = 11.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
            loading || rows == null -> {
                Text("读取中…", fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 12.dp))
            }
            else -> {
                val data = rows ?: emptyList()
                if (data.isEmpty()) {
                    Text("该区间没有资源数据", fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 12.dp))
                } else {
                    LazyColumn(Modifier.fillMaxSize().padding(top = 4.dp)) {
                        items(data) { row ->
                            Row(
                                Modifier.fillMaxWidth().padding(vertical = 8.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                CurrencyIcon(row.icon, modifier = Modifier.size(28.dp))
                                Column(Modifier.weight(1f).padding(horizontal = 10.dp)) {
                                    Text(row.name, fontSize = 14.sp)
                                    if (mode != 0) {
                                        Text("起 ${fmt(row.base)}", fontSize = 11.sp,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    }
                                }
                                Column(horizontalAlignment = Alignment.End) {
                                    Text(fmt(row.count), fontSize = 16.sp)
                                    if (mode != 0) {
                                        Text(
                                            if (row.delta == 0L) "持平"
                                            else (if (row.delta > 0L) "+" else "") + fmt(row.delta),
                                            fontSize = 13.sp,
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
    // 对齐 WPF：图标白名单 + (图标,名称) 聚合 + 按图标去重
    val cards = ResourceAggregator.aggregate(
        context = context,
        raw = day.items.map { Triple(it.id, it.name, it.count) },
        deltas = day.items.associate { it.name to (it.delta ?: 0L) },
        icons = day.items.associate { it.name to it.icon },
    )
    val rows = cards.map { RowModel(it.name, it.count, 0, 0, it.icon) }
    return rows to "${day.date} · 采集于 ${day.updatedAt.ifBlank { "—" }}"
}

private fun loadLedger(
    context: android.content.Context,
    config: String,
    roleKey: String,
    days: Int,
): Pair<List<RowModel>, String> {
    val ledger = Api.getJsonOrThrow<LedgerResp>(
        "/resource/${enc(config)}/ledger?days=$days&role_key=" + enc(roleKey))
    // 账本项没有 icon 字段，白名单按 name 无法判定——沿用后端已过滤的 delta 项，
    // 但补齐图标名以便展示（ledger 的 icon 由后端在 items 内提供）
    val cards = ResourceAggregator.aggregate(
        context = context,
        raw = ledger.items.map { Triple(it.id, it.name, it.count) },
        deltas = ledger.items.associate { it.name to (it.delta ?: 0L) },
        icons = ledger.items.associate { it.name to (it.icon ?: "") },
    )
    val rows = cards.map {
        RowModel(
            name = it.name,
            count = it.count,
            delta = it.delta,
            base = it.count - it.delta,
            icon = it.icon,
        )
    }
    return rows to "近 $days 天 · 基线 ${ledger.baseDate.ifBlank { "缺失" }} → ${ledger.lastDate}"
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
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}