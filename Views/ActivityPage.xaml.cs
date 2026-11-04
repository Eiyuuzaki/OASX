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

    private string _signalText = "";

    public int Id { get; init; }
    public string Label { get; init; } = "";
    public string Note { get; init; } = "";

    /// <summary>反推依据（来自客户端数据），空则不显示。</summary>
    public string SignalText
    {
        get => _signalText;
        set
        {
            if (_signalText == value)
            {
                return;
            }
            _signalText = value;
            PropertyChanged?.Invoke(this, new(nameof(SignalText)));
            PropertyChanged?.Invoke(this, new(nameof(SignalVisibility)));
        }
    }

    public System.Windows.Visibility SignalVisibility =>
        string.IsNullOrEmpty(_signalText)
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

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

    /// <summary>周常清单（按用户意见精简）。
    /// 可自动判定的三项（寮三十/狩猎战/逢魔）由每日任务与活动时段反推；
    /// 御魂次数客户端无计数（只有体力/层数进度），保持手动。</summary>
    private static readonly (int Id, string Label, string Note)[] WeeklySeed =
    [
        (1, "花合战 100 活跃度", "周活跃度奖励"),
        (2, "每周秘闻副本", "通关；首次分享 +20 勾"),
        (3, "斗技", "周结算按段位给勾（上周段位见下）"),
        (4, "地域鬼王", "每日 20-60 勾；周首次分享 +20 勾"),
        (5, "寮三十（寮集体任务）", "自动：取每日任务「集体任务」进度"),
        (6, "寮资金 / 寮包领取", "寮界面领取"),
        (7, "狩猎战 / 阴界之门", "自动：取每日任务「狩猎战/阴界之门」"),
        (8, "逢魔之时", "每日晚间开放；客户端无可判定字段，手动记录"),
        (9, "御魂副本", "客户端无次数计数，手动记录"),
        (10, "功勋商店周礼包", "30 功勋 7 个/周；蓝票 120 功勋 1 张/周"),
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
            _ = LoadFosterAsync();
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
        _ = LoadFosterAsync();
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
                    if (root.TryGetProperty("manual", out var mm) &&
                        mm.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in mm.EnumerateArray())
                        {
                            if (item.TryGetInt32(out var mid))
                            {
                                _manualIds.Add(mid);
                            }
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
            var id2 = id;
            vm.DoneChanged += () =>
            {
                if (!_applyingSignals)
                {
                    _manualIds.Add(id2);
                }
                SaveWeekly();
            };
            _weekly.Add(vm);
        }
        WeeklyList.ItemsSource = null;
        WeeklyList.ItemsSource = _weekly;
        UpdateWeeklySummary();
    }

    /// <summary>用户手动勾/取消过的条目（自动判定不再覆盖）。</summary>
    private readonly HashSet<int> _manualIds = [];

    /// <summary>正在应用自动判定（此时不算用户手动操作）。</summary>
    private bool _applyingSignals;

    private void SaveWeekly()
    {
        try
        {
            OasSettings.Set(WeeklyKey, JsonSerializer.Serialize(new
            {
                week = CurrentWeekKey(),
                done = _weekly.Where(x => x.Done).Select(x => x.Id).ToArray(),
                manual = _manualIds.ToArray(),
            }));
        }
        catch
        {
            // 保存失败不影响勾选
        }
        UpdateWeeklySummary();
    }

    /// <summary>把客户端数据反推成周常依据；可自动判定的三项直接打勾
    /// （用户手动取消过的条目不再自动改，记录在 settings.manual 里）。</summary>
    private void ApplyWeeklySignals(OasActivityApi.WeeklySignals? sig,
        OasActivityApi.ActivityBoard? board)
    {
        foreach (var item in _weekly)
        {
            item.SignalText = "";
        }
        if (sig is null && board is null)
        {
            return;
        }

        void Set(int id, string text)
        {
            var item = _weekly.FirstOrDefault(x => x.Id == id);
            if (item is not null && !string.IsNullOrEmpty(text))
            {
                item.SignalText = text;
            }
        }

        // 自动判定：从每日任务里找对应项
        var daily = board?.Daily ?? [];
        (bool Done, string Text) FromDaily(string keyword)
        {
            var hit = daily.FirstOrDefault(t =>
                t.Name.Contains(keyword, StringComparison.Ordinal));
            return hit is null
                ? (false, "")
                : (hit.Done, $"{hit.Name} {hit.ProgressText}");
        }

        // 花合战
        if (sig?.BattlepassLv is { Count: > 0 })
        {
            Set(1, $"花合战等级 {sig.BattlepassLv.Values.Max()}");
        }
        // 斗技
        if (sig is not null)
        {
            Set(3, $"积分 {sig.PvpScore}　上周段位 {sig.LastWeekPvpStage}");
        }
        // 寮三十（每日任务「集体任务」）
        var guild = FromDaily("集体任务");
        Set(5, guild.Text);
        // 狩猎战 / 阴界之门
        var hunt = FromDaily("狩猎战");
        Set(7, hunt.Text);
        // 御魂：用每日任务里「探索/御魂/觉醒」那条作参考
        var yuhun = FromDaily("御魂");
        Set(9, yuhun.Text);

        // 逢魔之时：用户确认 evening_event（17:00-23:00）就是逢魔时段。
        // 时段开着只代表"可以做"，不代表"已参与"——只做提醒，不自动勾选。
        var fengmo = (board?.LiveWindows ?? [])
            .FirstOrDefault(w => w.Key == "evening_event");
        Set(8, fengmo is not null
            ? $"开放中，至 {fengmo.EndText}"
            : "每日 17:00-23:00 开放");

        // 自动勾选：寮三十 / 狩猎战（用户手动取消过的不覆盖）
        // ⚠️ 逢魔不做完成判定：候选信号 sfv_daily_fengmo 语义未验证
        AutoSet(5, guild.Done);
        AutoSet(7, hunt.Done);

        void AutoSet(int id, bool done)
        {
            var item = _weekly.FirstOrDefault(x => x.Id == id);
            if (item is null || !done || _manualIds.Contains(id) || item.Done)
            {
                return;
            }
            _applyingSignals = true;
            try
            {
                item.Done = true;
            }
            finally
            {
                _applyingSignals = false;
            }
        }
    }

    private void UpdateWeeklySummary()
    {
        WeeklySummaryText.Text = $"{_weekly.Count(x => x.Done)}/{_weekly.Count}" +
                                 $"（{CurrentWeekKey()}）";
    }

    // ==================== 结界寄养 ====================

    /// <summary>读寄养倒计时（reward_t + 周期），到点显示收取提示。</summary>
    private async Task LoadFosterAsync()
    {
        try
        {
            var state = await OasNotifyApi.GetFosterAsync();
            if (state is null || !state.Success)
            {
                FosterText.Text = state?.Error ?? "读取失败";
                FosterSubText.Text = "需要游戏运行且已进主界面";
                return;
            }
            var remain = state.RemainingSeconds;
            if (remain <= 0)
            {
                FosterText.Text = "✅ 寄养已成熟，快去收取！";
                FosterText.Foreground = Brush("OasSuccessBrush",
                    System.Windows.Media.Color.FromRgb(0x16, 0xA3, 0x4A));
                FosterSubText.Text = $"成熟时刻 {state.HarvestTimeText}" +
                                     (state.NotifyEnabled ? " · 已推送通知" : "");
                return;
            }
            FosterText.Foreground = Brush("OasTextPrimaryBrush",
                System.Windows.Media.Color.FromRgb(0x1F, 0x1F, 0x1F));
            var ts = TimeSpan.FromSeconds(remain);
            FosterText.Text = $"⏳ 距离成熟还有 {ts.Hours}小时{ts.Minutes}分" +
                              $"（{state.HarvestTimeText} 收取）";
            FosterSubText.Text = $"上次寄养 {state.RewardTimeText} · 周期 {state.CycleHours} 小时" +
                                 (state.NotifyEnabled
                                     ? " · 到点将推送通知"
                                     : " · 通知未启用（设置页可开启）");
        }
        catch (Exception ex)
        {
            FosterText.Text = $"读取失败：{ex.Message}";
        }
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
            ApplyWeeklySignals(board.WeeklySignals, board);

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