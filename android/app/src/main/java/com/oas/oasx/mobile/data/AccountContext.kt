package com.oas.oasx.mobile.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.MainScope
import kotlinx.coroutines.launch
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import com.oas.oasx.mobile.net.Api
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/// 全局账号上下文（与 WPF GlobalAccountContext 同口径）。
/// 账号 = (config 调度器, roleKey 角色) 二元组；账号列表来自
/// /resource/{config}/roles（读存档，离线可用），当前角色加 /role/current 标记。
object AccountContext {
    @Serializable
    data class Account(
        val config: String,
        val roleKey: String,
        val name: String,
        val level: Int = 0,
        val serverName: String = "",
        val lastUpdated: String = "",
        val dates: List<String> = emptyList(),
    ) {
        val displayName: String get() = "$name Lv$level · $serverName"
    }

    data class State(
        val accounts: List<Account> = emptyList(),
        val current: Account? = null,
        val error: String? = null,
        val loading: Boolean = false,
    )

    private val _state = MutableStateFlow(State())
    val state: StateFlow<State> get() = _state

    fun select(account: Account) {
        Prefs.currentConfig = "${account.config}|${account.roleKey}"
        _state.value = _state.value.copy(current = account)
    }

    /// 后台刷新账号列表（供 UI 按钮调用，不阻塞 Compose）。
    fun refreshInBackground() {
        if (_state.value.loading) return
        MainScope().launch { refresh() }
    }

    /// 拉取全部调度器的角色，扁平化为账号列表。
    /// 单个调度器失败不影响其它（该调度器账号缺席并记入 error）。
    suspend fun refresh() {
        _state.value = _state.value.copy(loading = true)
        try {
            val configs = Api.configList()
            val pairs = coroutineScope {
                configs.map { config ->
                    async(Dispatchers.IO) {
                        try {
                            val resp = Api.getJsonOrThrow<RolesResponse>("/resource/$config/roles")
                            resp.roles.map { r ->
                                Account(
                                    config = config,
                                    roleKey = r.roleKey,
                                    name = r.name,
                                    level = r.level,
                                    serverName = r.serverName,
                                    lastUpdated = r.lastUpdated,
                                    dates = r.dates,
                                )
                            }
                        } catch (e: Exception) {
                            emptyList()
                        }
                    }
                }
            }.awaitAll().flatten()
            val saved = Prefs.currentConfig
            val current = pairs.firstOrNull { "${it.config}|${it.roleKey}" == saved }
                ?: pairs.firstOrNull()
            _state.value = State(accounts = pairs, current = current, loading = false)
        } catch (e: Exception) {
            _state.value = _state.value.copy(
                loading = false,
                error = e.message?.take(100) ?: "加载失败",
            )
        }
    }
}

@Serializable
data class RolesResponse(
    @SerialName("config_name") val configName: String = "",
    val roles: List<RoleEntry> = emptyList(),
)

@Serializable
data class RoleEntry(
    @SerialName("role_key") val roleKey: String = "",
    val name: String = "",
    val level: Int = 0,
    @SerialName("server_name") val serverName: String = "",
    @SerialName("short_id") val shortId: String = "",
    @SerialName("avatar_id") val avatarId: String = "",
    @SerialName("last_updated") val lastUpdated: String = "",
    val dates: List<String> = emptyList(),
)