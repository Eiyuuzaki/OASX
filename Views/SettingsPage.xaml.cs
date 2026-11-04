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
        Loaded += (_, _) =>
        {
            _ = LoadNotifyAsync();
        };

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

    // ==================== 通知（onepush）====================

    private Dictionary<string, OasNotifyApi.ProviderInfo>? _providers;
    private OasNotifyApi.ProviderInfo? _currentProvider;
    private bool _loadingNotify;
    private readonly Dictionary<string, string> _paramValues = [];

    private async Task LoadNotifyAsync()
    {
        _loadingNotify = true;
        try
        {
            var providers = await OasNotifyApi.GetProvidersAsync();
            if (providers is null)
            {
                NotifyStatusText.Text = "渠道列表加载失败";
                return;
            }
            _providers = providers;
            NotifyProviderBox.Items.Clear();
            NotifyProviderBox.Items.Add(_MakeItem("null", "关闭"));
            foreach (var name in providers.Keys.OrderBy(x => x))
            {
                NotifyProviderBox.Items.Add(_MakeItem(name, name));
            }
            var cfg = await OasNotifyApi.GetConfigAsync();
            if (cfg is not null)
            {
                NotifyEnableSwitch.IsChecked = cfg.Value.Enable;
                _SelectProvider(cfg.Value.Provider);
                foreach (var (k, v) in cfg.Value.Params)
                {
                    _paramValues[k] = v;
                }
                _RefreshParamFields();
                _SelectCycle(cfg.Value.CycleHours);
            }
            else
            {
                _SelectProvider("null");
            }
        }
        catch (Exception ex)
        {
            NotifyStatusText.Text = $"加载失败：{ex.Message}";
        }
        finally
        {
            _loadingNotify = false;
        }
    }

    private static ComboBoxItem _MakeItem(string tag, string content) => new()
    {
        Tag = tag,
        Content = content,
    };

    private void _SelectProvider(string name)
    {
        for (var i = 0; i < NotifyProviderBox.Items.Count; i++)
        {
            if (NotifyProviderBox.Items[i] is ComboBoxItem { Tag: string t } && t == name)
            {
                NotifyProviderBox.SelectedIndex = i;
                return;
            }
        }
        NotifyProviderBox.SelectedIndex = 0;
    }

    private void _SelectCycle(int hours)
    {
        for (var i = 0; i < NotifyCycleBox.Items.Count; i++)
        {
            if (NotifyCycleBox.Items[i] is ComboBoxItem { Tag: string t }
                && int.TryParse(t, out var h) && h == hours)
            {
                NotifyCycleBox.SelectedIndex = i;
                return;
            }
        }
        NotifyCycleBox.SelectedIndex = 0;
    }

    private void NotifyProvider_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingNotify)
        {
            return;
        }
        _RefreshParamFields();
        _ = SaveNotifyAsync();
    }

    private void NotifyCycle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingNotify)
        {
            _ = SaveNotifyAsync();
        }
    }

    private void NotifyEnable_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loadingNotify)
        {
            _ = SaveNotifyAsync();
        }
    }

    private void _RefreshParamFields()
    {
        NotifyFieldsPanel.Children.Clear();
        _paramValues.Clear();
        if (NotifyProviderBox.SelectedItem is not ComboBoxItem { Tag: string tag } ||
            tag == "null" || _providers is null || !_providers.TryGetValue(tag, out var info))
        {
            _currentProvider = null;
            return;
        }
        _currentProvider = info;
        foreach (var name in info.Required.Concat(info.Optional))
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var label = new TextBlock
            {
                Text = name + (info.Required.Contains(name) ? " *" : ""),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var box = new Wpf.Ui.Controls.TextBox
            {
                PlaceholderText = info.Required.Contains(name)
                    ? "必填" : "选填",
                Text = _paramValues.TryGetValue(name, out var v) ? v : "",
            };
            box.TextChanged += (o, _) =>
            {
                _paramValues[name] = box.Text;
            };
            Grid.SetColumn(label, 0);
            Grid.SetColumn(box, 1);
            row.Children.Add(label);
            row.Children.Add(box);
            NotifyFieldsPanel.Children.Add(row);
        }
    }

    private async Task SaveNotifyAsync()
    {
        try
        {
            var provider = NotifyProviderBox.SelectedItem is ComboBoxItem { Tag: string t }
                ? t : "null";
            var enable = NotifyEnableSwitch.IsChecked == true;
            var cycle = NotifyCycleBox.SelectedItem is ComboBoxItem { Tag: string ct }
                && int.TryParse(ct, out var h) ? h : 6;
            // 必填校验
            if (_currentProvider is not null)
            {
                var missing = _currentProvider.Required
                    .Where(r => !_paramValues.TryGetValue(r, out var v)
                                || string.IsNullOrWhiteSpace(v))
                    .ToList();
                if (missing.Count > 0)
                {
                    NotifyStatusText.Text = $"缺少必填参数：{string.Join("、", missing)}";
                    return;
                }
            }
            var ok = await OasNotifyApi.SaveConfigAsync(
                enable, provider, _paramValues, cycle);
            NotifyStatusText.Text = ok
                ? $"已保存 {DateTime.Now:HH:mm:ss}" + (enable ? "（已启用）" : "")
                : "保存失败";
        }
        catch (Exception ex)
        {
            NotifyStatusText.Text = $"保存失败：{ex.Message}";
        }
    }

    private async void NotifyTest_Click(object sender, RoutedEventArgs e)
    {
        NotifyTestButton.IsEnabled = false;
        try
        {
            await SaveNotifyAsync();
            NotifyStatusText.Text = "测试中…";
            var result = await OasNotifyApi.TestAsync();
            NotifyStatusText.Text = $"测试：{result}";
        }
        finally
        {
            NotifyTestButton.IsEnabled = true;
        }
    }

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
