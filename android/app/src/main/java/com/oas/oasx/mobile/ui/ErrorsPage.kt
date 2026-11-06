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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
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
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.ErrorsApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/// 错误记录：列出后端归档的异常目录，点开看 log.txt 与截图。
///
/// 对齐 Flutter error_records_panel：列表（时间/脚本/大小/截图数）+ 详情（全文 + 图）。
@Composable
fun ErrorsPage(onBack: () -> Unit) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    var scopeAll by remember { mutableIntStateOf(0) } // 0=全部 1=当前账号
    var items by remember { mutableStateOf<List<ErrorsApi.Item>?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    var detail by remember { mutableStateOf<ErrorsApi.Detail?>(null) }
    var openId by remember { mutableStateOf<String?>(null) }
    var reload by remember { mutableIntStateOf(0) }

    LaunchedEffect(scopeAll, reload, acct.current?.config) {
        items = null
        error = null
        try {
            val script = if (scopeAll == 1) acct.current?.config else null
            items = withContext(Dispatchers.IO) { ErrorsApi.list(scriptName = script).items }
        } catch (e: Exception) {
            error = e.message?.take(120)
        }
    }

    LaunchedEffect(openId) {
        val id = openId ?: return@LaunchedEffect
        detail = null
        try {
            detail = withContext(Dispatchers.IO) { ErrorsApi.detail(id) }
        } catch (e: Exception) {
            error = "详情加载失败：${e.message?.take(100)}"
        }
    }

    // ── 详情视图 ──
    if (openId != null) {
        Column(Modifier.fillMaxSize().padding(16.dp)) {
            Text(
                "← 返回列表",
                Modifier.clickable { openId = null; detail = null },
                fontSize = 14.sp,
                color = MaterialTheme.colorScheme.primary,
            )
            val d = detail
            if (d == null) {
                if (error != null) {
                    Text("详情加载失败：$error", fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.error,
                        modifier = Modifier.padding(top = 10.dp))
                } else {
                    Text("加载中…", fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 10.dp))
                }
                return@Column
            }
            Text(
                (d.scriptName ?: "未知脚本") + " · " + d.time.replace('T', ' ').take(19),
                fontSize = 16.sp,
                modifier = Modifier.padding(top = 8.dp),
            )
            Text(
                "${d.log?.fileName ?: "log.txt"} · ${(d.log?.content?.length ?: 0)} 字符 · " +
                    "${d.images.size} 张截图",
                fontSize = 11.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )

            // 截图
            if (d.images.isNotEmpty()) {
                LazyColumn(
                    Modifier.fillMaxWidth().heightIn(max = 320.dp)
                        .padding(top = 8.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                ) {
                    items(d.images) { img ->
                        RemoteImage(
                            url = ErrorsApi.imageUrl(d.id, img.name),
                            modifier = Modifier.fillMaxWidth().heightIn(max = 240.dp),
                        )
                    }
                }
            }

            // 日志全文
            Text("日志内容", fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 10.dp, bottom = 4.dp))
            Card(Modifier.fillMaxWidth().weight(1f)) {
                Column(Modifier.verticalScroll(rememberScrollState()).padding(10.dp)) {
                    Text(
                        d.log?.content ?: "（无内容）",
                        fontSize = 10.sp,
                        lineHeight = 14.sp,
                        fontFamily = FontFamily.Monospace,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
        }
        return
    }

    // ── 列表视图 ──
    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Text(
            "← 返回",
            Modifier.clickable { onBack() },
            fontSize = 14.sp,
            color = MaterialTheme.colorScheme.primary,
        )
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("错误记录", fontSize = 22.sp, modifier = Modifier.weight(1f))
            TextButton(onClick = { reload++ }) { Text("刷新", fontSize = 12.sp) }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(
                selected = scopeAll == 0,
                onClick = { scopeAll = 0 },
                label = { Text("全部", fontSize = 12.sp) },
            )
            FilterChip(
                selected = scopeAll == 1,
                onClick = { scopeAll = 1 },
                label = { Text("当前账号", fontSize = 12.sp) },
            )
        }

        error?.let {
            Text(it, fontSize = 12.sp, color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(top = 8.dp))
        }

        val list = items
        if (list == null) {
            Row(Modifier.fillMaxWidth().padding(top = 16.dp),
                horizontalArrangement = Arrangement.Center) {
                CircularProgressIndicator(Modifier.heightIn(max = 24.dp))
            }
            return@Column
        }
        if (list.isEmpty()) {
            Text("没有错误记录（运行异常时后端会自动归档）",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 12.dp))
            return@Column
        }
        LazyColumn(Modifier.fillMaxSize().padding(top = 8.dp)) {
            items(list) { it ->
                Card(
                    Modifier.fillMaxWidth().padding(vertical = 3.dp)
                        .clickable { openId = it.id },
                ) {
                    Row(
                        Modifier.fillMaxWidth().padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Column(Modifier.weight(1f)) {
                            Text(
                                it.time.replace('T', ' ').take(19),
                                fontSize = 13.sp,
                            )
                            Text(
                                buildString {
                                    append(it.scriptName ?: "旧格式")
                                    append(" · ")
                                    append(fmt(it.logSize))
                                    append(" 字节")
                                    if (it.imageCount > 0) append(" · ${it.imageCount} 图")
                                },
                                fontSize = 11.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        Text("›", fontSize = 15.sp,
                            color = MaterialTheme.colorScheme.primary)
                    }
                }
            }
        }
    }
}

/// 远程图片（错误截图）：直接用 OkHttp 拉字节再解码。
@Composable
fun RemoteImage(url: String, modifier: Modifier = Modifier) {
    var bmp by remember(url) { mutableStateOf<android.graphics.Bitmap?>(null) }
    var failed by remember(url) { mutableStateOf(false) }
    LaunchedEffect(url) {
        try {
            val bytes = withContext(Dispatchers.IO) {
                com.oas.oasx.mobile.net.Api.client.newCall(
                    okhttp3.Request.Builder().url(url).build(),
                ).execute().use { resp ->
                    if (!resp.isSuccessful) throw RuntimeException("HTTP ${resp.code}")
                    resp.body?.bytes() ?: ByteArray(0)
                }
            }
            bmp = android.graphics.BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
            if (bmp == null) failed = true
        } catch (e: Exception) {
            failed = true
        }
    }
    val b = bmp
    when {
        b != null -> Image(
            bitmap = b.asImageBitmap(),
            contentDescription = null,
            modifier = modifier,
        )
        failed -> Text("截图加载失败", fontSize = 11.sp,
            color = MaterialTheme.colorScheme.error)
        else -> Text("加载中…", fontSize = 11.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}