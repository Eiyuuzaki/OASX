package com.oas.oasx.mobile.data

import com.oas.oasx.mobile.net.Api
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/// 错误记录（后端 log/error 目录归档）。
///
/// GET /logs/errors           → 列表（支持 date / script 过滤、cursor 分页）
/// GET /logs/errors/{id}      → 详情（log.txt 全文 + PNG 截图元信息）
/// GET /logs/errors/{id}/images/{name} → 单张截图字节
object ErrorsApi {
    @Serializable
    data class Item(
        val id: String = "",
        val directory: String = "",
        @SerialName("script_name") val scriptName: String? = null,
        @SerialName("timestamp_ms") val timestampMs: Long = 0,
        val time: String = "",
        val legacy: Boolean = false,
        @SerialName("log_size") val logSize: Long = 0,
        @SerialName("image_count") val imageCount: Int = 0,
    )

    @Serializable
    data class ListResp(
        val date: String? = null,
        @SerialName("script_name") val scriptName: String? = null,
        val items: List<Item> = emptyList(),
        @SerialName("next_cursor") val nextCursor: String? = null,
        @SerialName("has_more") val hasMore: Boolean = false,
    )

    @Serializable
    data class LogText(
        @SerialName("file_name") val fileName: String = "",
        val content: String = "",
    )

    @Serializable
    data class ImageInfo(
        val name: String = "",
        @SerialName("byte_size") val byteSize: Long = 0,
        val width: Int = 0,
        val height: Int = 0,
    )

    @Serializable
    data class Detail(
        val id: String = "",
        val directory: String = "",
        @SerialName("script_name") val scriptName: String? = null,
        @SerialName("timestamp_ms") val timestampMs: Long = 0,
        val time: String = "",
        val legacy: Boolean = false,
        val log: LogText? = null,
        val images: List<ImageInfo> = emptyList(),
    )

    suspend fun list(
        date: String? = null,
        scriptName: String? = null,
        limit: Int = 30,
    ): ListResp {
        val q = buildString {
            append("?limit=").append(limit)
            date?.takeIf { it.isNotBlank() }?.let { append("&date=").append(enc(it)) }
            scriptName?.takeIf { it.isNotBlank() }?.let {
                append("&script_name=").append(enc(it))
            }
        }
        return Api.getJsonOrThrow("/logs/errors$q")
    }

    suspend fun detail(id: String): Detail =
        Api.getJsonOrThrow("/logs/errors/" + enc(id))

    /// 截图的下载地址（交给图片加载库直接用）。
    fun imageUrl(id: String, name: String): String =
        Api.baseUrl.trimEnd('/') + "/logs/errors/" + enc(id) + "/images/" + enc(name)

    private fun enc(s: String): String = java.net.URLEncoder.encode(s, "UTF-8")
}

/// 资源采集控制（后端 ResourceCollector 的启停与状态）。
///
/// GET  /resource/{config}/status  → {config_name, running}
/// POST /resource/{config}/collect → 立即采集一次
/// POST /resource/{config}/start   → 启动周期采集
/// POST /resource/{config}/stop    → 停止周期采集
object CollectApi {
    @Serializable
    data class Status(
        @SerialName("config_name") val configName: String = "",
        val running: Boolean = false,
    )

    @Serializable
    data class Simple(
        val success: Boolean = false,
        val error: String? = null,
        val message: String? = null,
        @SerialName("config_name") val configName: String? = null,
        val running: Boolean? = null,
    )

    suspend fun status(config: String): Status =
        Api.getJsonOrThrow("/resource/" + enc(config) + "/status")

    suspend fun collectNow(config: String): Simple =
        Api.postJsonOrThrow("/resource/" + enc(config) + "/collect")

    suspend fun start(config: String): Status =
        Api.postJsonOrThrow("/resource/" + enc(config) + "/start")

    suspend fun stop(config: String): Status =
        Api.postJsonOrThrow("/resource/" + enc(config) + "/stop")

    private fun enc(s: String): String = java.net.URLEncoder.encode(s, "UTF-8")
}