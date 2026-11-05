package com.oas.oasx.mobile.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
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
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.LedgerResp

/// 资产页：当前账号近 7 天账本（数据归属 = 账号，role_key 随账号传递）。
@Composable
fun AssetsPage(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()

    when (val account = acct.current) {
        null -> Column(Modifier.fillMaxSize().padding(24.dp)) {
            Text("资产", fontSize = 22.sp)
            Text("先在首页选择账号", fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        else -> {
            AsyncView(key = "ledger:$backend:${account.config}:${account.roleKey}", fetch = {
                Api.getJsonOrThrow<LedgerResp>(
                    "/resource/${account.config}/ledger?days=7" +
                        "&role_key=${java.net.URLEncoder.encode(account.roleKey, "UTF-8")}"
                ) to account
            }) { (ledger, a) ->
                LazyColumn(Modifier.fillMaxSize().padding(16.dp)) {
                    item { PageTitle("资产 · 近 7 天") }
                    item {
                        Text(
                            "${a.displayName} · 基线 ${ledger.baseDate.ifBlank { "缺失" }} " +
                                "→ ${ledger.lastDate}",
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(bottom = 8.dp),
                        )
                    }
                    items(ledger.items.filter { it.delta != null }.take(40)) { item ->
                        val d = item.delta ?: 0L
                        Row(Modifier.fillMaxWidth().padding(vertical = 6.dp)) {
                            Column(Modifier.weight(1f)) {
                                Text(item.name, fontSize = 14.sp)
                                Text("起 ${fmt(item.count - d)}", fontSize = 11.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            Column(horizontalAlignment = Alignment.End) {
                                Text(fmt(item.count), fontSize = 16.sp)
                                Text(
                                    if (d == 0L) "持平" else (if (d > 0L) "+" else "") + fmt(d),
                                    fontSize = 13.sp,
                                    color = if (d >= 0L) MaterialTheme.colorScheme.primary
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