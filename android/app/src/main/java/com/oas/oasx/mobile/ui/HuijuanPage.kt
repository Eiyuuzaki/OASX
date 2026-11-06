package com.oas.oasx.mobile.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.HuijuanApi
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasCard
import com.oas.oasx.mobile.ui.components.OasPageTitle
import com.oas.oasx.mobile.ui.components.OasSectionTitle
import com.oas.oasx.mobile.ui.components.OasStateBlock
import com.oas.oasx.mobile.ui.theme.Spacing
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/// 绘卷：区服棋盘进度。非活动期不展示上期遗留分数。
@Composable
fun HuijuanPage(onBack: () -> Unit) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    var board by remember(account?.config) { mutableStateOf<HuijuanApi.Board?>(null) }
    var error by remember(account?.config) { mutableStateOf<String?>(null) }
    var loading by remember(account?.config) { mutableStateOf(false) }
    var reload by remember { mutableStateOf(0) }

    LaunchedEffect(account?.config, reload) {
        val acc = account ?: return@LaunchedEffect
        loading = true
        error = null
        try {
            board = withContext(Dispatchers.IO) { HuijuanApi.board(acc.config) }
        } catch (e: Exception) {
            error = e.message?.take(120)
        }
        loading = false
    }

    Column(
        Modifier.fillMaxSize().padding(horizontal = Spacing.md, vertical = Spacing.sm),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            TextButton(onClick = onBack) {
                Text("返回", style = MaterialTheme.typography.labelLarge)
            }
            OasPageTitle("绘卷", Modifier.weight(1f))
            TextButton(onClick = { reload++ }) {
                Text("刷新", style = MaterialTheme.typography.labelLarge)
            }
        }
        OasCaption(account?.let { "${it.name} · ${it.serverName}" } ?: "未选择账号")

        error?.let { OasCaption(it) }

        val b = board
        when {
            loading && b == null -> OasStateBlock(loading = true, error = null)
            b == null -> OasStateBlock(
                loading = false, error = null, emptyText = "暂无数据")
            !b.success -> OasStateBlock(
                loading = false, error = b.error ?: "读取失败")
            !HuijuanApi.isActive(b) -> OasCard {
                OasSectionTitle("当前不在绘卷活动期")
                OasCaption("活动结束后不再显示上期分数。下一次活动开启后自动恢复。")
            }
            else -> {
                OasCaption("第 ${b.issue} 期 · 目标 ${fmt(b.threshold)}")
                LazyColumn(verticalArrangement = Arrangement.spacedBy(Spacing.sm)) {
                    items(b.scrolls) { sc ->
                        OasCard {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                OasBody(sc.name.ifBlank { "卷 ${sc.id}" }, Modifier.weight(1f))
                                if (sc.finished) {
                                    Text(
                                        "✓ 已达成",
                                        style = MaterialTheme.typography.labelSmall,
                                        color = MaterialTheme.colorScheme.primary,
                                    )
                                }
                            }
                            OasCaption("区服 ${fmt(sc.score)} · " +
                                "${"%.2f".format(sc.percent)}%")
                            LinearProgressIndicator(
                                progress = { (sc.percent / 100.0).toFloat().coerceIn(0f, 1f) },
                                modifier = Modifier.fillMaxWidth()
                                    .padding(vertical = Spacing.xs),
                            )
                            Row(Modifier.fillMaxWidth()) {
                                OasCaption("我的贡献 ${fmt(sc.selfScore)}", Modifier.weight(1f))
                                sc.selfRank?.let {
                                    OasCaption("排名 $it")
                                }
                            }
                            OasCaption("${sc.beginText} → ${sc.endText}")
                        }
                    }
                }
            }
        }
    }
}