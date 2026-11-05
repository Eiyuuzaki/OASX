package com.oas.oasx.mobile.net

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/// 只声明用到的字段；ignoreUnknownKeys 兜底其余。
@Serializable
data class ActivityBoard(
    val success: Boolean = false,
    val error: String? = null,
    @SerialName("daily_done") val dailyDone: Int = 0,
    @SerialName("daily_total") val dailyTotal: Int = 0,
    @SerialName("monster_done") val monsterDone: Int = 0,
    @SerialName("monster") val monster: List<MonsterRow> = emptyList(),
    @SerialName("current_activities") val activities: List<CurAct> = emptyList(),
    @SerialName("live_windows") val live: List<LiveW> = emptyList(),
)

@Serializable
data class MonsterRow(
    val name: String = "",
    val fin: Boolean = false,
)

@Serializable
data class CurAct(
    val name: String = "",
    val end: String = "",
)

@Serializable
data class LiveW(
    val key: String = "",
    @SerialName("end_text") val endText: String = "",
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
    val name: String = "",
    val count: Long = 0,
    val delta: Long? = null,
)