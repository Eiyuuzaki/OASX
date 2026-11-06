using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using OASX.Services;

namespace OASX.Views;

/// <summary>配置传输页：导出（整份/单任务、备份/分享）与导入（整份/单任务）。
///
/// 对齐 Flutter config_transfer_dialog + config_transfer_api。
/// 后端明确约定用 JSON 而非 multipart（无 python-multipart）。
/// </summary>
public partial class TransferPage : UserControl
{
    private string _mode = "backup";

    public TransferPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        // 能力声明
        var caps = await OasTransferApi.GetCapabilitiesAsync();
        if (caps is null)
        {
            CapabilityText.Text = "后端不支持配置传输（接口不可用）";
            ExportButton.IsEnabled = false;
            ImportButton.IsEnabled = false;
            return;
        }
        var ok = caps.Version == 1 && caps.ConfigImport && caps.TaskImport;
        CapabilityText.Text = ok
            ? $"后端支持 v{caps.Version} · 上限 {caps.MaxBytes / 1024} KB"
            : $"后端能力不足（version={caps.Version}, import={caps.ConfigImport}/{caps.TaskImport}）";
        ExportButton.IsEnabled = ok;
        ImportButton.IsEnabled = ok;

        // 配置列表
        try
        {
            var configs = await OasApi.GetConfigListAsync();
            ExportConfigSelector.ItemsSource = configs;
            ImportConfigSelector.ItemsSource = configs;
            if (configs.Count > 0)
            {
                var cur = GlobalAccountContext.Instance.Current?.Config;
                var idx = cur is not null ? configs.IndexOf(cur) : -1;
                ExportConfigSelector.SelectedIndex = idx >= 0 ? idx : 0;
                ImportConfigSelector.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"配置列表加载失败：{ex.Message}";
        }
    }

    // ==================== 页签切换 ====================

    private void TabExport_Click(object sender, RoutedEventArgs e)
    {
        ExportPanel.Visibility = Visibility.Visible;
        ImportPanel.Visibility = Visibility.Collapsed;
    }

    private void TabImport_Click(object sender, RoutedEventArgs e)
    {
        ExportPanel.Visibility = Visibility.Collapsed;
        ImportPanel.Visibility = Visibility.Visible;
    }

    // ==================== 导出 ====================

    private async void Scope_Checked(object sender, RoutedEventArgs e)
    {
        if (ExportTaskSelector is null) return;
        var byTask = ScopeTask?.IsChecked == true;
        ExportTaskSelector.IsEnabled = byTask;
        if (byTask && ExportTaskSelector.ItemsSource is null)
        {
            await LoadTaskListAsync();
        }
    }

    private async Task LoadTaskListAsync()
    {
        try
        {
            var menu = await OasApi.Http.GetStringAsync("/script_menu");
            var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(menu);
            var flat = (map ?? new())
                .SelectMany(kv => kv.Value)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            ExportTaskSelector.ItemsSource = flat;
            if (flat.Count > 0) ExportTaskSelector.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"任务列表加载失败：{ex.Message}";
        }
    }

    private async void ExportConfigSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ExportTaskSelector?.IsEnabled == true && ExportTaskSelector.ItemsSource is null)
        {
            await LoadTaskListAsync();
        }
    }

    private string? CurrentExportConfig()
        => ExportConfigSelector?.SelectedItem as string;

    private void ReadMode()
    {
        _mode = (ExportModeSelector?.SelectedItem as ComboBoxItem)?.Tag as string ?? "backup";
    }

    private async Task<(bool Ok, string Body)> DoExportAsync()
    {
        ReadMode();
        var config = CurrentExportConfig();
        if (config is null) return (false, "未选择配置");

        if (ScopeTask?.IsChecked == true)
        {
            var task = ExportTaskSelector?.SelectedItem as string;
            if (task is null) return (false, "未选择任务");
            return await OasTransferApi.ExportTaskAsync(config, task, _mode);
        }
        return await OasTransferApi.ExportConfigAsync(config, _mode);
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        PreviewButton.IsEnabled = false;
        ExportStatus.Text = "生成中…";
        try
        {
            var (ok, body) = await DoExportAsync();
            if (!ok)
            {
                ExportStatus.Text = $"导出失败：{body}";
                PreviewText.Text = "";
                return;
            }
            // 预览截断：只显示前 4000 字符，避免大文件卡 UI
            PreviewText.Text = body.Length > 4000
                ? body[..4000] + "\n\n…（已截断，完整内容请导出到文件）"
                : body;
            ExportStatus.Text = $"预览成功（{body.Length} 字符）";
        }
        finally
        {
            PreviewButton.IsEnabled = true;
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        ReadMode();
        var config = CurrentExportConfig();
        if (config is null)
        {
            ExportStatus.Text = "未选择配置";
            return;
        }

        var byTask = ScopeTask?.IsChecked == true;
        var task = byTask ? ExportTaskSelector?.SelectedItem as string : null;
        if (byTask && task is null)
        {
            ExportStatus.Text = "未选择任务";
            return;
        }

        var defaultName = byTask
            ? $"{config}-{task}-{_mode}.json"
            : $"{config}-{_mode}.json";

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出配置",
            FileName = defaultName,
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        ExportButton.IsEnabled = false;
        ExportStatus.Text = "导出中…";
        try
        {
            var (ok, body) = await DoExportAsync();
            if (!ok)
            {
                ExportStatus.Text = $"导出失败：{body}";
                return;
            }
            await File.WriteAllTextAsync(dlg.FileName, body, System.Text.Encoding.UTF8);
            ExportStatus.Text = $"已导出到 {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"导出失败：{ex.Message}";
        }
        finally
        {
            ExportButton.IsEnabled = true;
        }
    }

    // ==================== 导入 ====================

    private void ImportScope_Checked(object sender, RoutedEventArgs e)
    {
        if (ImportConfigSelector is null) return;
        var byTask = ImportTask?.IsChecked == true;
        ImportConfigSelector.IsEnabled = byTask;
        ImportHintText.Text = byTask
            ? "（把任务参数写入所选配置）"
            : "（整份导入会新建配置，不覆盖已有）";
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var byTask = ImportTask?.IsChecked == true;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = byTask ? "选择任务参数文件" : "选择配置文件",
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        ImportButton.IsEnabled = false;
        ImportStatus.Text = "导入中…";
        ImportResultText.Text = "";
        try
        {
            (bool ok, string body) result;
            if (byTask)
            {
                var config = ImportConfigSelector?.SelectedItem as string;
                if (config is null)
                {
                    ImportStatus.Text = "未选择目标配置";
                    return;
                }
                result = await OasTransferApi.ImportTaskAsync(config, dlg.FileName);
            }
            else
            {
                result = await OasTransferApi.ImportConfigAsync(dlg.FileName);
            }

            if (result.ok)
            {
                ImportStatus.Text = "导入成功";
                ImportResultText.Text = FormatImportBody(result.body);
                // 新配置可能已加入列表，刷新
                await InitAsync();
            }
            else
            {
                ImportStatus.Text = "导入失败";
                ImportResultText.Text = FormatImportBody(result.body);
            }
        }
        catch (Exception ex)
        {
            ImportStatus.Text = "导入失败";
            ImportResultText.Text = ex.Message;
        }
        finally
        {
            ImportButton.IsEnabled = true;
        }
    }

    /// <summary>把后端返回的 JSON 结果整理成可读文本（含 warnings）。</summary>
    private static string FormatImportBody(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var sb = new System.Text.StringBuilder();
            if (root.TryGetProperty("detail", out var detail))
            {
                sb.AppendLine($"错误：{detail}");
            }
            if (root.TryGetProperty("name", out var name))
            {
                sb.AppendLine($"配置名：{name}");
            }
            if (root.TryGetProperty("warnings", out var warns)
                && warns.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("警告：");
                foreach (var w in warns.EnumerateArray())
                {
                    sb.AppendLine($"  · {w}");
                }
            }
            if (sb.Length == 0) sb.AppendLine(body);
            return sb.ToString();
        }
        catch
        {
            return body;
        }
    }
}
