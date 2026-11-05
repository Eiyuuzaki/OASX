package com.oas.oasx.mobile.ui

import android.content.SharedPreferences
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
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
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.net.ActivityBoard
import com.oas.oasx.mobile.net.FosterResp
import com.oas.oasx.mobile.net.LedgerResp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/// 通用：后台请求 + 三态（加载/错误/内容）。
@Composable
fun <T> AsyncView(
    key: String,
    fetch: suspend () -> T,
    content: @Composable (T) -> Unit,
) {
    var state by remember(key) { mutableStateOf<Result<T>?>(null) }
    LaunchedEffect(key) {
        state = null
        state = withContext(Dispatchers.IO) {
            try {
                Result.success(fetch())
            } catch (e: Exception) {
                Result.failure(e)
            }
        }
    }
    when (val s = state) {
        null -> Column(
            Modifier.fillMaxSize().padding(24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally,
        ) { CircularProgressIndicator() }
        else -> s.fold(
            onFailure = { ErrorView(it.message ?: it.toString()) },
            onSuccess = { content(it) },
        )
    }
}

@Composable
fun ErrorView(msg: String) {
    Column(Modifier.fillMaxWidth().padding(24.dp)) {
        Text("读取失败", fontSize = 16.sp, color = MaterialTheme.colorScheme.error)
        Text(msg, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
fun PageTitle(text: String) {
    Text(text, fontSize = 22.sp)
}

@Composable
fun SectionTitle(text: String) {
    Text(text, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
}

fun fmt(n: Long): String = when {
    n >= 100_000_000L -> "%.1f亿".format(n / 100_000_000.0)
    n >= 10_000L -> "%.1f万".format(n / 10_000.0)
    else -> "%,d".format(n)
}

fun firstConfig(): String {
    val list = Api.configList()
    return list.firstOrNull() ?: throw RuntimeException("后端无配置")
}

/// 配置选择下拉（数据来自 /config_list）。
@Composable
fun ConfigPicker(
    configs: List<String>,
    selected: String,
    onSelect: (String) -> Unit,
) {
    var expanded by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = !expanded },
    ) {
        OutlinedTextField(
            value = selected,
            onValueChange = {},
            readOnly = true,
            label = { Text("配置") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            singleLine = true,
        )
        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
        ) {
            configs.forEach { c ->
                DropdownMenuItem(
                    text = { Text(c) },
                    onClick = { onSelect(c); expanded = false },
                )
            }
        }
    }
}

/// 仪表盘：配置选择 + 每日/悬赏 + 寄养双卡。
@Composable
fun DashboardPage(backend: String) {
    var configs by remember { mutableStateOf<List<String>?>(null) }
    var config by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(backend) {
        val list = withContext(Dispatchers.IO) {
            try {
                Api.configList()
            } catch (e: Exception) {
                emptyList()
            }
        }
        configs = list
        if (config == null) config = list.firstOrNull()
    }

    val cfg = config
    when {
        configs == null -> Column(
            Modifier.fillMaxSize().padding(24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally,
        ) { CircularProgressIndicator() }
        cfg == null -> ErrorView("后端无配置")
        else -> Column(Modifier.fillMaxSize().padding(16.dp)) {
            ConfigPicker(configs, cfg) { config = it }
            AsyncView(key = "dash:$backend:$cfg", fetch = {
                val board = Api.getJsonOrThrow<ActivityBoard>("/activity/$cfg?force=1")
                val foster = Api.getJsonOrThrow<FosterResp>("/notify/foster/refresh")
                board to foster
            }) { (board, foster) ->
                LazyColumn(
                    Modifier.fillMaxSize(),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    // 后端错误如实上浮（模拟器离线/游戏未开），不再显示 0/0
                    if (!board.success) {
                        item { ErrorView(board.error ?: "读取失败") }
                    } else {
                        item {
                            Card(Modifier.fillMaxWidth()) {
                                Column(Modifier.padding(16.dp)) {
                                    Text("每日任务", fontSize = 13.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    Text("${board.dailyDone} / ${board.dailyTotal}",
                                        fontSize = 30.sp)
                                    Text(
                                        "悬赏封印  ${board.monsterDone} / ${board.monster.size}",
                                        fontSize = 13.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    )
                                }
                            }
                        }
                    }
                    item {
                        Card(Modifier.fillMaxWidth()) {
                            Column(Modifier.padding(16.dp)) {
                                Text("寄养", fontSize = 13.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                                when {
                                    !foster.success -> Text(
                                        foster.error ?: "读取失败",
                                        fontSize = 15.sp,
                                        color = MaterialTheme.colorScheme.error,
                                    )
                                    else -> {
                                        val f = foster.foster
                                        when {
                                            f == null || f.active == false ->
                                                Text("当前未寄养", fontSize = 20.sp)
                                            (f.remaining ?: 0) <= 0 ->
                                                Text("已成熟，去收取", fontSize = 20.sp,
                                                    color = MaterialTheme.colorScheme.primary)
                                            else -> {
                                                val r = (f.remaining ?: 0) / 60
                                                Text("${r / 60}小时${r % 60}分后成熟",
                                                    fontSize = 20.sp)
                                                Text(
                                                    "${f.cardName ?: f.cardItem} · ${f.endText} 收取",
                                                    fontSize = 12.sp,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                )
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    item {
                        Card(Modifier.fillMaxWidth()) {
                            Column(Modifier.padding(16.dp)) {
                                Text("自结界卡", fontSize = 13.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                                val o = foster.own
                                when {
                                    o == null || o.unknown == true ->
                                        Text("未读取", fontSize = 18.sp)
                                    o.empty == true -> Text("空", fontSize = 18.sp)
                                    (o.remaining ?: 0) <= 0 ->
                                        Text("已成熟，去收取", fontSize = 20.sp,
                                            color = MaterialTheme.colorScheme.primary)
                                    else -> {
                                        val r = (o.remaining ?: 0) / 60
                                        Text("${o.cardName ?: o.cardItem} ${r / 60}小时${r % 60}分",
                                            fontSize = 20.sp)
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

/// 活动：当前活动 + 进行中时段。
@Composable
fun ActivityPage(backend: String) {
    var configs by remember { mutableStateOf<List<String>?>(null) }
    var config by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(backend) {
        val list = withContext(Dispatchers.IO) {
            try {
                Api.configList()
            } catch (e: Exception) {
                emptyList()
            }
        }
        configs = list
        if (config == null) config = list.firstOrNull()
    }

    val cfg = config
    when {
        configs == null || cfg == null -> Column(
            Modifier.fillMaxSize().padding(24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally,
        ) { CircularProgressIndicator() }
        else -> Column(Modifier.fillMaxSize().padding(16.dp)) {
            ConfigPicker(configs, cfg) { config = it }
            AsyncView(key = "act:$backend:$cfg", fetch = {
                Api.getJsonOrThrow<ActivityBoard>("/activity/$cfg")
            }) { board ->
                LazyColumn(
                    Modifier.fillMaxSize(),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    if (!board.success) {
                        item { ErrorView(board.error ?: "读取失败") }
                    } else {
                        item { SectionTitle("当前活动") }
                        items(board.activities.filter { it.name.isNotBlank() }) { a ->
                            Row(Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                                Text(a.name, Modifier.weight(1f), fontSize = 14.sp)
                                Text(
                                    a.end.takeIf { it.isNotBlank() }?.let { "截止 $it" } ?: "",
                                    fontSize = 12.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                        item { SectionTitle("进行中时段") }
                        items(board.live) { w ->
                            Row(Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                                Text(w.key, Modifier.weight(1f), fontSize = 14.sp)
                                Text("至 ${w.endText}", fontSize = 12.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    }
                }
            }
        }
    }
}

/// 资产：近 7 天账本。
@Composable
fun AssetsPage(backend: String) {
    var configs by remember { mutableStateOf<List<String>?>(null) }
    var config by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(backend) {
        val list = withContext(Dispatchers.IO) {
            try {
                Api.configList()
            } catch (e: Exception) {
                emptyList()
            }
        }
        configs = list
        if (config == null) config = list.firstOrNull()
    }

    val cfg = config
    when {
        configs == null || cfg == null -> Column(
            Modifier.fillMaxSize().padding(24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally,
        ) { CircularProgressIndicator() }
        else -> Column(Modifier.fillMaxSize().padding(16.dp)) {
            ConfigPicker(configs, cfg) { config = it }
            AsyncView(key = "ledger:$backend:$cfg", fetch = {
                Api.getJsonOrThrow<LedgerResp>("/resource/$cfg/ledger?days=7")
            }) { ledger ->
                LazyColumn(
                    Modifier.fillMaxSize(),
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                ) {
                    item { PageTitle("资产 · 近 7 天") }
                    item {
                        Text(
                            "基线 ${ledger.baseDate.ifBlank { "缺失" }} → ${ledger.lastDate}",
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                    items(ledger.items.filter { it.delta != null }.take(30)) { item ->
                        val d = item.delta ?: 0L
                        Row(Modifier.fillMaxWidth().padding(vertical = 6.dp)) {
                            Column(Modifier.weight(1f)) {
                                Text(item.name, fontSize = 14.sp)
                                Text(
                                    "起 ${fmt(item.count - d)}",
                                    fontSize = 11.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
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

/// 日志：v0 占位（SSE 后续接入）。
@Composable
fun LogsPage() {
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text("实时日志", fontSize = 18.sp)
        Text(
            "SSE 推流将在后续版本接入（后端 /resource/*/stream 已就绪）",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/// 设置：后端地址 + 测试连接。
@Composable
fun SettingsPage(prefs: SharedPreferences, onSaved: (String) -> Unit) {
    var url by remember { mutableStateOf(prefs.getString("backend", "") ?: "") }
    var status by remember { mutableStateOf("") }
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("设置", fontSize = 18.sp)
        Text(
            "OAS 后端地址（PC 局域网 IP，端口 22288）",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        OutlinedTextField(
            value = url,
            onValueChange = { url = it },
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text("http://192.168.x.x:22288") },
            singleLine = true,
        )
        Button(onClick = {
            val base = url.trim().trimEnd('/')
            status = "测试中…"
            Thread {
                try {
                    Api.baseUrl = base
                    val list = Api.get("/config_list")
                    prefs.edit().putString("backend", base).apply()
                    status = "连接成功：$list"
                    onSaved(base)
                } catch (e: Exception) {
                    status = "连接失败：${e.message?.take(80)}"
                }
            }.start()
        }) { Text("保存并测试") }
        if (status.isNotEmpty()) {
            Text(status, fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Text(
            "提示：PC 与手机需在同一局域网；后端监听 0.0.0.0:22288。",
            fontSize = 11.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}