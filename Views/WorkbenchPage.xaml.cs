using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OASX.Services;
using ContentDialog = Wpf.Ui.Controls.ContentDialog;
using ContentDialogButton = Wpf.Ui.Controls.ContentDialogButton;
using ContentDialogResult = Wpf.Ui.Controls.ContentDialogResult;

namespace OASX.Views;

/// <summary>任务分组展示模型。</summary>
public sealed class TaskItemVm
{
    public string Name { get; init; } = "";
    public string Display { get; init; } = "";
}

/// <summary>任务分组展示模型。</summary>
public sealed class TaskGroupVm
{
    public string Group { get; init; } = "";
    public string Count { get; init; } = "";
    public List<TaskItemVm> Tasks { get; init; } = [];
}

/// <summary>
/// 工作台 v2：配置列表 + 脚本状态/启停 + 任务分组列表，
/// 任务点击进入参数编辑（ArgsHost 内切换，返回按钮回概览）。
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
            var last = WorkbenchState.ActiveScript;
            var idx = last is not null ? _configs.IndexOf(last) : -1;
            ConfigList.SelectedIndex = idx >= 0 ? idx : (_configs.Count > 0 ? 0 : -1);

            if (!_menuLoaded)
            {
                await LoadMenuAsync();
                _menuLoaded = true;

                // ★ 预热：首次进参数页要 JIT ArgsPage 类 + 其控件栈
                //（一次性成本，实测首次点击卡 1-2 秒——"第一次点不动"）。
                // 在工作台加载完成时提前付出：建一个丢弃实例，
                // 它的 args 请求打不存在的任务会 404，静默。
                try
                {
                    var warmup = new ArgsPage("__warmup__", "__warmup__");
                    _ = warmup;
                }
                catch
                {
                    // 预热失败不影响功能
                }
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
            TaskGroups.ItemsSource = menu.Where(kv => kv.Value.Count > 0).Select(kv => new TaskGroupVm
            {
                Group = I18nMenu.Tr(kv.Key),
                Count = $"{kv.Value.Count} 项",
                Tasks = kv.Value.Select(t => new TaskItemVm
                {
                    Name = t,
                    Display = I18nMenu.Tr(t),
                }).ToList(),
            }).ToList();
        }
        catch (Exception ex)
        {
            TaskGroups.ItemsSource = new[] { new TaskGroupVm
            {
                Group = "任务加载失败",
                Count = "",
                Tasks = [new TaskItemVm { Name = ex.Message, Display = ex.Message }],
            } };
        }
    }

    private async void ConfigList_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ConfigList.SelectedItem is not string script) return;
        WorkbenchState.ActiveScript = script;
        ScriptNameText.Text = script;
        ShowOverview();
        await RefreshStateAsync(script);
    }

    /// <summary>任务条目点击 → 参数编辑页。</summary>
    private void TaskItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: string task } &&
            WorkbenchState.ActiveScript is string script)
        {
            ArgsFrame.Navigate(new ArgsPage(script, task));
            ShowArgsView();
        }
    }

    private void ShowArgsView()
    {
        OverviewPanel.Visibility = Visibility.Collapsed;
        ArgsHostPanel.Visibility = Visibility.Visible;
    }

    private void ShowOverview()
    {
        ArgsHostPanel.Visibility = Visibility.Collapsed;
        OverviewPanel.Visibility = Visibility.Visible;
    }

    private void BackToOverview_Click(object sender, RoutedEventArgs e) => ShowOverview();

    // ==================== 配置管理（新建/改名/删除） ====================

    /// <summary>Fluent 输入对话框：确定返回输入值，取消返回 null。</summary>
    private async Task<string?> PromptTextAsync(string title, string initial = "")
    {
        var host = (Window.GetWindow(this) as MainWindow)?.DialogHost;
        if (host is null) return null;
        var tb = new System.Windows.Controls.TextBox { Text = initial, MinWidth = 280 };
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 4) };
        panel.Children.Add(tb);
        var dialog = new ContentDialog(host)
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? tb.Text.Trim() : null;
    }

    /// <summary>Fluent 确认对话框：返回是否点了确定。</summary>
    private async Task<bool> ConfirmAsync(string message, string title = "确认")
    {
        var host = (Window.GetWindow(this) as MainWindow)?.DialogHost;
        if (host is null) return false;
        var dialog = new ContentDialog(host)
        {
            Title = title,
            Content = new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 340,
            },
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <summary>新建配置：后端生成新名 + 以 template 复制。</summary>
    private async void ConfigAdd_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var newName = await OasConfigApi.GetNewNameAsync();
            if (string.IsNullOrEmpty(newName))
            {
                StateText.Text = "后端未能生成新配置名";
                return;
            }
            var list = await OasConfigApi.CopyAsync(newName);
            _configs = list;
            ConfigList.ItemsSource = _configs;
            var idx = _configs.IndexOf(newName);
            ConfigList.SelectedIndex = idx >= 0 ? idx : _configs.Count - 1;
            StateText.Text = $"已创建 {newName}";
        }
        catch (Exception ex)
        {
            StateText.Text = $"新建失败：{ex.Message}";
        }
    }

    /// <summary>重命名当前配置（运行中会被后端停掉，属预期行为）。</summary>
    private async void ConfigRename_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigList.SelectedItem is not string oldName)
        {
            StateText.Text = "先选中一个配置";
            return;
        }
        var newName = await PromptTextAsync("重命名配置", oldName);
        if (newName is null || newName == oldName) return;
        try
        {
            var ok = await OasConfigApi.RenameAsync(oldName, newName);
            if (!ok)
            {
                StateText.Text = "重命名失败（名称冲突或被占用）";
                return;
            }
            _configs = await OasApi.GetConfigListAsync();
            ConfigList.ItemsSource = _configs;
            var idx = _configs.IndexOf(newName);
            ConfigList.SelectedIndex = idx >= 0 ? idx : -1;
            StateText.Text = $"已重命名为 {newName}";
        }
        catch (Exception ex)
        {
            StateText.Text = $"重命名失败：{ex.Message}";
        }
    }

    /// <summary>删除当前配置（template 受后端保护；运行中的会被先停掉）。</summary>
    private async void ConfigDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigList.SelectedItem is not string name) return;
        if (!await ConfirmAsync($"删除配置「{name}」？此操作不可恢复。")) return;
        try
        {
            var ok = await OasConfigApi.DeleteAsync(name);
            if (!ok)
            {
                StateText.Text = "删除失败（template 不可删除或被占用）";
                return;
            }
            _configs = await OasApi.GetConfigListAsync();
            ConfigList.ItemsSource = _configs;
            ConfigList.SelectedIndex = _configs.Count > 0 ? 0 : -1;
            StateText.Text = $"已删除 {name}";
        }
        catch (Exception ex)
        {
            StateText.Text = $"删除失败：{ex.Message}";
        }
    }

    private async Task RefreshStateAsync(string script)
    {
        StateText.Text = "状态查询中…";
        var state = await OasApi.GetScriptStateAsync(script);
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
