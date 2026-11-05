using System.Diagnostics;
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
    public string PinKey { get; init; } = "";
    public bool IsPinned { get; init; }
    public System.Windows.FrameworkElement PinIcon { get; init; } =
        new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Pin24);
}

/// <summary>
/// 资产页：配置/角色/日期三联下拉 + 图标卡片网格。
/// 复刻 Flutter 版 resource_controller 的筛选管线：
/// 图标白名单（本地 currency 图标） → (图标,名称) 聚合 → |delta| 降序。
/// 190+ 项原始数据 → 十几张用户关心的卡片。
/// </summary>
public partial class ResourcePage : UserControl
{
    private List<string> _dates = [];
    private int _dateIndex = -1;
    private bool _suppressDateEvent;
    private bool _suppressAccountEvent;
    private OasResourceApi.ResourceDay? _currentDay;
    private HashSet<string> _pinned = [];

    /// <summary>视图模式：day=单日快照；"7"/"30"=区间账本（最近 N 天）。</summary>
    private string ViewMode =>
        (ViewSelector.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "day";

    private async void ViewSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DateSelector.IsEnabled = ViewMode == "day";
        await ReloadCurrentViewAsync();
    }

    private const string PinnedKey = "pinnedResources";

    private void LoadPinned()
    {
        var raw = OasSettings.Get(PinnedKey, "");
        _pinned = raw.Length == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(raw.Split(';', StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
    }

    private void SavePinned() =>
        OasSettings.Set(PinnedKey, string.Join(';', _pinned));

    private bool IsPinned(string iconBase) => _pinned.Contains(iconBase);

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
        LoadPinned();
        // ⚠️ 挂在 AssetCards 自身：页面宽度 ≠ 网格可用宽度（导航栏展开/收起
        // 会吃掉宽度，导致列数滞后、右侧留白）。AssetCards 与工具栏同宽，
        // 以它的实际宽度计算列数才能对齐顶栏铺满。
        Loaded += (_, _) => RecomputeCardWidth(AssetCards.ActualWidth);
        AssetCards.SizeChanged += (_, e) => RecomputeCardWidth(e.NewSize.Width);
        _ = LoadAccountsAsync();
    }

    /// <summary>可用宽度 → 列数（卡片最小 260px，2–4 列）→ 卡片宽度。</summary>
    private void RecomputeCardWidth(double available)
    {
        var usable = available - 20;                 // 页边距/滚动条余量
        var cols = Math.Clamp((int)(usable / 230), 2, 4);
        CardWidth = Math.Floor((usable - 8 * (cols - 1)) / cols);
    }

    /// <summary>从全局账号上下文加载账号列表（规范 3.1：页面不自持选择状态）。</summary>
    private async Task LoadAccountsAsync()
    {
        EmptyText.Visibility = Visibility.Collapsed;
        AssetCards.ItemsSource = null;
        var ctx = GlobalAccountContext.Instance;
        // 启动时的账号加载可能仍在进行：订阅完成后补载（否则页面停在空态）
        ctx.AccountsLoaded -= OnAccountsLoadedRefresh;
        ctx.AccountsLoaded += OnAccountsLoadedRefresh;
        if (ctx.Accounts.Count == 0)
        {
            await ctx.LoadAccountsAsync();
        }
        if (ctx.Error is not null)
        {
            StatusText.Text = $"部分调度器账号读取失败：{ctx.Error}";
        }

        // 选项构造与标题栏共用（GlobalAccountContext.BuildOptions），标签保持一致
        AccountSelector.ItemsSource = ctx.BuildOptions();
        AccountSelector.DisplayMemberPath = "Label";
        _suppressAccountEvent = true;
        // 优先选中持久化的当前账号，其次首个
        var target = ctx.Current;
        var idx = target is null ? -1 : ctx.Accounts.ToList().IndexOf(target);
        AccountSelector.SelectedIndex = ctx.Accounts.Count == 0
            ? -1 : (idx >= 0 ? idx : 0);
        _suppressAccountEvent = false;

        // 标题栏切账号 → 本页跟随
        GlobalAccountContext.Instance.CurrentChanged -= OnGlobalAccountChanged;
        GlobalAccountContext.Instance.CurrentChanged += OnGlobalAccountChanged;

        if (AccountSelector.SelectedItem is not null)
        {
            await AccountSelectedAsync();
        }
        else
        {
            EmptyText.Visibility = Visibility.Visible;
        }
    }

    /// <summary>账号切换 → 设全局上下文 → 加载该账号的日期与资产。</summary>
    private async void AccountSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressAccountEvent) return;
        await AccountSelectedAsync();
    }

    /// <summary>账号列表加载完成 → 重新填充（解决启动竞态导致的空态）。</summary>
    private async void OnAccountsLoadedRefresh() => await LoadAccountsAsync();

    /// <summary>标题栏切了账号 → 同步本页下拉并重载（不二次写回上下文）。</summary>
    private async void OnGlobalAccountChanged(AccountEntry? entry)
    {
        if (entry is null) return;
        _suppressAccountEvent = true;
        var options = AccountSelector.ItemsSource as IEnumerable<GlobalAccountContext.AccountOption>;
        AccountSelector.SelectedItem = options?.FirstOrDefault(
            o => o.Entry.Config == entry.Config && o.Entry.RoleKey == entry.RoleKey);
        _suppressAccountEvent = false;
        await AccountSelectedAsync();
    }

    private async Task AccountSelectedAsync()
    {
        if ((AccountSelector.SelectedItem as dynamic)?.Entry is not AccountEntry entry)
        {
            return;
        }
        GlobalAccountContext.Instance.SetCurrent(entry);
        LoadingBarHost(loading: true);
        EmptyText.Visibility = Visibility.Collapsed;
        AssetCards.ItemsSource = null;
        try
        {
            _dates = entry.Dates;
            _suppressDateEvent = true;
            DateSelector.ItemsSource = _dates;
            _dateIndex = _dates.Count > 0 ? 0 : -1;
            DateSelector.SelectedIndex = _dateIndex;
            _suppressDateEvent = false;

            await ReloadCurrentViewAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载日期失败：{ex.Message}";
        }
        finally
        {
            LoadingBarHost(loading: false);
        }
    }

    private void LoadingBarHost(bool loading)
    {
        if (loading) StatusText.Text = "加载中…";
    }

    private string? CurrentDate =>
        _dateIndex >= 0 && _dateIndex < _dates.Count ? _dates[_dateIndex] : null;

    /// <summary>日期切换 → 加载该日明细。</summary>
    private async void DateSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressDateEvent) return;
        await ReloadCurrentViewAsync();
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
            await ReloadCurrentViewAsync();
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
            await ReloadCurrentViewAsync();
        }
    }

    /// <summary>按当前视图模式重载（单日快照 / 区间账本）。</summary>
    private async Task ReloadCurrentViewAsync()
    {
        if ((AccountSelector.SelectedItem as dynamic)?.Entry is not AccountEntry entry)
        {
            EmptyText.Visibility = Visibility.Visible;
            return;
        }
        if (ViewMode != "day")
        {
            await LoadLedgerAsync(entry.Config, entry.RoleKey, int.Parse(ViewMode));
            return;
        }
        if (CurrentDate is string date)
        {
            await LoadDayAsync(entry.Config, date, entry.RoleKey);
        }
        else
        {
            EmptyText.Visibility = Visibility.Visible;
        }
    }

    /// <summary>图标过滤 + (图标,名称) 聚合 + 按图标去重 + 排序。
    /// 单日快照与区间账本共用（Flutter resource_controller._applyIconFilter 移植）。</summary>
    private List<OasResourceApi.ResourceItem> AggregateCards(
        IEnumerable<OasResourceApi.ResourceItem> raw)
    {
        // 1. 图标白名单；count>0 或 delta≠0（账本里"花光"是重要事件，不能丢）
        // 2. (图标,名称) 聚合——同名视为同一种资产的多期版本，数量相加
        var byIconName = raw
            .Where(item => CurrencyIconRegistry.Has(item.Icon)
                           && (item.Count > 0 || (item.Delta ?? 0) != 0))
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

        // 3. 按图标二次去重：同一图标多期/多名称只留一张卡
        //    （优先常驻档 id<901000 → 数量大 → id 小），否则过期代币整屏刷出。
        var byIcon = new Dictionary<string, OasResourceApi.ResourceItem>();
        foreach (var agg in byIconName)
        {
            var icon = CurrencyIconRegistry.BaseName(agg.Icon);
            if (!byIcon.TryGetValue(icon, out var prev) || PreferGroup(agg, prev))
            {
                byIcon[icon] = agg;
            }
        }

        // 排序：置顶优先 → |delta| 降序 → 名称
        return byIcon.Values
            .OrderByDescending(x => IsPinned(CurrencyIconRegistry.BaseName(x.Icon)))
            .ThenByDescending(x => Math.Abs(x.Delta ?? 0))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>区间账本：最近 N 天首尾对比 → 卡片网格。</summary>
    private async Task LoadLedgerAsync(string config, string roleKey, int days)
    {
        EmptyText.Visibility = Visibility.Collapsed;
        try
        {
            var ledger = await OasResourceApi.GetLedgerAsync(config, roleKey, days);
            if (ledger is null || ledger.Items.Count == 0)
            {
                AssetCards.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                StatusText.Text = "账本：区间内无数据";
                return;
            }

            var visible = AggregateCards(ledger.Items.Select(i =>
                new OasResourceApi.ResourceItem(i.Id, i.Name, i.Icon, i.Count, i.Delta)));

            var up = visible.Count(x => (x.Delta ?? 0) > 0);
            var down = visible.Count(x => (x.Delta ?? 0) < 0);
            var baseNote = ledger.BaseDate.Length > 0
                ? $"基线 {ledger.BaseDate}"
                : "基线缺失";
            StatusText.Text =
                $"账本 {ledger.FirstDate} → {ledger.LastDate}（{baseNote}）· " +
                $"涨 {up} 项 / 跌 {down} 项 · 共 {visible.Count} 项";

            AssetCards.ItemsSource = visible.Select(item =>
            {
                var pinKey = CurrencyIconRegistry.BaseName(item.Icon);
                var pinned = IsPinned(pinKey);
                return new AssetCardVm
                {
                    Name = item.Name,
                    CountText = FormatCount(item.Count),
                    DeltaText = item.Delta is { } d && d != 0 ? FormatDelta(d) : "持平",
                    DeltaBrush = new SolidColorBrush((item.Delta ?? 0) >= 0
                        ? Color.FromRgb(0x4C, 0xAF, 0x50)
                        : Color.FromRgb(0xE5, 0x53, 0x53)),
                    IconImage = LoadIcon(item.Icon),
                    PinKey = pinKey,
                    IsPinned = pinned,
                    PinIcon = new Wpf.Ui.Controls.SymbolIcon(
                        Wpf.Ui.Controls.SymbolRegular.Pin24)
                    {
                        Foreground = pinned
                            ? FindBrush("OasAccentBrush")
                            : FindBrush("OasTextTertiaryBrush"),
                    },
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"账本加载失败：{ex.Message}";
        }
    }

    /// <summary>加载某天的资产明细并渲染图标卡。</summary>
    private async Task LoadDayAsync(string config, string date, string roleKey)
    {
        EmptyText.Visibility = Visibility.Collapsed;
        try
        {
            var day = await OasResourceApi.GetResourceDayAsync(config, date, roleKey);
            _currentDay = day;
            if (day is null || day.Items.Count == 0)
            {
                AssetCards.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                StatusText.Text = $"{date} 无数据";
                return;
            }

            var visible = AggregateCards(day.Items);

            AssetCards.ItemsSource = visible.Select(item =>
            {
                var pinKey = CurrencyIconRegistry.BaseName(item.Icon);
                var pinned = IsPinned(pinKey);
                return new AssetCardVm
                {
                    Name = item.Name,
                    CountText = FormatCount(item.Count),
                    DeltaText = item.Delta is { } d && d != 0 ? FormatDelta(d) : "",
                    DeltaBrush = new SolidColorBrush((item.Delta ?? 0) >= 0
                        ? Color.FromRgb(0x4C, 0xAF, 0x50)
                        : Color.FromRgb(0xE5, 0x53, 0x53)),
                    IconImage = LoadIcon(item.Icon),
                    PinKey = pinKey,
                    IsPinned = pinned,
                    // 已置顶：图钉 accent 蓝（未置顶：中性灰）
                    PinIcon = new Wpf.Ui.Controls.SymbolIcon(
                        Wpf.Ui.Controls.SymbolRegular.Pin24)
                    {
                        Foreground = pinned
                            ? FindBrush("OasAccentBrush")
                            : FindBrush("OasTextTertiaryBrush"),
                    },
                };
            }).ToList();
            StatusText.Text = $"{date} · {visible.Count} 项资产";

        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败：{ex.Message}";
        }
    }

    /// <summary>生成资产分享图（纯前端渲染，保存到「图片\OASX」）。</summary>
    /// <summary>分享链路日志（排障用）。</summary>
    private static void ShareLog(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "share.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {msg}" + Environment.NewLine);
        }
        catch
        {
            // 日志失败不影响功能
        }
    }

    private DateTime _lastShareAt = DateTime.MinValue;

    private void Share_Click(object sender, RoutedEventArgs e)
    {
        ShareLog("enter");
        // 防抖：1.5s 内重复点击（含自动化误触）直接忽略，
        // 避免连续渲染 + 连续弹出资源管理器
        if ((DateTime.Now - _lastShareAt).TotalMilliseconds < 1500)
        {
            ShareLog("debounced");
            return;
        }
        _lastShareAt = DateTime.Now;
        try
        {
            if (AssetCards.ItemsSource is not IEnumerable<AssetCardVm> cards)
            {
                ShareLog("early: no items source");
                StatusText.Text = "没有可分享的资产数据";
                return;
            }
            if ((AccountSelector.SelectedItem as dynamic)?.Entry is not AccountEntry entry)
            {
                ShareLog("early: no account");
                StatusText.Text = "先选择账号";
                return;
            }
            ShareLog($"account={entry.Config}|{entry.RoleKey}");
            var date = CurrentDate ?? DateTime.Today.ToString("yyyy-MM-dd");
            var items = cards.Select(c => (
                Name: c.Name,
                Count: c.CountText,
                Delta: c.DeltaText.TrimStart('+'),
                Up: c.DeltaText.StartsWith('+'))).ToList();
            if (items.Count == 0)
            {
                StatusText.Text = "没有可分享的资产数据";
                return;
            }
            ShareLog($"items={items.Count} date={date} render start");
            var path = ShareImageRenderer.RenderAssets(
                entry.Name, $"Lv{entry.Level} · {entry.ServerName}",
                date, items);
            ShareLog($"render done: {path}");
            StatusText.Text = $"分享图已保存：{path}";
            // 路径入剪贴板：无论能否拉起资源管理器，用户都能直接粘贴定位
            // 路径入剪贴板：无论能否打开目录，用户都能直接粘贴定位
            // （剪贴板可能被其他程序占用 CLIPBRD_E_CANT_OPEN → 重试）
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Clipboard.SetText(path);
                    ShareLog("path copied to clipboard");
                    break;
                }
                catch (Exception ex)
                {
                    ShareLog($"clipboard failed ({attempt}/3): {ex.Message}");
                    if (attempt < 3)
                    {
                        System.Threading.Thread.Sleep(120);
                    }
                }
            }

            // 预览弹窗：**不启动 explorer**——它在本机受限环境下以 0xc0000142
            // 初始化失败，Windows 会给崩掉的子进程弹系统错误框，
            // 被误认为 OASX 崩溃。改为应用内预览 + 复制路径。
            ShareLog("showing preview dialog");
            SharePreviewDialog.Show(Window.GetWindow(this), path);
            ShareLog("preview closed");
        }
        catch (Exception ex)
        {
            ShareLog("EXCEPTION: " + ex);
            StatusText.Text = $"分享图生成失败：{ex.Message}";
        }
    }

    /// <summary>点卡片 → 涨势图弹窗（后端按名称跨期聚合）。</summary>
    private async void AssetCard_Click(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string itemName }) return;
        if ((AccountSelector.SelectedItem as dynamic)?.Entry is not AccountEntry entry)
        {
            return;
        }
        try
        {
            var points = await OasResourceApi.GetHistoryAsync(
                entry.Config, entry.RoleKey, itemName);
            ResourceHistoryDialog.Show(Window.GetWindow(this), itemName, points);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"历史数据加载失败：{ex.Message}";
        }
    }

    /// <summary>图钉切换：更新置顶集合并重排。</summary>
    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string pinKey })
        {
            if (_pinned.Contains(pinKey))
            {
                _pinned.Remove(pinKey);
            }
            else
            {
                _pinned.Add(pinKey);
            }
            SavePinned();
            if (_currentDay is not null && _currentDay.Items.Count > 0)
            {
                ReRenderCards(_currentDay);
            }
        }
    }

    /// <summary>用缓存的天数据重跑筛选/置顶/排序管线（不重新请求后端）。</summary>
    private void ReRenderCards(OasResourceApi.ResourceDay day)
    {
        if ((AccountSelector.SelectedItem as dynamic)?.Entry is not AccountEntry entry) return;
        var date = CurrentDate ?? day.Date;
        _ = LoadDayAsync(entry.Config, date, entry.RoleKey);
    }

    /// <summary>按键取主题画刷（语义色）。</summary>
    private static Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

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
