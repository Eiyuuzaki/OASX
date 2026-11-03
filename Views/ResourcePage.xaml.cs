using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

/// <summary>角色下拉展示模型。</summary>
public sealed class RoleVm
{
    public string RoleKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public List<string> Dates { get; init; } = [];
}

/// <summary>资产卡展示模型（图标卡）。</summary>
public sealed class AssetCardVm
{
    public string Name { get; init; } = "";
    public string CountText { get; init; } = "";
    public string DeltaText { get; init; } = "";
    public Brush DeltaBrush { get; init; } = Brushes.Transparent;
    public ImageSource? IconImage { get; init; }
}

/// <summary>
/// 资产页：配置/角色/日期三联下拉 + 图标卡片网格。
/// 复刻 Flutter 版 resource_controller 的筛选管线：
/// 图标白名单（本地 currency 图标） → (图标,名称) 聚合 → |delta| 降序。
/// 190+ 项原始数据 → 十几张用户关心的卡片。
/// </summary>
public partial class ResourcePage : UserControl
{
    private List<string> _configs = [];
    private List<string> _dates = [];
    private int _dateIndex = -1;
    private bool _suppressDateEvent;

    /// <summary>响应式卡片宽度：SizeChanged 按 2/3/4 列重算（规范 6.3）。</summary>
    public static readonly DependencyProperty CardWidthProperty =
        DependencyProperty.Register(nameof(CardWidth), typeof(double),
            typeof(ResourcePage), new PropertyMetadata(225.0));

    public double CardWidth
    {
        get => (double)GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

    public ResourcePage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => RecomputeCardWidth(e.NewSize.Width);
        _ = LoadAsync();
    }

    /// <summary>可用宽度 → 列数（卡片最小 260px，2–4 列）→ 卡片宽度。</summary>
    private void RecomputeCardWidth(double available)
    {
        var usable = available - 20;                 // 页边距/滚动条余量
        var cols = Math.Clamp((int)(usable / 260), 2, 4);
        CardWidth = Math.Floor((usable - 8 * (cols - 1)) / cols);
    }

    private async Task LoadAsync()
    {
        try
        {
            _configs = await OasApi.GetConfigListAsync();
            ConfigSelector.ItemsSource = _configs;
            var last = WorkbenchState.ActiveScript;
            var idx = last is not null ? _configs.IndexOf(last) : -1;
            ConfigSelector.SelectedIndex = idx >= 0 ? idx : (_configs.Count > 0 ? 0 : -1);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"后端连接失败：{ex.Message}";
        }
    }

    /// <summary>配置切换 → 加载角色列表（含各自可用日期）。</summary>
    private async void ConfigSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ConfigSelector.SelectedItem is not string config) return;
        EmptyText.Visibility = Visibility.Collapsed;
        AssetCards.ItemsSource = null;
        try
        {
            var roles = await OasResourceApi.GetRolesAsync(config);
            var vms = roles.Select(r => new RoleVm
            {
                RoleKey = r.RoleKey,
                DisplayName = $"{r.Name} Lv{r.Level} · {r.ServerName}",
                Dates = r.Dates ?? [],
            }).ToList();
            RoleSelector.ItemsSource = vms;
            if (vms.Count > 0)
            {
                RoleSelector.SelectedIndex = 0;
            }
            else
            {
                RoleSelector.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载角色失败：{ex.Message}";
        }
    }

    /// <summary>角色切换 → 加载该角色的可用日期 → 加载最新一天。</summary>
    private async void RoleSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (RoleSelector.SelectedItem is not RoleVm role) return;
        if (ConfigSelector.SelectedItem is not string config) return;
        try
        {
            _dates = role.Dates;
            _suppressDateEvent = true;
            DateSelector.ItemsSource = _dates;
            _dateIndex = _dates.Count > 0 ? 0 : -1;
            DateSelector.SelectedIndex = _dateIndex;
            _suppressDateEvent = false;

            if (CurrentDate is string date)
            {
                await LoadDayAsync(config, date, role.RoleKey);
            }
            else
            {
                EmptyText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载日期失败：{ex.Message}";
        }
    }

    private string? CurrentDate =>
        _dateIndex >= 0 && _dateIndex < _dates.Count ? _dates[_dateIndex] : null;

    /// <summary>日期切换 → 加载该日明细。</summary>
    private async void DateSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressDateEvent) return;
        await LoadCurrentDayAsync();
    }

    /// <summary>← = 更新的一天（日期列表倒序，最新在前）。</summary>
    private async void PrevDate_Click(object sender, RoutedEventArgs e)
    {
        if (_dateIndex > 0)
        {
            _dateIndex--;
            _suppressDateEvent = true;
            DateSelector.SelectedIndex = _dateIndex;
            _suppressDateEvent = false;
            await LoadCurrentDayAsync();
        }
    }

    /// <summary>→ = 更旧的一天。</summary>
    private async void NextDate_Click(object sender, RoutedEventArgs e)
    {
        if (_dateIndex < _dates.Count - 1)
        {
            _dateIndex++;
            _suppressDateEvent = true;
            DateSelector.SelectedIndex = _dateIndex;
            _suppressDateEvent = false;
            await LoadCurrentDayAsync();
        }
    }

    private async Task LoadCurrentDayAsync()
    {
        if (ConfigSelector.SelectedItem is string config &&
            CurrentDate is string date &&
            RoleSelector.SelectedItem is RoleVm role)
        {
            await LoadDayAsync(config, date, role.RoleKey);
        }
    }

    /// <summary>加载某天的资产明细并渲染图标卡。</summary>
    private async Task LoadDayAsync(string config, string date, string roleKey)
    {
        EmptyText.Visibility = Visibility.Collapsed;
        try
        {
            var day = await OasResourceApi.GetResourceDayAsync(config, date, roleKey);
            if (day is null || day.Items.Count == 0)
            {
                AssetCards.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                StatusText.Text = $"{date} 无数据";
                return;
            }

            // Flutter 版 resource_controller._applyIconFilter 的移植：
            // 1. 图标白名单（本地 currency 图标）+ count>0
            // 2. (图标,名称) 聚合——同名视为同一种资产的多期版本，数量相加
            // 3. |delta| 降序，同名稳定
            var byIconName = day.Items
                .Where(item => CurrencyIconRegistry.Has(item.Icon) && item.Count > 0)
                .GroupBy(item => (CurrencyIconRegistry.BaseName(item.Icon), item.Name))
                .Select(g =>
                {
                    var count = g.Sum(x => x.Count);
                    var deltaSum = g.Sum(x => x.Delta ?? 0);
                    var newest = g.OrderByDescending(x => x.Id).First();
                    return new OasResourceApi.ResourceItem(
                        newest.Id, g.Key.Name, g.Key.Item1, count, deltaSum);
                })
                .ToList();

            // 3. **按图标二次去重**（Flutter merged[icon] + _preferGroup）：
            //    同一图标下的多期/多名称版本只保留一张卡——
            //    优先常驻档（id<901000），其次数量大，最后 id 小。
            //    没有这步时过期货币和活动代币会整屏刷出来（55 张卡 bug）。
            var byIcon = new Dictionary<string, OasResourceApi.ResourceItem>();
            foreach (var agg in byIconName)
            {
                var icon = CurrencyIconRegistry.BaseName(agg.Icon);
                if (!byIcon.TryGetValue(icon, out var prev) || PreferGroup(agg, prev))
                {
                    byIcon[icon] = agg;
                }
            }

            var visible = byIcon.Values
                .OrderByDescending(x => Math.Abs(x.Delta ?? 0))
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AssetCards.ItemsSource = visible.Select(item => new AssetCardVm
            {
                Name = item.Name,
                CountText = FormatCount(item.Count),
                DeltaText = item.Delta is { } d && d != 0 ? FormatDelta(d) : "",
                DeltaBrush = new SolidColorBrush((item.Delta ?? 0) >= 0
                    ? Color.FromRgb(0x4C, 0xAF, 0x50)
                    : Color.FromRgb(0xE5, 0x53, 0x53)),
                IconImage = LoadIcon(item.Icon),
            }).ToList();
            StatusText.Text = $"{date} · {visible.Count} 项资产";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败：{ex.Message}";
        }
    }

    private static ImageSource? LoadIcon(string icon)
    {
        var baseName = CurrencyIconRegistry.BaseName(icon);
        if (baseName.Length == 0) return null;
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/currency/{baseName}.png");
            return new System.Windows.Media.Imaging.BitmapImage(uri);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>常驻档判定线：资源 ID 低于此值的是常驻资产，高于的是
    /// 活动期次版本（Flutter _coreIdCutoff = 901000）。</summary>
    private const int CoreIdCutoff = 901000;

    /// <summary>资源 ID 通常是纯数字；非数字时退化为 0 保证比较不抛异常。</summary>
    private static int IdOf(OasResourceApi.ResourceItem item) =>
        int.TryParse(item.Id, out var n) ? n : 0;

    /// <summary>同一图标下多个名称分组的取舍：优先常驻档，其次数量大，
    /// 最后 id 小（Flutter _preferGroup 移植）。</summary>
    private static bool PreferGroup(OasResourceApi.ResourceItem a,
        OasResourceApi.ResourceItem b)
    {
        var aCore = IdOf(a) < CoreIdCutoff;
        var bCore = IdOf(b) < CoreIdCutoff;
        if (aCore != bCore) return aCore;
        if (a.Count != b.Count) return a.Count > b.Count;
        return IdOf(a) < IdOf(b);
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
