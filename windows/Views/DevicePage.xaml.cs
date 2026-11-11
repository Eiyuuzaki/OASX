using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OASX.Services;

namespace OASX.Views;

/// <summary>设备管理页：查看/切换各调度器使用的模拟器。
///
/// 对齐 Android DevicePage。后端行为：PUT /device/current 只改配置文件，
/// 不重启运行中的脚本（下次启动生效）。
/// </summary>
public partial class DevicePage : UserControl
{
    public sealed class InstanceRow
    {
        public string Serial { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string SubText { get; init; } = "";
        public string Badge { get; init; } = "";
    }

    public DevicePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            var configs = await OasApi.GetConfigListAsync();
            ConfigSelector.ItemsSource = configs;
            if (configs.Count == 0) return;

            // 优先跟随全局选中账号的调度器
            var wanted = GlobalAccountContext.Instance.Current?.Config;
            var idx = wanted is not null ? configs.IndexOf(wanted) : -1;
            ConfigSelector.SelectedIndex = idx >= 0 ? idx : 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"配置列表加载失败：{ex.Message}";
        }
    }

    private string? CurrentConfig => ConfigSelector?.SelectedItem as string;

    private async void ConfigSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await RefreshAsync();

    private async Task RefreshAsync()
    {
        var config = CurrentConfig;
        if (config is null)
        {
            StatusText.Text = "无配置";
            return;
        }

        StatusText.Text = "读取中…";
        var current = await OasDeviceApi.GetCurrentAsync(config);
        if (current is not null)
        {
            CurrentSerialText.Text = current.Serial;
            var state = current.Online ? "在线" : "离线";
            CurrentMetaText.Text = string.Join(" · ",
                new[] { current.EmulatorName, current.EmulatorType, state }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        else
        {
            CurrentSerialText.Text = "—";
            CurrentMetaText.Text = "读取失败";
        }

        var resp = await OasDeviceApi.ListInstancesAsync();
        var rows = new ObservableCollection<InstanceRow>();
        if (resp?.Instances is { } list)
        {
            foreach (var inst in list)
            {
                rows.Add(new InstanceRow
                {
                    Serial = inst.Serial,
                    DisplayName = string.IsNullOrWhiteSpace(inst.DisplayName)
                        ? inst.InstanceName : inst.DisplayName,
                    SubText = string.Join(" · ", new[]
                    {
                        inst.Serial,
                        inst.Online ? "在线" : "离线",
                        inst.Root == true ? "root" : null,
                        string.IsNullOrWhiteSpace(inst.Model) ? null : inst.Model,
                    }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    Badge = current?.Serial == inst.Serial ? "当前" : "",
                });
            }
        }
        InstanceList.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 选中当前设备
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Serial == current?.Serial)
            {
                InstanceList.SelectedIndex = i;
                break;
            }
        }

        StatusText.Text = "";
    }

    private async void InstanceList_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (InstanceList.SelectedItem is not InstanceRow row) return;
        var config = CurrentConfig;
        if (config is null) return;
        if (row.Serial == CurrentSerialText.Text) return; // 未变化不切换

        // 确认对话框：切换会影响下次脚本启动
        var confirm = System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            $"把调度器 {config} 的设备切换为 {row.Serial}？\n\n只改配置，不中断正在运行的脚本；下次启动脚本时生效。",
            "切换设备", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            await RefreshAsync(); // 恢复选中
            return;
        }

        StatusText.Text = "切换中…";
        var (ok, body) = await OasDeviceApi.SwitchAsync(config, row.Serial);
        StatusText.Text = ok
            ? $"已切换到 {row.Serial}（下次启动脚本生效）"
            : $"切换失败：{body}";
        await RefreshAsync();
    }
}
