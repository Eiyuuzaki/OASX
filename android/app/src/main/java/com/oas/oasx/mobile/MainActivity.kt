package com.oas.oasx.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountBalanceWallet
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Task
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat
import androidx.lifecycle.lifecycleScope
import com.oas.oasx.mobile.data.AccountContext
import com.oas.oasx.mobile.data.Prefs
import com.oas.oasx.mobile.net.Api
import com.oas.oasx.mobile.ui.AssetsPage
import com.oas.oasx.mobile.ui.HomePage
import com.oas.oasx.mobile.ui.RecordsPage
import com.oas.oasx.mobile.ui.SettingsPage
import com.oas.oasx.mobile.ui.TasksPage
import com.oas.oasx.mobile.ui.theme.OasxTheme
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        Prefs.init(this)
        Api.baseUrl = Prefs.backend

        setContent {
            var themeMode by remember { mutableStateOf(Prefs.themeMode) }
            var dynamicColor by remember { mutableStateOf(Prefs.dynamicColor) }
            var backend by remember { mutableStateOf(Prefs.backend) }
            val dark = when (themeMode) {
                "light" -> false
                "dark" -> true
                else -> isSystemInDarkTheme()
            }
            OasxTheme(darkTheme = dark, dynamicColor = dynamicColor) {
                // 状态栏图标明暗跟随主题
                val view = LocalView.current
                SideEffect {
                    val window = (view.context as? ComponentActivity)?.window ?: return@SideEffect
                    WindowCompat.getInsetsController(window, view).apply {
                        isAppearanceLightStatusBars = !dark
                        isAppearanceLightNavigationBars = !dark
                    }
                }
                Surface(
                    Modifier.fillMaxSize(),
                    color = androidx.compose.material3.MaterialTheme.colorScheme.surface,
                ) {
                    OasxApp(
                        backend = backend,
                        onThemeChanged = { themeMode = it },
                        onDynamicColorChanged = { dynamicColor = it },
                    ) { newBackend ->
                        backend = newBackend
                        Api.baseUrl = newBackend
                        lifecycleScope.launch { AccountContext.ensureLoaded(force = true) }
                    }
                    androidx.compose.runtime.LaunchedEffect(Unit) {
                        if (backend.isNotEmpty()) AccountContext.ensureLoaded()
                    }
                }
            }
        }
    }
}

@Composable
fun OasxApp(
    backend: String,
    onThemeChanged: (String) -> Unit,
    onDynamicColorChanged: (Boolean) -> Unit,
    onBackendSaved: (String) -> Unit,
) {
    val tabs = listOf(
        Triple("首页", Icons.Filled.Home, 0),
        Triple("资产", Icons.Filled.AccountBalanceWallet, 1),
        Triple("任务", Icons.Filled.Task, 2),
        Triple("记录", Icons.Filled.Description, 3),
        Triple("设置", Icons.Filled.Settings, 4),
    )
    var tab by remember { mutableIntStateOf(0) }
    val effectiveTab = if (Prefs.backend.isBlank()) 4 else tab

    Scaffold(
        bottomBar = {
            NavigationBar {
                tabs.forEach { (label, icon, index) ->
                    NavigationBarItem(
                        selected = effectiveTab == index,
                        onClick = { tab = index },
                        icon = { Icon(icon, contentDescription = label) },
                        label = { Text(label) },
                    )
                }
            }
        },
    ) { pad ->
        androidx.compose.foundation.layout.Box(Modifier.fillMaxSize().padding(pad)) {
            when (effectiveTab) {
                0 -> HomePage(Prefs.backend)
                1 -> AssetsPage(Prefs.backend)
                2 -> TasksPage(Prefs.backend)
                3 -> RecordsPage(Prefs.backend)
                else -> SettingsPage(
                    currentBackend = backend,
                    onThemeChanged = onThemeChanged,
                    onDynamicColorChanged = onDynamicColorChanged,
                    onBackendSaved = onBackendSaved,
                )
            }
        }
    }
}