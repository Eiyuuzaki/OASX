using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OASX.Services;

namespace OASX.Views;

/// <summary>
/// 设置页（Windows 11 设置风格）：后端 / 个性化 / 窗口 / 系统 四组。
/// 开关即时持久化（OasSettings），开机自启写 HKCU Run 键（免管理员）。
/// </summary>
public partial class SettingsPage : UserControl
{
    /// <summary>开机自启的注册表键名。</summary>
    private const string RunKeyName = "OASX";

    private bool _initializing = true;

    public SettingsPage()
    {
        InitializeComponent();

        BackendBox.Text = OasSettings.BackendAddress;
        ThemeSelector.SelectedIndex = OasSettings.ThemeMode switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0,
        };
        BackdropSelector.SelectedIndex = OasSettings.BackdropMode switch
        {
            "acrylic" => 1,
            "none" => 2,
            _ => 0,
        };
        RememberBoundsToggle.IsChecked =
            OasSettings.Get("rememberWindowBounds", "false") == "true";
        MinimizeToTrayToggle.IsChecked =
            OasSettings.Get("minimizeToTray", "false") == "true";
        StopBackendToggle.IsChecked =
            OasSettings.Get("stopBackendOnExit", "false") == "true";
        LaunchAtStartupToggle.IsChecked = IsStartupRegistered();
        AutoStartBackendToggle.IsChecked = OasBackendLauncher.Enabled;
        OasWorkDirBox.Text = OasBackendLauncher.WorkDir;
        VersionText.Text = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "??"}";
        _initializing = false;
    }

    // ==================== OAS 后端 ====================

    private void SaveBackend_Click(object sender, RoutedEventArgs e)
    {
        var addr = BackendBox.Text.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(addr)) return;
        if (!addr.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !addr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            addr = "http://" + addr;
        }
        OasSettings.BackendAddress = addr;
        BackendBox.Text = addr;
        OasApi.Reset();
    }

    // ==================== 个性化 ====================

    private void ThemeSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (ThemeSelector.SelectedItem is ComboBoxItem { Tag: string mode })
        {
            OasSettings.ThemeMode = mode;
            App.ApplySavedTheme();
        }
    }

    private void BackdropSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (BackdropSelector.SelectedItem is ComboBoxItem { Tag: string mode })
        {
            OasSettings.BackdropMode = mode;
            App.ApplySavedTheme();
        }
    }

    // ==================== 窗口 ====================

    private void RememberBounds_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        OasSettings.Set("rememberWindowBounds",
            RememberBoundsToggle.IsChecked == true ? "true" : "false");
    }

    private void MinimizeToTray_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        OasSettings.Set("minimizeToTray",
            MinimizeToTrayToggle.IsChecked == true ? "true" : "false");
    }

    // ==================== 系统 ====================

    private void StopBackend_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        OasSettings.Set("stopBackendOnExit",
            StopBackendToggle.IsChecked == true ? "true" : "false");
    }

    private static string RunKeyPath =>
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private void AutoStartBackend_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        OasSettings.Set("autoStartBackend",
            AutoStartBackendToggle.IsChecked == true ? "true" : "false");
    }

    private void SaveWorkDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = OasWorkDirBox.Text.Trim();
        if (string.IsNullOrEmpty(dir)) return;
        OasSettings.Set("oasWorkDir", dir);
        OasWorkDirBox.Text = dir;
    }

    private static bool IsStartupRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunKeyName) is string;
    }

    private void LaunchAtStartup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (LaunchAtStartupToggle.IsChecked == true)
            {
                var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) return;
                key.SetValue(RunKeyName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(RunKeyName, false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"开机自启设置失败：{ex.Message}", "OASX",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
