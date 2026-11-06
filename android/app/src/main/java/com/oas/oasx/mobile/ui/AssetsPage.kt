package com.oas.oasx.mobile.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
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
import com.oas.oasx.mobile.data.Prefs
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.LedgerResp
import com.oas.oasx.mobile.net.ResourceDatesResp
import com.oas.oasx.mobile.net.ResourceDay
import com.oas.oasx.mobile.data.RoleEntry
import com.oas.oasx.mobile.data.RolesResponse
import java.io.InputStream

/// 资产页：账号维度，三种模式（单日快照 / 7天 / 30天）+ 货币图标。
///
/// 与 WPF ResourcePage 对齐：页内自带账号下拉，不依赖首页先选账号——
/// 打开资产页时若全局无当前账号，自动取第一个有角色的调度器账号并记住。
@Composable
fun AssetsPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    var selfAccount by remember { mutableStateOf<AccountContext.Account?>(null) }
    var mode by remember { mutableIntStateOf(0) }
    var rows by remember { mutableStateOf<List<RowModel>?>(null) }
    var header by remember { mutableStateOf("") }
    var error by remember { mutableStateOf<String?>(null) }
    var reload by remember { mutableIntStateOf(0) }
    var showPicker by remember { mutableStateOf(false) }
    var loadingAccounts by remember { mutableStateOf(false) }

    // 当前生效账号：优先全局当前账号，否则页内自选
    val effective = acct.current ?: selfAccount

    // 全局无账号时，本页自行拉一次角色列表并落定第一个
    LaunchedEffect(acct.current, selfAccount) {
        if (acct.current != null || selfAccount != null) return@LaunchedEffect
        loadingAccounts = true
        try {
            val list = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                loadAllAccounts()
            }
            selfAccount = list.firstOrNull()
            if (selfAccount != null) {
                Prefs.currentConfig =
                    "${selfAccount!!.config}|${selfAccount!!.roleKey}"
            }
        } catch (e: Exception) {
            error = "账号加载失败：${e.message?.take(80)}"
        }
        loadingAccounts = false
    }

    LaunchedEffect(effective?.config, effective?.roleKey, mode, reload) {
        val acc = effective ?: return@LaunchedEffect
        rows = null
        error = null
        try {
            val result = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                if (mode == 0) {
                    val dates = Api.getJsonOrThrow<ResourceDatesResp>(
                        "/resource/${acc.config}/dates?role_key=" + enc(acc.roleKey))
                    val d = dates.dates.firstOrNull() ?: ""
                    loadDay(acc.config, acc.roleKey, d)
                } else {
                    loadLedger(acc.config, acc.roleKey, if (mode == 1) 7 else 30)
                }
            }
            rows = result.first
            header = result.second
        } catch (e: Exception) {
            error = e.message?.take(100)
        }
    }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("资产", fontSize = 22.sp, modifier = Modifier.weight(1f))
            Text(
                "切换账号",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.primary,
                modifier = Modifier.clickable { showPicker = true },
            )
        }
        Text(
            effective?.let { "${it.name} Lv${it.level} · ${it.serverName} · ${it.config}" }
                ?: if (loadingAccounts) "账号加载中…" else "无可用账号",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

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
        Text(header, fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp))

        error?.let {
            Text(it, fontSize = 12.sp, color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(top = 8.dp))
        }

        val data = rows
        when {
            effective == null -> Text(
                if (loadingAccounts) "读取账号中…" else "无可用账号（需先在各调度器下登录游戏）",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 12.dp),
            )
            data == null -> Text("读取中…", fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp))
            else -> LazyColumn(Modifier.fillMaxSize().padding(top = 4.dp)) {
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

    if (showPicker) {
        AccountSwitcherDialog(
            accounts = acct.accounts.ifEmpty { listOfNotNull(selfAccount) },
            current = effective,
            loading = acct.loading || loadingAccounts,
            error = acct.error,
            onSelect = { acc ->
                AccountContext.select(acc)
                selfAccount = acc
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

/// 拉取所有调度器的角色并扁平化（资产页自助用，无需全局上下文已加载）。
private fun loadAllAccounts(): List<AccountContext.Account> {
    val configs = Api.configList()
    val out = mutableListOf<AccountContext.Account>()
    configs.forEach { config ->
        try {
            val resp = Api.getJsonOrThrow<RolesResponse>(
                "/resource/${enc(config)}/roles")
            resp.roles.forEach { r: RoleEntry ->
                out.add(
                    AccountContext.Account(
                        config = config,
                        roleKey = r.roleKey,
                        name = r.name,
                        level = r.level,
                        serverName = r.serverName,
                        lastUpdated = r.lastUpdated,
                        dates = r.dates,
                    ),
                )
            }
        } catch (e: Exception) {
            // 单个调度器失败不影响其它
        }
    }
    return out
}

private fun loadDay(config: String, roleKey: String, date: String): Pair<List<RowModel>, String> {
    val day = Api.getJsonOrThrow<ResourceDay>(
        "/resource/$config?date=$date&role_key=" + enc(roleKey))
    val rows = day.items.map { RowModel(it.name, it.count, 0, 0, it.icon) }
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