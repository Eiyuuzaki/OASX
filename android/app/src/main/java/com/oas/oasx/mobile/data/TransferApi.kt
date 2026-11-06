package com.oas.oasx.mobile.data

import com.oas.oasx.mobile.net.Api
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/// 配置导入导出（对齐 WPF OasTransferApi）。
///
/// 后端约定（实测）：
///   GET  /config/transfer/capabilities
///   GET  /config/export?name=&mode=
///   GET  /config/task/export?config_name=&task_name=&mode=
///   POST /config/import           → {name, task_name, payload}
///   POST /config/task/import      → {config_name, task_name, payload}
object TransferApi {
    @Serializable
    data class Capabilities(
        val version: Int = 0,
        val modes: List<String> = emptyList(),
        @SerialName("config_import") val configImport: Boolean = false,
        @SerialName("task_import") val taskImport: Boolean = false,
        @SerialName("max_bytes") val maxBytes: Long = 0,
    )

    @Serializable
    data class ImportResult(
        val name: String? = null,
        val warnings: List<String> = emptyList(),
        val detail: String? = null,
    )

    suspend fun capabilities(): Capabilities =
        Api.getJsonOrThrow("/config/transfer/capabilities")

    /// 导出整份配置 → 原始 JSON 文本（可直接写文件）。
    suspend fun exportConfig(name: String, mode: String = "backup"): String =
        Api.get("/config/export?name=" + enc(name) + "&mode=" + enc(mode))

    /// 导出单任务参数。
    suspend fun exportTask(config: String, task: String, mode: String = "backup"): String =
        Api.get(
            "/config/task/export?config_name=" + enc(config) +
                "&task_name=" + enc(task) + "&mode=" + enc(mode),
        )

    /// 导入整份配置（payload 为原始 JSON 文本；只新建不覆盖）。
    suspend fun importConfig(rawJson: String, newName: String = ""): ImportResult =
        Api.postJsonWithBodyOrThrow(
            "/config/import",
            buildImportBody(name = newName, taskName = "", raw = rawJson),
        )

    /// 导入单任务参数到现有配置。
    suspend fun importTask(config: String, taskName: String, rawJson: String): ImportResult =
        Api.postJsonWithBodyOrThrow(
            "/config/task/import",
            buildImportBody(configName = config, name = "", taskName = taskName, raw = rawJson),
        )

    /// 构造导入请求体：{name|config_name, task_name, payload}
    /// payload 直接内嵌原始 JSON（不二次转义）。
    private fun buildImportBody(
        configName: String? = null,
        name: String = "",
        taskName: String = "",
        raw: String,
    ): String = buildString {
        append("{")
        if (configName != null) {
            append("\"config_name\":\"").append(esc(configName)).append("\",")
        } else {
            append("\"name\":\"").append(esc(name)).append("\",")
        }
        append("\"task_name\":\"").append(esc(taskName)).append("\",")
        append("\"payload\":").append(raw)
        append("}")
    }

    private fun esc(s: String): String = s.replace("\\", "\\\\").replace("\"", "\\\"")

    private fun enc(s: String): String = java.net.URLEncoder.encode(s, "UTF-8")
}