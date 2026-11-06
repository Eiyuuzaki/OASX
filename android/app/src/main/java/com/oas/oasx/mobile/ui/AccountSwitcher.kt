package com.oas.oasx.mobile.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.oas.oasx.mobile.data.AccountContext

/// 账号切换弹窗。
///
/// 账号 = (调度器 config, 角色 roleKey) 二元组——同一调度器切号会产生多个账号，
/// 不同调度器各有一个当前角色。这里把扁平化后的账号列表全部列出，
/// 并标注所属调度器，避免用户误以为是"某个调度器的号"。
@Composable
fun AccountSwitcherDialog(
    accounts: List<AccountContext.Account>,
    current: AccountContext.Account?,
    loading: Boolean,
    error: String?,
    onSelect: (AccountContext.Account) -> Unit,
    onRefresh: () -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("切换账号") },
        text = {
            Column {
                if (error != null) {
                    Text(
                        error,
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.error,
                        modifier = Modifier.padding(bottom = 8.dp),
                    )
                }
                when {
                    loading -> Text("加载中…", fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                    accounts.isEmpty() -> Text(
                        "没有可用账号（需要先在各调度器下登录游戏）",
                        fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    else -> LazyColumn(Modifier.heightIn(max = 420.dp)) {
                        items(accounts) { acc ->
                            val isCurrent = current != null &&
                                acc.config == current.config &&
                                acc.roleKey == current.roleKey
                            Row(
                                Modifier.fillMaxWidth()
                                    .clickable { onSelect(acc) }
                                    .padding(vertical = 10.dp, horizontal = 4.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Column(Modifier.weight(1f)) {
                                    Text(
                                        acc.name.ifBlank { acc.roleKey },
                                        fontSize = 15.sp,
                                        color = if (isCurrent) MaterialTheme.colorScheme.primary
                                                else MaterialTheme.colorScheme.onSurface,
                                    )
                                    Text(
                                        "Lv${acc.level} · ${acc.serverName} · 调度器 ${acc.config}",
                                        fontSize = 11.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    )
                                }
                                if (isCurrent) {
                                    Text("✓", fontSize = 15.sp,
                                        color = MaterialTheme.colorScheme.primary)
                                }
                            }
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onRefresh) { Text("刷新") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("关闭") }
        },
    )
}