package com.oas.oasx.mobile.net

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/// 只声明用到的字段；ignoreUnknownKeys 兜底其余。
/// 字段名与后端 daily_task_reader 的返回逐一对齐（snake_case）。
@Serializable
data class ActivityBoard(
    val success: Boolean = false,
    val error: String? = null,
    val hint: String? = null,
    @SerialName("config_name") val configName: String = "",
    // 每日任务
    val daily: List<DailyRow> = emptyList(),
    @SerialName("daily_done") val dailyDone: Int = 0,
    @SerialName("daily_total") val dailyTotal: Int = 0,
    // 悬赏封印
    val monster: List<MonsterRow> = emptyList(),
    @SerialName("monster_done") val monsterDone: Int = 0,
    // 活动：activity = 活动面板任务名；current_activities = 参与的限时活动
    val activity: List<String> = emptyList(),
    @SerialName("activity_available") val activityAvailable: Boolean = false,
    @SerialName("current_activities") val activities: List<CurAct> = emptyList(),
    @SerialName("live_windows") val live: List<LiveW> = emptyList(),
    // 服务器时间
    @SerialName("server_week_day") val serverWeekDay: Int = 0,
    @SerialName("server_time_text") val serverTimeText: String = "",
    // 周常信号 / 阴阳寮（WPF 同源字段，当前页面按需展示）
    @SerialName("weekly_signals") val weeklySignals: WeeklySignals? = null,
    val guild: GuildInfo? = null,
    @SerialName("live_role") val liveRole: LiveRole? = null,
)

@Serializable
data class DailyRow(
    val id: Int = 0,
    val name: String = "",
    val title: String = "",
    val cur: Int = 0,
    val max: Int = 0,
    val score: Int = 0,
    val done: Boolean = false,
    @SerialName("progress_text") val progressText: String = "",
)

@Serializable
data class MonsterRow(
    val id: String = "",
    val fin: Boolean = false,
    val process: Int = 0,
    val need: Int = 0,
    val type: Int? = null,
)

@Serializable
data class CurAct(
    val id: String = "",
    val name: String = "",
    val begin: String = "",
    val end: String = "",
)

@Serializable
data class LiveW(
    val key: String = "",
    val end: Long = 0,
    @SerialName("end_text") val endText: String = "",
)

/// 周常反推信号（客户端展示依据用）。
@Serializable
data class WeeklySignals(
    @SerialName("pvp_score") val pvpScore: Int = 0,
    @SerialName("pvp_rank") val pvpRank: Int = 0,
    @SerialName("last_week_pvp_stage") val lastWeekPvpStage: Int = 0,
    @SerialName("season_daily_win") val seasonDailyWin: Int = 0,
    @SerialName("coop_play_times") val coopPlayTimes: Int = 0,
    @SerialName("coop_join_times") val coopJoinTimes: Int = 0,
    @SerialName("coop_total_win") val coopTotalWin: Int = 0,
    @SerialName("dojo_win") val dojoWin: Int = 0,
    @SerialName("dojo_lose") val dojoLose: Int = 0,
    @SerialName("dojo_score") val dojoScore: Int = 0,
    @SerialName("jiejie_cards") val jiejieCards: Int = 0,
)

@Serializable
data class GuildInfo(
    val name: String = "",
    val lv: Int? = null,
    val duty: Int? = null,
    @SerialName("gym_state") val gymState: Int? = null,
    @SerialName("gym_left_challenge") val gymLeftChallenge: Int? = null,
)

/// 活动数据的真实归属角色（内存桥读到谁就是谁）。
@Serializable
data class LiveRole(
    @SerialName("avatar_id") val avatarId: String = "",
    @SerialName("server_id") val serverId: String = "",
    val name: String = "",
    val level: Int = 0,
    @SerialName("server_show_name") val serverShowName: String = "",
    @SerialName("role_key") val roleKey: String = "",
)

@Serializable
data class FosterResp(
    val success: Boolean = false,
    val error: String? = null,
    val foster: Slot? = null,
    val own: Slot? = null,
)

@Serializable
data class Slot(
    val active: Boolean? = null,
    @SerialName("start_text") val startText: String? = null,
    @SerialName("end_text") val endText: String? = null,
    val hours: Int? = null,
    @SerialName("card_item") val cardItem: String? = null,
    @SerialName("card_name") val cardName: String? = null,
    val remaining: Int? = null,
    val empty: Boolean? = null,
    val unknown: Boolean? = null,
)

@Serializable
data class LedgerResp(
    @SerialName("first_date") val firstDate: String = "",
    @SerialName("last_date") val lastDate: String = "",
    @SerialName("base_date") val baseDate: String = "",
    val items: List<LedgerItem> = emptyList(),
)

@Serializable
data class LedgerItem(
    val id: String = "",
    val name: String = "",
    val icon: String = "",
    val count: Long = 0,
    @SerialName("first_count") val firstCount: Long = 0,
    val delta: Long? = null,
)

// ==================== 资产：单日快照 / 日期列表 ====================

@Serializable
data class ResourceDay(
    @SerialName("config_name") val configName: String = "",
    @SerialName("role_key") val roleKey: String = "",
    val date: String = "",
    @SerialName("updated_at") val updatedAt: String = "",
    val items: List<ResourceItem> = emptyList(),
    @SerialName("sample_count") val sampleCount: Int = 0,
)

@Serializable
data class ResourceItem(
    val id: String = "",
    val name: String = "",
    val icon: String = "",
    val count: Long = 0,
    val delta: Long? = null,
    @SerialName("delta_from") val deltaFrom: String = "",
)

@Serializable
data class ResourceDatesResp(
    @SerialName("config_name") val configName: String = "",
    @SerialName("role_key") val roleKey: String = "",
    val dates: List<String> = emptyList(),
)
