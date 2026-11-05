package com.oas.oasx.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Event
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.AccountBalanceWallet
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import com.oas.oasx.mobile.ui.ActivityPage
import com.oas.oasx.mobile.ui.AssetsPage
import com.oas.oasx.mobile.ui.DashboardPage
import com.oas.oasx.mobile.ui.LogsPage
import com.oas.oasx.mobile.ui.SettingsPage

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val prefs = getSharedPreferences("oasx", MODE_PRIVATE)
        setContent {
            MaterialTheme {
                Surface(Modifier.fillMaxSize()) {
                    OasxApp(prefs)
                }
            }
        }
    }
}

@Composable
fun OasxApp(prefs: android.content.SharedPreferences) {
    var backend by remember { mutableStateOf(prefs.getString("backend", "") ?: "") }
    var tab by remember { mutableIntStateOf(0) }
    // 未配置后端地址：强制先走设置页（连接是其它一切的前提）
    val effectiveTab = if (backend.isBlank()) 4 else tab

    val tabs = listOf(
        Triple("仪表盘", Icons.Filled.Home, 0),
        Triple("活动", Icons.Filled.Event, 1),
        Triple("资产", Icons.Filled.AccountBalanceWallet, 2),
        Triple("日志", Icons.Filled.Description, 3),
        Triple("设置", Icons.Filled.Settings, 4),
    )

    Scaffold(
        bottomBar = {
            NavigationBar {
                tabs.forEach { (label, icon, index) ->
                    NavigationBarItem(
                        selected = effectiveTab == index,
                        onClick = {
                            if (backend.isBlank() && index != 4) {
                                tab = 4
                            } else {
                                tab = index
                            }
                        },
                        icon = { Icon(icon, contentDescription = label) },
                        label = { Text(label) },
                    )
                }
            }
        },
    ) { pad ->
        val context = LocalContext.current
        Surface(Modifier.fillMaxSize().padding(pad)) {
            when (effectiveTab) {
                0 -> DashboardPage(backend)
                1 -> ActivityPage(backend)
                2 -> AssetsPage(backend)
                3 -> LogsPage()
                else -> SettingsPage(prefs) { newUrl ->
                    backend = newUrl
                    tab = 0
                }
            }
        }
    }
}