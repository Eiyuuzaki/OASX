package com.oas.oasx.mobile.data

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement

/// 通知推送（复用 OAS 的 onepush）。
/// 后端把 provider + params 组装成 YAML 存进脚本配置；
/// 读回来是 YAML 文本，这里做最小解析用于回填表单（对齐 WPF ParseYaml）。
object NotifyApi {
    @Serializable
    data class ProvidersResp(
        val success: Boolean = false,
        val providers: Map<String, Provider> = emptyMap(),
    )

    @Serializable
    data class Provider(
        val required: List<String> = emptyList(),
        val optional: List<String> = emptyList(),
    )

    @Serializable
    data class ConfigResp(
        val success: Boolean = false,
        @SerialName("notify_enable") val notifyEnable: Boolean = false,
        @SerialName("notify_config") val notifyConfig: String = "",
        @SerialName("cycle_hours") val cycleHours: Int = 6,
    )

    /// 保存请求体（后端据此生成 YAML）。
    @Serializable
    data class SaveBody(
        @SerialName("notify_enable") val notifyEnable: Boolean,
        val provider: String,
        val params: Map<String, String>,
        @SerialName("cycle_hours") val cycleHours: Int,
    )

    @Serializable
    data class SimpleResp(
        val success: Boolean = false,
        val error: String? = null,
        val message: String? = null,
    )

    /// 解析后端返回的 YAML：第一行 provider，其余 key: value。
    /// 值可能带引号（yaml.safe_dump 行为），此处剥离。
    fun parseYaml(text: String): Pair<String, Map<String, String>> {
        var provider = "null"
        val params = LinkedHashMap<String, String>()
        text.lineSequence().forEach { rawLine ->
            val line = rawLine.trim()
            if (line.isEmpty() || line.startsWith("#")) return@forEach
            val idx = line.indexOf(':')
            if (idx <= 0) return@forEach
            val key = line.substring(0, idx).trim()
            var value = line.substring(idx + 1).trim()
            if (value.length >= 2) {
                val f = value.first()
                val l = value.last()
                if ((f == '\'' && l == '\'') || (f == '"' && l == '"')) {
                    value = value.substring(1, value.length - 1)
                }
            }
            if (key == "provider") {
                provider = value
            } else if (value.isNotEmpty()) {
                params[key] = value
            }
        }
        return provider to params
    }
}