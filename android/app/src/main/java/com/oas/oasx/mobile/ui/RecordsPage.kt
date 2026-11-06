package com.oas.oasx.mobile.ui

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
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.data.StatsResponse
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasCard
import com.oas.oasx.mobile.ui.components.OasCardTone
import com.oas.oasx.mobile.ui.components.OasError
import com.oas.oasx.mobile.ui.components.OasLabel
import com.oas.oasx.mobile.ui.components.OasMetric
import com.oas.oasx.mobile.ui.components.OasPageTitle
import com.oas.oasx.mobile.ui.components.OasSectionTitle
import com.oas.oasx.mobile.ui.components.OasStateBlock
import com.oas.oasx.mobile.ui.theme.Spacing

/// 记录页：统计 / 实时日志 / 错误 / 绘卷 四个子页签。
@Composable
fun RecordsPage(backend: String) {
    var subTab by remember { mutableIntStateOf(0) }

    Column(Modifier.fillMaxSize()) {
        Column(Modifier.padding(horizontal = Spacing.md, vertical = Spacing.sm)) {
            OasPageTitle("记录")
            Row(
                Modifier.fillMaxWidth().padding(top = Spacing.sm),
                horizontalArrangement = Arrangement.spacedBy(Spacing.sm),
            ) {
                listOf("统计", "日志", "错误", "绘卷").forEachIndexed { idx, label ->
                    FilterChip(
                        selected = subTab == idx,
                        onClick = { subTab = idx },
                        label = { Text(label) },
                    )
                }
            }
        }
        when (subTab) {
            1 -> LogsPage(backend)
            2 -> ErrorsPage(onBack = { subTab = 0 })
            3 -> HuijuanPage(onBack = { subTab = 0 })
            else -> StatsView(backend)
        }
    }
}

/// 统计视图：日期切换 + 总览磁贴 + 各任务 runs。
@Composable
fun StatsView(backend: String) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val account = acct.current
    var dates by remember(account?.config) { mutableStateOf<List<String>>(emptyList()) }
    var selected by remember(account?.config) { mutableStateOf<String?>(null) }
    var stats by remember(account?.config) { mutableStateOf<StatsResponse?>(null) }
    var error by remember(account?.config) { mutableStateOf<String?>(null) }
    var datesLoading by remember(account?.config) { mutableStateOf(false) }
    var statsLoading by remember(account?.config) { mutableStateOf(false) }
    var datesLoaded by remember(account?.config) { mutableStateOf(false) }

    LaunchedEffect(account?.config, backend) {
        val script = account?.config ?: return@LaunchedEffect
        datesLoading = true
        datesLoaded = false
        error = null
        try {
            val resp = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.getJsonOrThrow<com.oas.oasx.mobile.data.StatsDatesResponse>(
                    "/stats/$script/dates")
            }
            dates = resp.dates
            selected = resp.dates.firstOrNull()
        } catch (e: Exception) {
            error = e.message?.take(100)
        }
        datesLoading = false
        datesLoaded = true
    }

    LaunchedEffect(account?.config, selected) {
        val script = account?.config ?: return@LaunchedEffect
        val date = selected ?: run {
            stats = null
            return@LaunchedEffect
        }
        statsLoading = true
        try {
            stats = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
                Api.getJsonOrThrow<StatsResponse>("/stats/$script?date=$date")
            }
            error = null
        } catch (e: Exception) {
            stats = null
            error = e.message?.take(100)
        }
        statsLoading = false
    }

    Column(
        Modifier.fillMaxSize().padding(horizontal = Spacing.md),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm),
    ) {
        if (dates.isNotEmpty()) {
            Row(
                Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                OasCaption("日期 ${selected ?: "—"}（共 ${dates.size} 天）", Modifier.weight(1f))
                TextButton(onClick = {
                    val idx = dates.indexOf(selected)
                    selected = if (idx >= 0 && idx < dates.size - 1) dates[idx + 1]
                    else dates.firstOrNull()
                }) { Text("切换", style = MaterialTheme.typography.labelLarge) }
            }
        }

        error?.let { OasError(it) }

        val s = stats
        when {
            account == null -> OasStateBlock(
                loading = false, error = null, emptyText = "先在首页选择账号")
            datesLoading -> OasStateBlock(loading = true, error = null)
            statsLoading -> OasStateBlock(loading = true, error = null)
            datesLoaded && dates.isEmpty() -> OasStateBlock(
                loading = false, error = null,
                emptyText = "该账号暂无统计数据（脚本跑过一次后才会有）")
            s == null -> OasStateBlock(
                loading = false, error = null, emptyText = "无数据")
            else -> {
                Row(
                    Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(Spacing.sm),
                ) {
                    StatTile("总时长", formatDuration(s.totalRuntimeSeconds), Modifier.weight(1f))
                    StatTile("任务次数", "${s.totalTaskRunCount}", Modifier.weight(1f))
                    StatTile("战斗场次", "${s.totalBattleCount}", Modifier.weight(1f))
                }
                OasSectionTitle("各任务", Modifier.padding(top = Spacing.sm))
                LazyColumn(
                    Modifier.fillMaxSize(),
                    verticalArrangement = Arrangement.spacedBy(Spacing.sm),
                ) {
                    items(s.tasks.entries.toList()) { (name, t) ->
                        OasCard {
                            OasBody(name)
                            OasCaption("执行 ${t.runCount} 次 · 共 " +
                                formatDuration(t.totalDurationSeconds))
                            t.runs.takeLast(3).reversed().forEach { run ->
                                OasCaption("  ${run.startTime.takeLast(8)} · " +
                                    formatDuration(run.durationSeconds))
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun StatTile(label: String, value: String, modifier: Modifier = Modifier) {
    OasCard(modifier = modifier, tone = OasCardTone.Primary) {
        OasLabel(label)
        OasMetric(value)
    }
}

fun formatDuration(seconds: Double): String {
    val total = seconds.toInt()
    val h = total / 3600
    val m = (total % 3600) / 60
    return when {
        h > 0 -> "${h}小时${m}分"
        m > 0 -> "${m}分${total % 60}秒"
        else -> "${total}秒"
    }
}