using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

/// <summary>资产条目展示模型（图标卡）。</summary>
public sealed class AssetCardVm
{
    public string Name { get; init; } = "";
    public string CountText { get; init; } = "";
    public string DeltaText { get; init; } = "";
    public Brush DeltaBrush { get; init; } = Brushes.Transparent;
    public ImageSource? IconImage { get; init; }
}

/// <summary>
/// 资产页：日期导航 + 图标卡片网格。
/// 复刻 Flutter 版 resource_controller 的三步筛选：
/// 图标白名单 → (图标,名称) 聚合 → |delta| 降序。
/// </summary>
public partial class ResourcePage : UserControl
{
    /// <summary>图标白名单：Assets/currency/ 下的 basename（与 Flutter 版
    /// IconRegistry 同源——只展示本地有图标的资源，190+ 项 → 十几项）。</summary>
    private static readonly HashSet<string> IconWhitelist = BuildIconWhitelist();

    private List<string> _dates = [];
    private int _dateIndex = -1;
    private bool _suppressDateEvent;

    public ResourcePage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private static HashSet<string> BuildIconWhitelist()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "currency");
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir, "*.png"))
            {
                var baseName = Path.GetFileNameWithoutExtension(f);
                if (baseName.Length > 0 && baseName.ToLowerInvariant() != "readme")
                {
                    set.Add(baseName);
                }
            }
        }
        return set;
    }

    private async Task LoadAsync()
    {
        LoadingBar.IsEnabled = true;
        try
        {
            var configs = await OasApi.GetConfigListAsync();
            ConfigSelector.ItemsSource = configs;
            var last = WorkbenchState.ActiveScript;
            var idx = last is not null ? configs.IndexOf(last) : -1;
            ConfigSelector.SelectedIndex = idx >= 0 ? idx : (configs.Count > 0 ? 0 : -1);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"后端连接失败：{ex.Message}";
        }
        finally
        {
            LoadingBar.IsEnabled = false;
        }
    }

    /// <summary>配置切换 → 加载角色与日期 → 加载最新一天。</summary>
    private async void ConfigSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ConfigSelector.SelectedItem is not string config) return;
        await LoadDatesForAsync(config);
    }

    private async Task LoadDatesForAsync(string config)
    {
        LoadingBar.IsEnabled = true;
        EmptyText.Visibility = Visibility.Collapsed;
        AssetCards.ItemsSource = null;
        try
        {
            var roles = await OasResourceApi.GetRolesAsync(config);
            var role = roles?.Roles?.FirstOrDefault();
            if (role is null)
            {
                RoleText.Text = "无角色数据";
                return;
            }
            RoleText.Text = $"{role.Name} Lv{role.Level} · {role.ServerName}";
            _dates = role.Dates ?? [];
            _suppressDateEvent = true;
            DateSelector.ItemsSource = _dates;
            _dateIndex = _dates.Count > 0 ? 0 : -1;
            DateSelector.SelectedIndex = _dateIndex;
            _suppressDateEvent = false;

            if (CurrentDate is string date)
            {
                await LoadDayAsync(config, date);
            }
            else
            {
                EmptyText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败：{ex.Message}";
        }
        finally
        {
            LoadingBar.IsEnabled = false;
        }
    }

    private string? CurrentDate =>
        _dateIndex >= 0 && _dateIndex < _dates.Count ? _dates[_dateIndex] : null;

    /// <summary>日期切换 → 加载该日明细。</summary>
    private async void DateSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressDateEvent) return;
        if (ConfigSelector.SelectedItem is string config && CurrentDate is string date)
        {
            await LoadDayAsync(config, date);
        }
    }

    private async void PrevDate_Click(object sender, RoutedEventArgs e)
    {
        // 日期列表倒序（最新在前）：← = 更新的一天
        if (_dateIndex > 0)
        {
            _dateIndex--;
            _suppressDateEvent = true;
            DateSelector.SelectedIndex = _dateIndex;
            _suppressDateEvent = false;
            if (ConfigSelector.SelectedItem is string config && CurrentDate is string date)
            {
                await LoadDayAsync(config, date);
            }
        }
    }

    private async void NextDate_Click(object sender, RoutedEventArgs e)
    {
        if (_dateIndex < _dates.Count - 1)
        {
            _dateIndex++;
            _suppressDateEvent = true;
            DateSelector.SelectedIndex = _dateIndex;
            _suppressDateEvent = false;
            if (ConfigSelector.SelectedItem is string config && CurrentDate is string date)
            {
                await LoadDayAsync(config, date);
            }
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigSelector.SelectedItem is string config)
        {
            await LoadDatesForAsync(config);
        }
    }

    /// <summary>加载某天的资产明细并渲染图标卡。</summary>
    private async Task LoadDayAsync(string config, string date)
    {
        LoadingBar.IsEnabled = true;
        EmptyText.Visibility = Visibility.Collapsed;
        try
        {
            var day = await OasResourceApi.GetResourceDayAsync(config, date);
            if (day is null || day.Items.Count == 0)
            {
                AssetCards.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                StatusText.Text = $"{date} 无数据";
                return;
            }

            StatusText.Text = $"{date} · 更新于 {day.UpdatedAt} · 展示 {visibleCount(day.Items)} / {day.Items.Count} 项";

            // Flutter 版 resource_controller._applyIconFilter 的移植：
            // 1. 图标白名单 + count>0 → 2. (图标,名称) 聚合 → 3. |delta| 降序
            var visible = day.Items
                .Where(item => IconWhitelist.Contains(BaseName(item.Icon)) && item.Count > 0)
                .GroupBy(item => (BaseName(item.Icon), item.Name))
                .Select(g =>
                {
                    var count = g.Sum(x => x.Count);
                    var deltaSum = g.Sum(x => x.Delta);
                    var newest = g.OrderByDescending(x => x.Id).First();
                    return new OasResourceApi.ResourceItem(
                        newest.Id, g.Key.Name, g.Key.Item1, count, deltaSum);
                })
                .OrderByDescending(x => Math.Abs(x.Delta))
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AssetCards.ItemsSource = visible.Select(item => new AssetCardVm
            {
                Name = item.Name,
                CountText = FormatCount(item.Count),
                DeltaText = item.Delta == 0 ? "" : FormatDelta(item.Delta),
                DeltaBrush = new SolidColorBrush(item.Delta >= 0
                    ? Color.FromRgb(0x4C, 0xAF, 0x50)
                    : Color.FromRgb(0xE5, 0x53, 0x53)),
                IconImage = LoadIcon(item.Icon),
            }).ToList();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败：{ex.Message}";
        }
        finally
        {
            LoadingBar.IsEnabled = false;
        }
    }

    private static int visibleCount(
        IEnumerable<OasResourceApi.ResourceItem> items) =>
        items.Count(item => IconWhitelist.Contains(BaseName(item.Icon)) && item.Count > 0);

    /// <summary>图标名 → 本地图标资源（无图标返回 null，XAML 隐藏 Image）。</summary>
    private static ImageSource? LoadIcon(string icon)
    {
        var baseName = BaseName(icon);
        if (baseName.Length == 0) return null;
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/currency/{baseName}.png");
            var img = new System.Windows.Media.Imaging.BitmapImage(uri);
            return img;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>后端 icon 可能是 icon_jinbi 或 icon_jinbi.png → 统一去扩展名。</summary>
    private static string BaseName(string icon)
    {
        var name = icon.Trim();
        if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }
        return name;
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
