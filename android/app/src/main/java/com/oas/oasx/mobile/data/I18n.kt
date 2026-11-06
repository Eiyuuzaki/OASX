package com.oas.oasx.mobile.data

import com.oas.oasx.mobile.net.Api
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

/// 任务名/界面名中文映射。
///
/// 数据源：后端 `GET /home/task_name_translate`（读 module/config/i18n/zh-CN.json，
/// 权威最新）。与 WPF I18nMenu 同源，但不在客户端硬编码字典——避免后端新增
/// 任务后客户端跟不上。
///
/// 未命中的键回退原文（与 WPF/Flutter 行为一致）。
object I18n {
    private val _map = MutableStateFlow<Map<String, String>>(emptyMap())
    val map: StateFlow<Map<String, String>> get() = _map

    @Volatile
    private var loaded = false

    /// 拉一次并缓存；失败保持空表（调用方回退原文），不阻塞界面。
    suspend fun ensureLoaded() {
        if (loaded) return
        try {
            val m = Api.getJsonOrThrow<Map<String, String>>("/home/task_name_translate")
            if (m.isNotEmpty()) {
                _map.value = m
                loaded = true
            }
        } catch (e: Exception) {
            // 静默失败：翻译缺失不应影响功能
        }
    }

    /// 同步翻译（已加载时用）。
    fun t(key: String): String = _map.value[key] ?: key
}