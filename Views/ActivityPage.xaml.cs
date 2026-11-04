using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

/// <summary>每日任务行。</summary>
public sealed class DailyTaskVm
{
    public string Name { get; init; } = "";
    public string ProgressText { get; init; } = "";
    public string ScoreText { get; init; } = "";
    public Wpf.Ui.Controls.SymbolRegular StateSymbol { get; init; }
    public Brush StateBrush { get; init; } = Brushes.Gray;
}

/// <summary>进行中时段行（游戏内部 key + 结束时间）。</summary>
public sealed class LiveWindowVm
{
    public string Label { get; init; } = "";
    public string EndText { get; init; } = "";
}

/// <summary>当前活动行。</summary>
public sealed class CurrentActivityVm
{
    public string Name { get; init; } = "";
    public string EndText { get; init; } = "";
}

/// <summary>周常清单条目（社区整理；游戏内无面板，本地勾选）。</summary>
public sealed class WeeklyItemVm : System.ComponentModel.INotifyPropertyChanged
{
    private bool _done;

    public int Id { get; init; }
    public string Label { get; init; } = "";
    public string Note { get; init; } = "";

    public bool Done
    {
        get => _done;
        set
        {
            if (_done == value)
            {
                return;
            }
            _done = value;
            PropertyChanged?.Invoke(this, new(nameof(Done)));
            DoneChanged?.Invoke();
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public event Action? DoneChanged;
}

/// <summary>悬赏封印行。</summary>
public sealed class MonsterTaskVm
{
    public string Label { get; init; } = "";
    public string ProgressText { get; init; } = "";
    public Wpf.Ui.Controls.SymbolRegular StateSymbol { get; init; }
    public Brush StateBrush { get; init; } = Brushes.Gray;
}

/// <summary>
/// 活动页：每日任务 + 悬赏封印（完成状态由游戏客户端数据自动判定），
/// 活动任务需要游戏内打开活动面板后才可读（客户端懒加载，与同类实现一致）。
/// </summary>
public partial class ActivityPage : UserControl
{
    private readonly System.Windows.Threading.DispatcherTimer _autoRefresh = new()
    {
        Interval = TimeSpan.FromSeconds(60),
    };

    private bool _lastHasTasks;
    private readonly List<WeeklyItemVm> _weekly = [];

    /// <summary>社区整理版周常清单（网易大神/NGA/知乎 多来源一致条目）。</summary>
    private static readonly (int Id, string Label, string Note)[] WeeklySeed =
    [
        (1, "花合战 100 活跃度", "周活跃度奖励"),
        (2, "每周秘闻副本", "通关；首次分享 +20 勾"),
        (3, "斗技 / 协同斗技", "周结算 60-700 勾 / 60-300 勾"),
        (4, "地域鬼王", "每日 20-60 勾；周首次分享 +20 勾"),
        (5, "寮三十（寮集体任务）", "每日任务里有计数，可对照"),
        (6, "寮资金 / 寮包领取", "寮界面领取"),
        (7, "狩猎战 / 阴界之门", "按周/日时段开放"),
        (8, "逢魔之时", "每日 19:00-21:00"),
        (9, "御魂副本清次数", "周次数与体力规划"),
        (10, "结界突破 / 寄养结界卡", "结界卡产出资源"),
        (11, "道馆", "每日开放时段"),
        (12, "大神 App 签到（周满勤）", "满勤给黑蛋碎片"),
        (13, "功勋商店周礼包", "30 功勋 7 个/周；蓝票 120 功勋 1 张/周"),
    ];

    public ActivityPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var ctx = GlobalAccountContext.Instance;
            ctx.CurrentChanged -= OnContextChanged;
            ctx.CurrentChanged += OnContextChanged;
            ctx.AccountsLoaded -= OnAccountsLoaded;
            ctx.AccountsLoaded += OnAccountsLoaded;
            _autoRefresh.Tick += OnAutoRefreshTick;
            _autoRefresh.Start();
            LoadWeekly();
            _ = LoadAsync();
        };
        Unloaded += (_, _) =>
        {
            var ctx = GlobalAccountContext.Instance;
            ctx.CurrentChanged -= OnContextChanged;
            ctx.AccountsLoaded -= OnAccountsLoaded;
            _autoRefresh.Stop();
            _autoRefresh.Tick -= OnAutoRefreshTick;
        };
    }

    private void OnContextChanged(AccountEntry? entry) => _ = LoadAsync();

    private void OnAccountsLoaded() => _ = LoadAsync();

    private void OnAutoRefreshTick(object? sender, EventArgs e)
    {
        // 有任务数据时才自动刷新（活动/日常进度会变；无数据说明游戏没开，别空跑）
        if (_lastHasTasks && GlobalAccountContext.Instance.Current is not null)
        {
            _ = LoadAsync(force: true);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await LoadAsync(force: true);

    // ==================== 周常清单（本地，周一 5:00 重置） ====================

    private const string WeeklyKey = "weeklyChecklist";

    /// <summary>本周标识：以周一 5:00 为界（yyyy-Www）。</summary>
    private static string CurrentWeekKey()
    {
        var now = DateTime.Now;
        var anchor = now.Date.AddHours(5);
        if (now < anchor)
        {
            anchor = anchor.AddDays(-1);
        }
        var monday = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
        var week = System.Globalization.ISOWeek.GetWeekOfYear(monday);
        return $"{monday:yyyy}-W{week:00}";
    }

    private void LoadWeekly()
    {
        var week = CurrentWeekKey();
        var done = new HashSet<int>();
        try
        {
            var raw = OasSettings.Get(WeeklyKey, "");
            if (raw.Length > 0)
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("week", out var w) && w.GetString() == week &&
                    root.TryGetProperty("done", out var d) &&
                    d.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in d.EnumerateArray())
                    {
                        if (item.TryGetInt32(out var id))
                        {
                            done.Add(id);
                        }
                    }
                }
            }
        }
        catch
        {
            // 解析失败按空处理
        }

        _weekly.Clear();
        foreach (var (id, label, note) in WeeklySeed)
        {
            var vm = new WeeklyItemVm
            {
                Id = id,
                Label = label,
                Note = note,
                Done = done.Contains(id),
            };
            vm.DoneChanged += SaveWeekly;
            _weekly.Add(vm);
        }
        WeeklyList.ItemsSource = null;
        WeeklyList.ItemsSource = _weekly;
        UpdateWeeklySummary();
    }

    private void SaveWeekly()
    {
        try
        {
            OasSettings.Set(WeeklyKey, JsonSerializer.Serialize(new
            {
                week = CurrentWeekKey(),
                done = _weekly.Where(x => x.Done).Select(x => x.Id).ToArray(),
            }));
        }
        catch
        {
            // 保存失败不影响勾选
        }
        UpdateWeeklySummary();
    }

    private void UpdateWeeklySummary()
    {
        WeeklySummaryText.Text = $"{_weekly.Count(x => x.Done)}/{_weekly.Count}" +
                                 $"（{CurrentWeekKey()}）";
    }

    private async Task LoadAsync(bool force = false)
    {
        var ctx = GlobalAccountContext.Instance;
        if (ctx.Accounts.Count == 0 && !ctx.IsLoading)
        {
            await ctx.LoadAccountsAsync();
        }
        var entry = ctx.Current;
        if (entry is null)
        {
            AccountText.Text = "未选择账号";
            ServerText.Text = "在资产页或左下角选择账号";
            DailyList.ItemsSource = null;
            MonsterList.ItemsSource = null;
            ActivityText.Text = "—";
            DailySummaryText.Text = "—";
            MonsterSummaryText.Text = "—";
            return;
        }

        AccountText.Text = entry.DisplayName;
        ServerText.Text = $"Lv{entry.Level} · {entry.ServerName} · 调度器 {entry.Config}";

        try
        {
            var board = await OasActivityApi.GetBoardAsync(entry.Config, force);
            if (board is null || !board.Success)
            {
                StatusText.Text = board?.Error ?? "读取失败";
                if (!string.IsNullOrEmpty(board?.Hint))
                {
                    StatusText.Text += $"（{board.Hint}）";
                }
                SubtitleText.Text = "需要游戏运行才能读取任务进度";
                _lastHasTasks = false;
                return;
            }

            _lastHasTasks = (board.Daily?.Count ?? 0) > 0;
            SubtitleText.Text = board.ServerWeekDay > 0
                ? $"每日/每周任务 · 完成状态自动判定（服务器时间 {board.ServerTimeText}）"
                : "每日/每周任务 · 完成状态自动判定";

            DailySummaryText.Text = board.DailyTotal > 0
                ? $"{board.DailyDone}/{board.DailyTotal}"
                : "—";
            if (board.DailyTotal == 0)
            {
                // 刚过 0 点时客户端会清空 daily_task，需游戏内刷新后才重新填充
                StatusText.Text = "今日任务尚未刷新——在游戏内打开一次「每日任务」后点刷新";
            }
            MonsterSummaryText.Text = $"{board.MonsterDone}/{board.Monster?.Count ?? 0}";

            DailyList.ItemsSource = (board.Daily ?? []).Select(t => new DailyTaskVm
            {
                Name = t.Name,
                ProgressText = t.ProgressText,
                ScoreText = t.Score > 0 ? $"+{t.Score}" : "",
                StateSymbol = t.Done
                    ? Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24
                    : Wpf.Ui.Controls.SymbolRegular.Circle24,
                StateBrush = t.Done
                    ? Brush("OasSuccessBrush", Color.FromRgb(0x16, 0xA3, 0x4A))
                    : Brush("OasTextSecondaryBrush", Color.FromRgb(0x61, 0x61, 0x61)),
            }).ToList();

            MonsterList.ItemsSource = (board.Monster ?? []).Select((m, i) => new MonsterTaskVm
            {
                Label = $"悬赏 #{i + 1}（类型 {m.Type?.ToString() ?? "-"}）",
                ProgressText = $"{m.Process}/{m.Need}",
                StateSymbol = m.Fin
                    ? Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24
                    : Wpf.Ui.Controls.SymbolRegular.Circle24,
                StateBrush = m.Fin
                    ? Brush("OasSuccessBrush", Color.FromRgb(0x16, 0xA3, 0x4A))
                    : Brush("OasTextSecondaryBrush", Color.FromRgb(0x61, 0x61, 0x61)),
            }).ToList();

            // 进行中的活动时段（只有内部 key，客户端无中文名）
            var live = (board.LiveWindows ?? [])
                .OrderBy(w => w.End)
                .Select(w => new LiveWindowVm
                {
                    Label = w.Key,
                    EndText = $"至 {w.EndText}",
                })
                .ToList();
            LiveList.ItemsSource = live;
            LiveEmptyText.Visibility = live.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            // 当前活动（名称来自客户端活动表）
            ActivityList.ItemsSource = (board.CurrentActivities ?? [])
                .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .Select(a => new CurrentActivityVm
                {
                    Name = a.Name,
                    EndText = FormatEnd(a.End),
                })
                .ToList();

            // 阴阳寮
            var g = board.Guild;
            GuildText.Text = g is null || string.IsNullOrEmpty(g.Name)
                ? "—（游戏内需先加入阴阳寮/打开寮页面）"
                : $"{g.Name}　Lv{g.Lv}　职位 {DutyName(g.Duty)}"
                  + (g.GymLeftChallenge is { } left ? $"　宴会剩余挑战 {left:0}" : "")
                  + (g.GiftBagWeekTimes is { } gb ? $"　周礼包 {gb}" : "");

            ActivityText.Text = board.ActivityAvailable && board.Activity?.Count > 0
                ? string.Join("\n", board.Activity)
                : "活动面板任务需要先在游戏内打开「活动」页面读取一次（客户端懒加载），之后刷新即可显示。";

            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"读取失败：{ex.Message}";
            _lastHasTasks = false;
        }
    }

    /// <summary>"(2026, 11, 8, 0, 0, 0, 0, 0, -1)" → "11-08 截止"。</summary>
    private static string FormatEnd(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "";
        }
        var digits = raw.Trim('(', ')').Split(',')
            .Select(x => x.Trim())
            .Where(x => int.TryParse(x, out _))
            .Select(int.Parse)
            .ToArray();
        return digits.Length >= 3
            ? $"{digits[1]:00}-{digits[2]:00} 截止"
            : "";
    }

    /// <summary>寮职位：3=会长/副会长？客户端只给数字，按常见对应展示。</summary>
    private static string DutyName(int? duty) => duty switch
    {
        1 => "会长",
        2 => "副会长",
        3 => "成员",
        _ => duty?.ToString() ?? "-",
    };

    private static Brush Brush(string key, Color fallback) =>
        Application.Current.TryFindResource(key) as Brush
        ?? new SolidColorBrush(fallback);
}