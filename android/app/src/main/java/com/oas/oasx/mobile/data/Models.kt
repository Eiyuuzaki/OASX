package com.oas.oasx.mobile.data

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

// ==================== 调度器实时状态（WS /ws/{script}） ====================

@Serializable
data class QueueItem(
    val name: String = "",
    @SerialName("next_run") val nextRun: String = "",
)

@Serializable
data class ScheduleData(
    val running: QueueItem? = null,
    val pending: List<QueueItem> = emptyList(),
    val waiting: List<QueueItem> = emptyList(),
)

// ==================== 任务菜单（/script_menu） ====================

@Serializable
data class ScriptMenuGroup(
    val group: String = "",
    val tasks: List<ScriptMenuTask> = emptyList(),
)

@Serializable
data class ScriptMenuTask(
    val name: String = "",
    val enabled: Boolean? = null,
)

// ==================== 统计（/stats/{script}） ====================

@Serializable
data class BattleStats(
    val count: Int = 0,
    @SerialName("avg_duration_seconds") val avgDurationSeconds: Double = 0.0,
)

@Serializable
data class TaskRunStats(
    @SerialName("start_time") val startTime: String = "",
    @SerialName("end_time") val endTime: String = "",
    @SerialName("duration_seconds") val durationSeconds: Double = 0.0,
)

@Serializable
data class TaskStats(
    @SerialName("run_count") val runCount: Int = 0,
    @SerialName("total_duration_seconds") val totalDurationSeconds: Double = 0.0,
    val runs: List<TaskRunStats> = emptyList(),
)

@Serializable
data class StatsResponse(
    @SerialName("script_name") val scriptName: String = "",
    @SerialName("total_runtime_seconds") val totalRuntimeSeconds: Double = 0.0,
    @SerialName("total_task_run_count") val totalTaskRunCount: Int = 0,
    @SerialName("total_battle_count") val totalBattleCount: Int = 0,
    val tasks: Map<String, TaskStats> = emptyMap(),
)

@Serializable
data class StatsDatesResponse(
    @SerialName("script_name") val scriptName: String = "",
    val dates: List<String> = emptyList(),
)

// ==================== 账号（/role/current + /config_list） ====================

@Serializable
data class RoleCurrent(
    @SerialName("avatar_id") val avatarId: String = "",
    @SerialName("server_id") val serverId: String = "",
    @SerialName("short_id") val shortId: String = "",
    val name: String = "",
    val level: Int = 0,
    @SerialName("server_show_name") val serverShowName: String = "",
    @SerialName("role_key") val roleKey: String = "",
)