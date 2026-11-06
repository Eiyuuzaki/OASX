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
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasStateBlock
import com.oas.oasx.mobile.ui.theme.Spacing

/// 账号切换弹窗（M3 AlertDialog + 语义排版）。
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
        title = { Text("切换账号", style = MaterialTheme.typography.titleLarge) },
        text = {
            when {
                loading -> OasStateBlock(loading = true, error = null)
                error != null -> OasStateBlock(
                    loading = false, error = error, onRetry = onRefresh)
                accounts.isEmpty() -> OasStateBlock(
                    loading = false,
                    error = null,
                    emptyText = "没有可用账号（需先在各调度器下登录游戏）",
                )
                else -> LazyColumn(Modifier.heightIn(max = 420.dp)) {
                    items(accounts) { acc ->
                        val isCurrent = current != null &&
                            acc.config == current.config &&
                            acc.roleKey == current.roleKey
                        Row(
                            Modifier.fillMaxWidth()
                                .clickable { onSelect(acc) }
                                .padding(vertical = Spacing.sm),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Column(Modifier.weight(1f)) {
                                OasBody(
                                    acc.name.ifBlank { acc.roleKey },
                                    color = if (isCurrent) MaterialTheme.colorScheme.primary
                                            else MaterialTheme.colorScheme.onSurface,
                                )
                                OasCaption("Lv${acc.level} · ${acc.serverName} · 调度器 ${acc.config}")
                            }
                            if (isCurrent) {
                                Text(
                                    "✓",
                                    style = MaterialTheme.typography.titleMedium,
                                    color = MaterialTheme.colorScheme.primary,
                                )
                            }
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onRefresh) {
                Text("刷新", style = MaterialTheme.typography.labelLarge)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("关闭", style = MaterialTheme.typography.labelLarge)
            }
        },
    )
}