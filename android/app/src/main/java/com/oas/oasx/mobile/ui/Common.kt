package com.oas.oasx.mobile.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
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
        state = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
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
            verticalArrangement = androidx.compose.foundation.layout.Arrangement.Center,
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