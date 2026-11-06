package com.oas.oasx.mobile.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.net.Api
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

@Serializable
data class DeviceCurrent(
    @SerialName("config_name") val configName: String = "",
    val serial: String = "",
    @SerialName("emulator_name") val emulatorName: String? = null,
    @SerialName("emulator_type") val emulatorType: String? = null,
    val online: Boolean = false,
)

@Serializable
data class DeviceInstance(
    @SerialName("instance_name") val instanceName: String = "",
    @SerialName("display_name") val displayName: String = "",
    val serial: String = "",
    @SerialName("adb_port") val adbPort: Int = 0,
    val online: Boolean = false,
    val model: String = "",
    val brand: String = "",
    val root: Boolean = false,
    val emulator: String = "",
)

@Serializable
data class DeviceInstances(
    val instances: List<DeviceInstance> = emptyList(),
    @SerialName("adb_devices") val adbDevices: List<String> = emptyList(),
)

@Serializable
private data class SwitchBody(
    @SerialName("config_name") val configName: String,
    val serial: String,
)

/// 模拟器设备选择：当前 serial + 可用实例列表，切换后写回该调度器配置。
/// 只改配置，不重启正在跑的脚本（后端行为），下次启动生效。
@Composable
fun DevicePage(onBack: () -> Unit) {
    val acct by AccountContext.state.collectAsStateWithLifecycle()
    val config = acct.current?.config

    var current by remember { mutableStateOf<DeviceCurrent?>(null) }
    var instances by remember { mutableStateOf<List<DeviceInstance>>(emptyList()) }
    var adbDevices by remember { mutableStateOf<List<String>>(emptyList()) }
    var status by remember { mutableStateOf("") }
    var reload by remember { mutableStateOf(0) }

    LaunchedEffect(config, reload) {
        val cfg = config ?: return@LaunchedEffect
        status = ""
        try {
            current = Api.getJsonOrThrow<DeviceCurrent>("/device/current?config_name=$cfg")
        } catch (e: Exception) {
            status = "读取当前设备失败：${e.message?.take(80)}"
        }
        try {
            val ins = Api.getJsonOrThrow<DeviceInstances>("/device/instances")
            instances = ins.instances
            adbDevices = ins.adbDevices
        } catch (e: Exception) {
            if (status.isEmpty()) status = "读取实例列表失败：${e.message?.take(80)}"
        }
    }

    Column(
        Modifier.fillMaxSize().padding(16.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Text(
            "← 返回",
            Modifier.clickable { onBack() },
            fontSize = 14.sp,
            color = MaterialTheme.colorScheme.primary,
        )
        Text("模拟器", fontSize = 22.sp)
        Text(
            "调度器 ${config ?: "—"} · 切换只改配置，下次启动脚本生效",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        val cur = current
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp)) {
                Text("当前设备", fontSize = 13.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Text(cur?.serial ?: "—", fontSize = 17.sp)
                Text(
                    buildString {
                        cur?.emulatorName?.let { append(it) }
                        cur?.emulatorType?.let {
                            if (isNotEmpty()) append(" · ")
                            append(it)
                        }
                        if (isNotEmpty()) append(" · ")
                        append(if (cur?.online == true) "在线" else "离线")
                    },
                    fontSize = 11.sp,
                    color = if (cur?.online == true) MaterialTheme.colorScheme.primary
                            else MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }

        Text("可用实例", fontSize = 13.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
        if (instances.isEmpty()) {
            Text("未发现实例（需 MuMu 等模拟器已启动）", fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        instances.forEach { ins ->
            val isCurrent = cur?.serial == ins.serial
            Card(
                Modifier.fillMaxWidth().clickable {
                    val cfg = config ?: return@clickable
                    status = "切换中…"
                    Thread {
                        try {
                            val body = "{\"config_name\":\"$cfg\",\"serial\":\"${ins.serial}\"}"
                            Api.putJsonOrThrow<DeviceCurrent>("/device/current", body)
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "已切换到 ${ins.serial}（下次启动脚本生效）"
                                reload++
                            }
                        } catch (e: Exception) {
                            android.os.Handler(android.os.Looper.getMainLooper()).post {
                                status = "切换失败：${e.message?.take(80)}"
                            }
                        }
                    }.start()
                },
            ) {
                Column(Modifier.padding(14.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            ins.displayName.ifBlank { ins.instanceName },
                            Modifier.weight(1f),
                            fontSize = 15.sp,
                            color = if (isCurrent) MaterialTheme.colorScheme.primary
                                    else MaterialTheme.colorScheme.onSurface,
                        )
                        if (isCurrent) {
                            Text("当前", fontSize = 11.sp,
                                color = MaterialTheme.colorScheme.primary)
                        }
                    }
                    Text(
                        buildString {
                            append(ins.serial)
                            append(" · ")
                            append(if (ins.online) "在线" else "离线")
                            if (ins.root) append(" · root")
                            if (ins.model.isNotBlank()) append(" · ${ins.model}")
                        },
                        fontSize = 11.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }

        if (adbDevices.isNotEmpty()) {
            Text("其它 adb 设备", fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 6.dp))
            adbDevices.forEach { d ->
                Text("· $d", fontSize = 11.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }

        if (status.isNotEmpty()) {
            Text(status, fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}