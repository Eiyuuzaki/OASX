using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using Microsoft.Win32;
using OASX.Services;
using ContentDialog = Wpf.Ui.Controls.ContentDialog;
using ContentDialogButton = Wpf.Ui.Controls.ContentDialogButton;
using ContentDialogResult = Wpf.Ui.Controls.ContentDialogResult;

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

    // ==================== 后端操作（真实端点） ====================

    private async void TestBackend_Click(object sender, RoutedEventArgs e)
    {
        BackendStatusText.Text = "检测中…";
        try
        {
            _ = await OasApi.GetConfigListAsync();
            BackendDot.Fill = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
            BackendStatusText.Text = "连接正常";
        }
        catch
        {
            BackendDot.Fill = new SolidColorBrush(Color.FromRgb(0xDC, 0x45, 0x45));
            BackendStatusText.Text = "不可达";
        }
    }

    /// <summary>导出诊断包：GET /home/export_diagnostic → 打开产物所在目录。</summary>
    private async void ExportDiagnostic_Click(object sender, RoutedEventArgs e)
    {
        BackendStatusText.Text = "生成诊断包…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var res = await OasApi.Http.GetAsync("/home/export_diagnostic", cts.Token);
            res.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cts.Token));
            var root = doc.RootElement;
            var ok = root.TryGetProperty("success", out var sEl) && sEl.GetBoolean();
            var path = root.TryGetProperty("path", out var pEl) ? pEl.GetString() ?? "" : "";
            if (ok && path.Length > 0)
            {
                BackendStatusText.Text = $"已导出：{path}";
                // 打开资源管理器并选中产物
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true,
                });
            }
            else
            {
                var err = root.TryGetProperty("error", out var eEl) ? eEl.GetString() : "";
                BackendStatusText.Text = $"导出失败：{err}";
            }
        }
        catch (Exception ex)
        {
            BackendStatusText.Text = $"导出失败：{ex.Message}";
        }
    }

    /// <summary>停止后端：GET /home/kill_server（需确认）。</summary>
    private async void StopBackend_Click(object sender, RoutedEventArgs e)
    {
        var host = (Window.GetWindow(this) as MainWindow)?.DialogHost;
        if (host is not null)
        {
            var dialog = new ContentDialog(host)
            {
                Title = "停止后端",
                Content = new TextBlock
                {
                    Text = "将向后端发送停止指令。停止后需重新启动 OAS 才能继续使用（可开启「启动时自动拉起后端」）。",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 360,
                },
                PrimaryButtonText = "停止",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                DialogWidth = 420,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        }
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await OasApi.Http.GetAsync("/home/kill_server", cts.Token);
            BackendStatusText.Text = "已发送停止指令";
            BackendDot.Fill = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
        }
        catch
        {
            BackendStatusText.Text = "已发送（后端可能已退出）";
        }
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
