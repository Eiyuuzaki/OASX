package com.oas.oasx.mobile.data

import com.oas.oasx.mobile.net.Api
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/// 绘卷棋盘（区服维度，数据实时从运行中的游戏读取）。
///
/// GET /huijuan/{config} → {issue, threshold, scrolls[]}
/// 每个卷含 score/percent/self_score/end_text 等；
/// 活动期判定用 end_text（不能用"今天有采样"当判据）。
object HuijuanApi {
    @Serializable
    data class Scroll(
        val id: String = "",
        val score: Long = 0,
        val percent: Double = 0.0,
        @SerialName("self_score") val selfScore: Long = 0,
        @SerialName("self_rank") val selfRank: Int? = null,
        val finished: Boolean = false,
        @SerialName("finish_time_text") val finishTimeText: String = "",
        val name: String = "",
        val img: String = "",
        val remain: Long = 0,
        @SerialName("begin_text") val beginText: String = "",
        @SerialName("end_text") val endText: String = "",
    )

    @Serializable
    data class Board(
        val success: Boolean = false,
        @SerialName("config_name") val configName: String = "",
        val issue: Int = 0,
        val threshold: Long = 0,
        val scrolls: List<Scroll> = emptyList(),
        val error: String? = null,
    )

    suspend fun board(config: String): Board =
        Api.getJsonOrThrow("/huijuan/" + enc(config))

    /// 是否处于活动期：只要有一个卷的 end_text 晚于现在即算。
    /// 后端 end_text 形如 "2026-10-30 23:59"。
    fun isActive(board: Board, now: java.util.Date = java.util.Date()): Boolean =
        board.scrolls.any { sc ->
            val end = parseDate(sc.endText) ?: return@any false
            end.after(now)
        }

    private fun parseDate(text: String): java.util.Date? {
        val patterns = listOf(
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
        )
        for (p in patterns) {
            try {
                val fmt = java.text.SimpleDateFormat(p, java.util.Locale.US)
                fmt.isLenient = false
                return fmt.parse(text.trim())
            } catch (e: Exception) {
                // 换下一个格式
            }
        }
        return null
    }

    private fun enc(s: String): String = java.net.URLEncoder.encode(s, "UTF-8")
}