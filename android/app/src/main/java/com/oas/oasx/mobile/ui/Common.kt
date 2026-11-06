package com.oas.oasx.mobile.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.oas.oasx.mobile.ui.components.OasError
import com.oas.oasx.mobile.ui.components.OasPageTitle
import com.oas.oasx.mobile.ui.components.OasSectionTitle
import com.oas.oasx.mobile.ui.theme.Spacing
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
        null -> CircularProgressIndicator(
            modifier = Modifier.padding(Spacing.lg),
            strokeWidth = 2.dp,
        )
        else -> s.fold(
            onFailure = { ErrorView(it.message ?: it.toString()) },
            onSuccess = { content(it) },
        )
    }
}

@Composable
fun ErrorView(msg: String) {
    Column(Modifier.fillMaxWidth().padding(Spacing.md)) {
        OasError(msg)
    }
}

@Composable
fun PageTitle(text: String) {
    OasPageTitle(text)
}

@Composable
fun SectionTitle(text: String) {
    OasSectionTitle(text)
}

fun fmt(n: Long): String = when {
    n >= 100_000_000L -> "%.1f亿".format(n / 100_000_000.0)
    n >= 10_000L -> "%.1f万".format(n / 10_000.0)
    else -> "%,d".format(n)
}