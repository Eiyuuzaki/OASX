package com.oas.oasx.mobile.data

import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener

/// 调度器实时状态通道（WS /ws/{script}）。
/// 消息两类：{"state":"..."} 与 {"schedule":{running,pending,waiting}}。
/// 断线自动重连（500ms 起指数退避，上限 10s）。
class SchedulerSocket {

    companion object {
        /// 全局共享：避免每次进入首页新建 OkHttp 线程池。
        private val client = OkHttpClient()
    }

    data class Snapshot(val state: String?, val schedule: ScheduleData?)

    private val json = Json { ignoreUnknownKeys = true }

    /// 连接并持续吐状态快照；收集方取消（页面离开）即断开。
    fun connect(script: String, baseUrl: String): Flow<Snapshot> = callbackFlow {
        val wsUrl = baseUrl.trimEnd('/')
            .replaceFirst("http://", "ws://")
            .replaceFirst("https://", "wss://") + "/ws/$script"
        var backoff = 500L
        var closed = false
        var currentWs: WebSocket? = null

        // 重连动作由变量持有（dial 与 reconnect 互相引用，局部函数无法前置）
        var reconnect: () -> Unit = {}

        fun dial() {
            if (closed) return
            val req = Request.Builder().url(wsUrl).build()
            client.newWebSocket(req, object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    currentWs = webSocket
                }
                override fun onMessage(webSocket: WebSocket, text: String) {
                    try {
                        var state: String? = null
                        var schedule: ScheduleData? = null
                        val obj: JsonObject = json.parseToJsonElement(text).jsonObject
                        obj["state"]?.let { state = it.toString().trim('"') }
                        obj["schedule"]?.let {
                            schedule = json.decodeFromJsonElement(ScheduleData.serializer(), it)
                        }
                        trySend(Snapshot(state, schedule))
                    } catch (e: Exception) {
                        // 单条消息解析失败不中断连接
                    }
                }

                override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                    reconnect()
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    reconnect()
                }
            })
        }

        reconnect = {
            if (!closed) {
                launch {
                    delay(backoff)
                    if (!closed) {
                        backoff = (backoff * 2).coerceAtMost(10_000L)
                        dial()
                    }
                }
            }
        }

        dial()
        awaitClose {
            closed = true
            // 收集方离开页面：关闭 WS，避免连接泄漏
            try { currentWs?.close(1000, "leave") } catch (e: Exception) {}
        }
    }
}