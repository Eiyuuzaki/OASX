package com.oas.oasx.mobile.data

import com.oas.oasx.mobile.net.Api
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/// 全局账号上下文（与 WPF GlobalAccountContext 同口径）。
///
/// 账号 = (调度器 config, 角色 roleKey) 二元组；账号列表来自
/// `/resource/{config}/roles`（读存档，离线可用）。
///
/// ★ 唯一账号来源：所有页面都从这里取，页面不再各自拉角色列表——
///   否则多份加载互相覆盖状态，界面会卡在"读取中"。
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
        /// 稳定身份键：切号判断、数据缓存键都用它。
        val key: String get() = "$config|$roleKey"
    }

    data class State(
        val accounts: List<Account> = emptyList(),
        val current: Account? = null,
        val error: String? = null,
        val loading: Boolean = false,
        /// 至少成功加载过一次（用于区分"还没加载"和"加载后为空"）
        val loaded: Boolean = false,
    )

    private val _state = MutableStateFlow(State())
    val state: StateFlow<State> get() = _state

    fun select(account: Account) {
        Prefs.currentConfig = account.key
        _state.value = _state.value.copy(current = account)
    }

    /// 幂等加载：已加载过就不再重复拉取（除非 force）。
    /// 失败时写 error 并保留 loaded=false，供 UI 显示"重试"。
    suspend fun ensureLoaded(force: Boolean = false) {
        val st = _state.value
        if (st.loading) return
        if (st.loaded && !force) return
        refresh()
    }

    /// 后台刷新（供 UI 按钮调用，不阻塞 Compose）。
    fun refreshInBackground() {
        if (_state.value.loading) return
        kotlinx.coroutines.MainScope().launch { refresh() }
    }

    suspend fun refresh() {
        _state.value = _state.value.copy(loading = true, error = null)
        try {
            val configs = Api.configList()
            val pairs = coroutineScope {
                configs.map { config ->
                    async(Dispatchers.IO) {
                        try {
                            // config 可能是中文名，必须显式编码
                            val enc = java.net.URLEncoder.encode(config, "UTF-8")
                            val resp = Api.getJsonOrThrow<RolesResponse>(
                                "/resource/$enc/roles",
                            )
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
            val current = pairs.firstOrNull { it.key == saved } ?: pairs.firstOrNull()
            if (current != null) Prefs.currentConfig = current.key
            _state.value = State(
                accounts = pairs,
                current = current,
                error = null,
                loading = false,
                loaded = true,
            )
        } catch (e: Exception) {
            _state.value = _state.value.copy(
                loading = false,
                loaded = false,
                error = e.message?.take(120) ?: "账号加载失败",
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