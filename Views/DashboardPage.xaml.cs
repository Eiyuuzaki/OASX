using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

public sealed record AssetVm(string Name, string CountText, string DeltaText, Brush DeltaBrush);

public partial class DashboardPage : UserControl
{
    private List<string> _configs = [];

    public DashboardPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        LoadingBar.IsEnabled = true;
        try
        {
            _configs = await OasApi.GetConfigListAsync();
            ConfigSelector.ItemsSource = _configs;
            if (_configs.Count > 0) ConfigSelector.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            EmptyText.Text = $"后端连接失败：{ex.Message}";
            EmptyText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingBar.IsEnabled = false;
        }
    }

    private async void ConfigSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ConfigSelector.SelectedItem is not string config) return;
        await LoadAssetsAsync(config);
    }

    private async Task LoadAssetsAsync(string config)
    {
        LoadingBar.IsEnabled = true;
        EmptyText.Visibility = Visibility.Collapsed;
        try
        {
            var day = await OasApi.GetResourceDayAsync(config);
            if (day is null || day.Items.Count == 0)
            {
                AssetList.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                UpdatedAtText.Text = "";
                return;
            }
            UpdatedAtText.Text = $"快照日期 {day.Date} · 更新于 {day.UpdatedAt}";
            AssetList.ItemsSource = day.Items.Select(item => new AssetVm(
                item.Name,
                FormatCount(item.Count),
                item.Delta == 0 ? "" : FormatDelta(item.Delta),
                new SolidColorBrush(item.Delta >= 0
                    ? Color.FromRgb(0x4C, 0xAF, 0x50)
                    : Color.FromRgb(0xE5, 0x53, 0x53)))).ToList();
        }
        catch (Exception ex)
        {
            EmptyText.Text = $"加载失败：{ex.Message}";
            EmptyText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingBar.IsEnabled = false;
        }
    }

    private static string FormatCount(long n) => n switch
    {
        >= 100_000_000 => $"{n / 100_000_000.0:0.0}亿",
        >= 10_000 => $"{n / 10_000.0:0.0}万",
        _ => n.ToString("N0"),
    };

    private static string FormatDelta(long d)
    {
        var sign = d > 0 ? "+" : "";
        var v = Math.Abs(d) switch
        {
            >= 100_000_000 => $"{Math.Abs(d) / 100_000_000.0:0.0}亿",
            >= 10_000 => $"{Math.Abs(d) / 10_000.0:0.0}万",
            _ => Math.Abs(d).ToString("N0"),
        };
        return $"{sign}{v}";
    }
}
