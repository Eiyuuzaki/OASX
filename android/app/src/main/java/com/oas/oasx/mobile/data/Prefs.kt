package com.oas.oasx.mobile.data

import android.content.Context
import android.content.SharedPreferences

/// 轻量偏好：后端地址、主题模式、当前账号。
/// v1 使用 SharedPreferences（键值都是字符串）；引入 DataStore 待后续增量。
object Prefs {
    private lateinit var prefs: SharedPreferences

    fun init(context: Context) {
        if (!::prefs.isInitialized) {
            prefs = context.getSharedPreferences("oasx", Context.MODE_PRIVATE)
        }
    }

    /// init 之后可随时取用（MainActivity onCreate 先 init）。
    fun get(): SharedPreferences = prefs

    var backend: String
        get() = prefs.getString("backend", "") ?: ""
        set(value) = prefs.edit().putString("backend", value).apply()

    var themeMode: String
        get() = prefs.getString("themeMode", "system") ?: "system"
        set(value) = prefs.edit().putString("themeMode", value).apply()

    /// 当前账号（config 名 + role_key），跨启动记忆。
    var currentConfig: String?
        get() = prefs.getString("currentConfig", null)
        set(value) = prefs.edit().putString("currentConfig", value).apply()
}