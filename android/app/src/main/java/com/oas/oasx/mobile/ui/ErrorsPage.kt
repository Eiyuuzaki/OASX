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
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.ErrorsApi
import com.oas.oasx.mobile.ui.components.OasBody
import com.oas.oasx.mobile.ui.components.OasCaption
import com.oas.oasx.mobile.ui.components.OasCard
import com.oas.oasx.mobile.ui.components.OasError
import com.oas.oasx.mobile.ui.components.OasPageTitle
import com.oas.oasx.mobile.ui.components.OasStateBlock
import com.oas.oasx.mobile.ui.theme.Spacing
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/// 错误记录：列表 + 详情（日志全文 + 截图）。
@Composable
fun ErrorsPage(onBack: () -> Unit) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    var scopeAll by remember { mutableIntStateOf(0) }
    var items by remember { mutableStateOf<List<ErrorsApi.Item>?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    var detail by remember { mutableStateOf<ErrorsApi.Detail?>(null) }
    var openId by remember { mutableStateOf<String?>(null) }
    var loading by remember { mutableStateOf(false) }
    var reload by remember { mutableIntStateOf(0) }

    LaunchedEffect(scopeAll, reload, acct.current?.config) {
        loading = true
        error = null
        items = null
        try {
            val script = if (scopeAll == 1) acct.current?.config else null
            items = withContext(Dispatchers.IO) {
                ErrorsApi.list(scriptName = script).items
            }
        } catch (e: Exception) {
            error = e.message?.take(120)
        }
        loading = false
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

    // 详情视图
    if (openId != null) {
        Column(
            Modifier.fillMaxSize().padding(horizontal = Spacing.md, vertical = Spacing.sm),
            verticalArrangement = Arrangement.spacedBy(Spacing.sm),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                TextButton(onClick = { openId = null; detail = null }) {
                    Text("返回", style = MaterialTheme.typography.labelLarge)
                }
                OasPageTitle("错误详情", Modifier.weight(1f))
            }
            val d = detail
            if (d == null) {
                OasStateBlock(
                    loading = error == null,
                    error = error,
                    emptyText = null,
                )
                return@Column
            }
            OasBody((d.scriptName ?: "未知脚本") + " · " + d.time.replace('T', ' ').take(19))
            OasCaption("${d.log?.fileName ?: "log.txt"} · " +
                "${(d.log?.content?.length ?: 0)} 字符 · ${d.images.size} 张截图")

            if (d.images.isNotEmpty()) {
                LazyColumn(
                    Modifier.fillMaxWidth().heightIn(max = 280.dp),
                    verticalArrangement = Arrangement.spacedBy(Spacing.sm),
                ) {
                    items(d.images) { img ->
                        RemoteImage(
                            url = ErrorsApi.imageUrl(d.id, img.name),
                            modifier = Modifier.fillMaxWidth().heightIn(max = 220.dp),
                        )
                    }
                }
            }

            OasCard(Modifier.fillMaxSize()) {
                Column(Modifier.verticalScroll(rememberScrollState())) {
                    Text(
                        d.log?.content ?: "（无内容）",
                        style = MaterialTheme.typography.bodySmall,
                        fontFamily = FontFamily.Monospace,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
        }
        return
    }

    // 列表视图
    Column(
        Modifier.fillMaxSize().padding(horizontal = Spacing.md, vertical = Spacing.sm),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            TextButton(onClick = onBack) {
                Text("返回", style = MaterialTheme.typography.labelLarge)
            }
            OasPageTitle("错误记录", Modifier.weight(1f))
            TextButton(onClick = { reload++ }) {
                Text("刷新", style = MaterialTheme.typography.labelLarge)
            }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(Spacing.sm)) {
            FilterChip(
                selected = scopeAll == 0,
                onClick = { scopeAll = 0 },
                label = { Text("全部") },
            )
            FilterChip(
                selected = scopeAll == 1,
                onClick = { scopeAll = 1 },
                label = { Text("当前账号") },
            )
        }

        error?.let { OasError(it) }

        when {
            loading -> OasStateBlock(loading = true, error = null)
            items.isNullOrEmpty() -> OasStateBlock(
                loading = false,
                error = null,
                emptyText = "没有错误记录（运行异常时后端会自动归档）",
            )
            else -> LazyColumn(verticalArrangement = Arrangement.spacedBy(Spacing.sm)) {
                items(items ?: emptyList()) { it ->
                    OasCard(
                        modifier = Modifier.clickable { openId = it.id },
                    ) {
                        OasBody(it.time.replace('T', ' ').take(19))
                        OasCaption(buildString {
                            append(it.scriptName ?: "旧格式")
                            append(" · ")
                            append(fmt(it.logSize))
                            append(" 字节")
                            if (it.imageCount > 0) append(" · ${it.imageCount} 图")
                        })
                    }
                }
            }
        }
    }
}

/// 远程图片（错误截图）。
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
        failed -> OasError("截图加载失败")
        else -> OasCaption("加载中…")
    }
}