using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OASX.Services;

namespace OASX.Views;

/// <summary>任务分组展示模型。</summary>
public sealed class TaskGroupVm
{
    public string Group { get; init; } = "";
    public string Count { get; init; } = "";
    public List<string> Tasks { get; init; } = [];
}

/// <summary>
/// 工作台 v1：配置列表 + 脚本状态/启停 + 任务分组列表。
/// 参数编辑（/{script}/{task}/args）与快速调度为下一增量。
/// </summary>
public partial class WorkbenchPage : UserControl
{
    private List<string> _configs = [];
    private bool _menuLoaded;

    public WorkbenchPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _configs = await OasApi.GetConfigListAsync();
            ConfigList.ItemsSource = _configs;
            // 恢复上次选中（页面每次导航重建，记在内存即可）
            var last = WorkbenchState.ActiveScript;
            var idx = last is not null ? _configs.IndexOf(last) : -1;
            ConfigList.SelectedIndex = idx >= 0 ? idx : (_configs.Count > 0 ? 0 : -1);

            if (!_menuLoaded)
            {
                await LoadMenuAsync();
                _menuLoaded = true;
            }
        }
        catch (Exception ex)
        {
            StateText.Text = $"后端连接失败：{ex.Message}";
        }
    }

    private async Task LoadMenuAsync()
    {
        try
        {
            var menu = await OasWorkbenchApi.GetScriptMenuAsync();
            TaskGroups.ItemsSource = menu.Select(kv => new TaskGroupVm
            {
                Group = kv.Key,
                Count = $"{kv.Value.Count} 项",
                Tasks = kv.Value,
            }).ToList();
        }
        catch (Exception ex)
        {
            TaskGroups.ItemsSource = new[] { new TaskGroupVm
            {
                Group = "任务加载失败",
                Count = "",
                Tasks = [ex.Message],
            } };
        }
    }

    private async void ConfigList_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ConfigList.SelectedItem is not string script) return;
        WorkbenchState.ActiveScript = script;
        ScriptNameText.Text = script;
        await RefreshStateAsync(script);
    }

    private async Task RefreshStateAsync(string script)
    {
        StateText.Text = "状态查询中…";
        var state = await OasApi.GetScriptStateAsync(script);
        // null = 后端阻塞超时/查询失败，按未知显示但不阻塞页面
        StateText.Text = state is null ? "状态：未知" : $"状态：{state.State}";
        StartButton.IsEnabled = state?.State != "running";
        StopButton.IsEnabled = state?.State == "running";
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (WorkbenchState.ActiveScript is not string script) return;
        StartButton.IsEnabled = false;
        var (ok, body) = await OasApi.StartScriptAsync(script);
        StateText.Text = ok ? "已发送启动" : $"启动失败：{body}";
        await RefreshStateAsync(script);
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (WorkbenchState.ActiveScript is not string script) return;
        StopButton.IsEnabled = false;
        var (ok, body) = await OasApi.StopScriptAsync(script);
        StateText.Text = ok ? "已发送停止" : $"停止失败：{body}";
        await RefreshStateAsync(script);
    }
}

/// <summary>
/// 当前工作台选中的脚本（页面每次导航重建，选中态用静态字段跨导航保留；
/// 应用重启后回到首个配置——与 Flutter 版行为一致）。
/// </summary>
public static class WorkbenchState
{
    public static string? ActiveScript { get; set; }
}
