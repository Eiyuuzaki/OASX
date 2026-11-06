package com.oas.oasx.mobile.data

import com.oas.oasx.mobile.net.Api
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.sse.EventSource
import okhttp3.sse.EventSourceListener
import okhttp3.sse.EventSources

/// 实时日志 SSE（/log/{script}/stream）。
/// 事件：ready / heartbeat / lines（{"lines": [...]}) / closed。
/// 断线自动重连（500ms 起指数退避，上限 5s）。
class LogStream {
    sealed class Event {
        data object Ready : Event()
        data class Lines(val lines: List<String>) : Event()
        data object Closed : Event()
    }

    private val json = Json { ignoreUnknownKeys = true }

    fun connect(script: String, baseUrl: String): Flow<Event> = callbackFlow {
        val url = baseUrl.trimEnd('/') + "/log/" +
            java.net.URLEncoder.encode(script, "UTF-8") + "/stream"
        var backoff = 500L
        var closed = false
        var es: EventSource? = null
        var reconnect: () -> Unit = {}

        fun dial() {
            if (closed) return
            val req = Request.Builder().url(url).build()
            es = EventSources.createFactory(Api.client).newEventSource(
                req,
                object : EventSourceListener() {
                    override fun onOpen(eventSource: EventSource, response: Response) {
                        backoff = 500L
                        trySend(Event.Ready)
                    }

                    override fun onEvent(
                        eventSource: EventSource,
                        id: String?, type: String?, data: String,
                    ) {
                        try {
                            when (type) {
                                "ready" -> trySend(Event.Ready)
                                "closed" -> trySend(Event.Closed)
                                "lines" -> {
                                    val arr = json.parseToJsonElement(data)
                                        .jsonObject["lines"]?.jsonArray ?: return
                                    val lines = arr.mapNotNull { e ->
                                        e.toString().trim('"').takeIf { it.isNotEmpty() }
                                    }
                                    if (lines.isNotEmpty()) trySend(Event.Lines(lines))
                                }
                            }
                        } catch (e: Exception) {
                            // 单条解析失败不中断流
                        }
                    }

                    override fun onFailure(
                        eventSource: EventSource, t: Throwable?, response: Response?,
                    ) {
                        if (!closed) reconnect()
                    }

                    override fun onClosed(eventSource: EventSource) {
                        if (!closed) reconnect()
                    }
                },
            )
        }

        reconnect = {
            if (!closed) {
                launch {
                    delay(backoff)
                    if (!closed) {
                        backoff = (backoff * 2).coerceAtMost(5_000L)
                        dial()
                    }
                }
            }
        }

        dial()
        awaitClose {
            closed = true
            try { es?.cancel() } catch (e: Exception) {}
        }
    }
}