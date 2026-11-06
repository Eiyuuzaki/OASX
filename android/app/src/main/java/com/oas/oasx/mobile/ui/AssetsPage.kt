package com.oas.oasx.mobile.ui

import androidx.compose.foundation.Image
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
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.LedgerResp
import com.oas.oasx.mobile.net.ResourceDay
import com.oas.oasx.mobile.net.ResourceDatesResp
import java.io.InputStream

/// 资产页：账号维度，三种模式（单日快照 / 7天账本 / 30天账本）+ 图标。
/// 数据加载放 IO 线程（rememberCoroutineScope），UI 用状态驱动。
@Composable
fun AssetsPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    var mode by remember { mutableIntStateOf(0) }
    var rows by remember { mutableStateOf<List<RowModel>?>(null) }
    var header by remember { mutableStateOf("") }
    var error by remember { mutableStateOf<String?>(null) }
    var reload by remember { mutableStateOf(0) }
    val scope = rememberCoroutineScope()

    val modes = listOf("单日快照" to 0, "近 7 天" to 1, "近 30 天" to 2)

    LaunchedEffect(account?.config, account?.roleKey, mode, reload) {
        val config = account?.config ?: return@LaunchedEffect
        val roleKey = account?.roleKey ?: ""
        rows = null
        error = null
        try {
            val result = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                if (mode == 0) {
                    val dates = Api.getJsonOrThrow<ResourceDatesResp>(
                        "/resource/$config/dates?role_key=" + enc(roleKey))
                    val d = dates.dates.firstOrNull() ?: ""
                    loadDay(config, roleKey, d)
                } else {
                    val days = if (mode == 1) 7 else 30
                    loadLedger(config, roleKey, days)
                }
            }
            rows = result.first
            header = result.second
        } catch (e: Exception) {
            error = e.message?.take(100)
        }
    }

    if (account == null) {
        Column(Modifier.fillMaxSize().padding(24.dp)) {
            Text("资产", fontSize = 22.sp)
            Text("先在首页选择账号", fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        return
    }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            modes.forEach { (label, index) ->
                FilterChip(
                    selected = mode == index,
                    onClick = { mode = index },
                    label = { Text(label, fontSize = 12.sp) },
                )
            }
        }
        Text("资产 · ${account.name}", fontSize = 22.sp,
            modifier = Modifier.padding(top = 6.dp))
        Text(header, fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant)

        error?.let {
            Text(it, fontSize = 12.sp, color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(top = 8.dp))
        }

        val data = rows
        if (data == null) {
            Text("读取中…", fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp))
        } else {
            LazyColumn(Modifier.fillMaxSize().padding(top = 4.dp)) {
                items(data) { row ->
                    Row(Modifier.fillMaxWidth().padding(vertical = 8.dp),
                        verticalAlignment = Alignment.CenterVertically) {
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

data class RowModel(
    val name: String,
    val count: Long,
    val delta: Long,
    val base: Long,
    val icon: String?,
)

private fun enc(s: String): String = java.net.URLEncoder.encode(s, "UTF-8")

private fun loadDay(config: String, roleKey: String, date: String): Pair<List<RowModel>, String> {
    val day = Api.getJsonOrThrow<ResourceDay>(
        "/resource/$config?date=$date&role_key=" + enc(roleKey))
    val rows = day.items.map {
        RowModel(it.name, it.count, 0, 0, it.icon)
    }
    return rows to "${day.date} · 采集于 ${day.updatedAt.ifBlank { "—" }}"
}

private fun loadLedger(config: String, roleKey: String, days: Int): Pair<List<RowModel>, String> {
    val ledger = Api.getJsonOrThrow<LedgerResp>(
        "/resource/$config/ledger?days=$days&role_key=" + enc(roleKey))
    val rows = ledger.items.filter { it.delta != null }.map {
        RowModel(
            name = it.name,
            count = it.count,
            delta = it.delta ?: 0L,
            base = it.count - (it.delta ?: 0L),
            icon = null,
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
                    baseName.substring(0, baseName.length - 4)
                    else baseName
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
        Column(modifier, horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center) {
            Text(
                baseName.firstOrNull()?.let { it.toString() } ?: "?",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}