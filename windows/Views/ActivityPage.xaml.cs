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
    /// <summary>游戏内部 key（重命名以它为键）。</summary>
    public string Key { get; init; } = "";
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
/// 活动任务需要游戏内打开活动面板后才可读（客户端懒加载，需先在游戏内打开活动面板一次）。
/// </summary>
public partial class ActivityPage : UserControl
{
    private readonly System.Windows.Threading.DispatcherTimer _autoRefresh = new()
    {
        Interval = TimeSpan.FromSeconds(60),
    };

    private bool _lastHasTasks;
    /// <summary>最近一次成功加载的数据（分享图用，避免再次请求）。</summary>
    private OasActivityApi.ActivityBoard? _lastBoard;
    private OasNotifyApi.FosterState? _lastFoster;
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

    /// <summary>生成活动/寄养摘要分享图，并弹出预览。</summary>
    private void ShareActivity_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var entry = GlobalAccountContext.Instance.Current;
            var board = _lastBoard;
            var foster = _lastFoster;
            if (board is null && foster is null)
            {
                StatusText.Text = "还没有可分享的数据，先刷新一次";
                return;
            }

            var rows = new List<(string Label, string Value, string Note)>();
            if (board is not null)
            {
                rows.Add(("每日任务", $"{board.DailyDone}/{board.DailyTotal}",
                    "满 100 活跃度"));
                rows.Add(("悬赏封印", $"{board.MonsterDone}/{board.Monster?.Count ?? 0}",
                    "封印完成"));
                rows.Add(("每周任务", $"{WeeklyDoneCount()}/{_weekly.Count}",
                    $"本周 {CurrentWeekKey()}"));
                var live = board.LiveWindows ?? [];
                if (live.Count > 0)
                {
                    var first = live.OrderBy(w => w.End).First();
                    rows.Add(("进行中时段", $"{live.Count} 个",
                        $"最近结束 {first.EndText}"));
                }
            }
            if (foster is not null)
            {
                var f = foster.Foster;
                if (f is not null && f.Active != false && f.Remaining is not null)
                {
                    var ts = TimeSpan.FromSeconds(f.Remaining ?? 0);
                    rows.Add((f.CardName ?? "寄养",
                        (f.Remaining ?? 0) <= 0 ? "已成熟" : $"{ts.Hours}小时{ts.Minutes:00}分",
                        $"{f.EndText} 收取"));
                }
                else
                {
                    rows.Add(("寄养", "未寄养", ""));
                }
                var own = foster.Own;
                if (own is not null && own.Empty == true)
                {
                    rows.Add(("自结界卡", "空", "自己放卡后显示倒计时"));
                }
                else if (own is not null && own.Unknown != true && own.Remaining is not null)
                {
                    var ts2 = TimeSpan.FromSeconds(own.Remaining ?? 0);
                    rows.Add(("自结界卡", $"{ts2.Hours}小时{ts2.Minutes:00}分",
                        $"{own.EndText} 收取"));
                }
            }

            var path = ShareImageRenderer.RenderSummary(
                "活动与寄养",
                entry?.DisplayName ?? "未选择账号",
                entry is null ? "" : $"调度器 {entry.Config}",
                $"服务器时间 {board?.ServerTimeText ?? "—"}",
                rows,
                "OASX活动");
            SharePreviewDialog.Show(Window.GetWindow(this), path);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"分享图生成失败：{ex.Message}";
        }
    }

    private int WeeklyDoneCount() =>
        _weekly.Count(x => x.Done);

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

    /// <summary>读寄养/自结界卡双槽位（数据来自游戏内嵌解释器）。</summary>
    /// <summary>读寄养/自结界卡双槽位（游戏内嵌解释器），填充两张圆环卡。</summary>
    private async Task LoadFosterAsync()
    {
        try
        {
            var state = await OasNotifyApi.GetFosterAsync();
            if (state is null || !state.Success)
            {
                FillFosterCard("需要游戏运行", "", "", "", 0, null, false);
                FillOwnCard("需要游戏运行", "", "", 0, null, false);
                FosterNotifyText.Text = "";
                return;
            }

            _lastFoster = state;
            var liveDesc = DescribeLiveRole(state.LiveRole);
            FosterNotifyText.Text = state.NotifyEnabled ? "通知已启用" : "通知未启用";

            // ---- 寄养 ----
            var foster = state.Foster;
            if (foster is null || foster.Active == false || foster.Remaining is null)
            {
                FillFosterCard("当前未寄养", liveDesc,
                    state.NotifyEnabled ? "到点将推送通知" : "通知未启用（设置页可开启）",
                    "", 0, null, false);
            }
            else
            {
                var remain = foster.Remaining ?? 0;
                var total = (foster.Hours ?? 6) * 3600.0;
                var name = (foster.CardName ?? "").Replace("/", " · ");
                var matured = remain <= 0;
                var ts = TimeSpan.FromSeconds(remain);
                FillFosterCard(name, liveDesc,
                    state.NotifyEnabled ? "到点将推送通知" : "通知未启用（设置页可开启）",
                    matured ? $"{foster.EndText} 可收取" : $"{foster.EndText} 收取",
                    Math.Clamp(1 - remain / total, 0, 1),
                    IconKeyOf(foster.CardName), !matured,
                    matured ? "已成熟" : $"{ts.Hours}小时{ts.Minutes:00}分");
            }

            // ---- 自结界卡 ----
            var own = state.Own;
            if (own is null || own.Unknown == true)
            {
                FillOwnCard("未读取", "", "打开一次结界页后可显示", 0, null, false);
            }
            else if (own.Error is not null)
            {
                FillOwnCard("读取失败", "", "", 0, null, false);
            }
            else if (own.Empty == true)
            {
                FillOwnCard("空", "", "自己放卡后会显示倒计时", 0, null, false);
            }
            else
            {
                var nameO = (own.CardName ?? own.CardItem ?? "").Replace("/", " · ");
                var maturedO = (own.Remaining ?? 0) <= 0;
                var tsO = TimeSpan.FromSeconds(own.Remaining ?? 0);
                FillOwnCard(nameO, maturedO ? "可收取" : $"{tsO.Hours}小时{tsO.Minutes:00}分后成熟",
                    maturedO ? $"{own.EndText} 可收取" : $"{own.EndText} 收取",
                    Math.Clamp(1 - (own.Remaining ?? 0) / ((own.Hours ?? 6) * 3600.0), 0, 1),
                    IconKeyOf(own.CardName), !maturedO);
            }
        }
        catch (Exception ex)
        {
            FosterNotifyText.Text = $"读取失败：{ex.Message}";
            FillFosterCard("需要游戏运行", "", "", "", 0, null, false);
            FillOwnCard("需要游戏运行", "", "", 0, null, false);
        }
    }

    /// <summary>寄养卡填充。</summary>
    private void FillFosterCard(string name, string liveText, string notifyText,
        string endText, double fraction, string? iconKey, bool running, string countdown = "")
    {
        ActFosterName.Text = name;
        ActFosterLive.Text = liveText;
        ActFosterNotify.Text = notifyText;
        ActFosterCountdown.Text = countdown;
        ActFosterEnd.Text = endText;
        SetRing(ActFosterRing, fraction);
        SetCardIcon(ActFosterIcon, ActFosterPlaceholder, iconKey);
        ActFosterCountdown.Foreground = new System.Windows.Media.SolidColorBrush(running
            ? System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB)
            : System.Windows.Media.Color.FromRgb(0x16, 0xA3, 0x4A));
    }

    /// <summary>自结界卡填充。</summary>
    private void FillOwnCard(string name, string countdown, string note,
        double fraction, string? iconKey, bool running)
    {
        ActOwnName.Text = name;
        ActOwnNote.Text = note;
        ActOwnCountdown.Text = countdown;
        ActOwnEnd.Text = "";
        SetRing(ActOwnRing, fraction);
        SetCardIcon(ActOwnIcon, ActOwnPlaceholder, iconKey);
        ActOwnCountdown.Foreground = new System.Windows.Media.SolidColorBrush(running
            ? System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB)
            : System.Windows.Media.Color.FromRgb(0x16, 0xA3, 0x4A));
    }

    /// <summary>卡名 → 内置图标 key。</summary>
    private static string? IconKeyOf(string? cardName)
    {
        if (string.IsNullOrEmpty(cardName))
        {
            return null;
        }
        if (cardName.Contains("斗鱼"))
        {
            return "realm_douyu";
        }
        if (cardName.Contains("太鼓"))
        {
            return "realm_taiko";
        }
        if (cardName.Contains("太阴"))
        {
            return "realm_taiyin";
        }
        if (cardName.Contains("伞室内"))
        {
            return "realm_sanshinei";
        }
        return null;
    }

    private static void SetCardIcon(System.Windows.Controls.Image icon,
        System.Windows.UIElement? placeholder, string? iconKey)
    {
        if (iconKey is not null && iconKey.Length > 0)
        {
            icon.Source = new System.Windows.Media.Imaging.BitmapImage(
                new Uri($"pack://application:,,,/Assets/Icons/{iconKey}.png"));
            icon.Visibility = Visibility.Visible;
            if (placeholder is not null) placeholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            icon.Source = null;
            icon.Visibility = Visibility.Collapsed;
            if (placeholder is not null) placeholder.Visibility = Visibility.Visible;
        }
    }

    /// <summary>圆环进度：fraction 0..1，从 12 点顺时针。</summary>
    private static void SetRing(System.Windows.Shapes.Ellipse ring, double fraction)
    {
        var t = ring.StrokeThickness;
        var c = 2 * Math.PI * (ring.Width - t) / 2;
        var dash = c / t;
        var f = Math.Clamp(fraction, 0, 1);
        ring.StrokeDashArray = new System.Windows.Media.DoubleCollection
        {
            f <= 0.001 ? 0 : dash * (f >= 0.999 ? 1.02 : f),
            dash
        };
        ring.Opacity = f <= 0.001 ? 0 : 1;
    }


    private string DescribeLiveRole(OasActivityApi.LiveRole? live)
    {
        var ctx = GlobalAccountContext.Instance;
        var selectedAvatar = ctx.Current?.RoleKey?.Split('@').FirstOrDefault() ?? "";
        if (live is null || string.IsNullOrEmpty(live.AvatarId))
        {
            return "";
        }
        var text = $"游戏内角色 {live.Name} Lv{live.Level}·{live.ServerShowName}";
        if (string.IsNullOrEmpty(selectedAvatar))
        {
            return text;
        }
        return live.AvatarId == selectedAvatar ? $"{text}（与所选一致）" : $"{text}（≠ 所选账号）";
    }

    /// <summary>概览卡下方的归属提示（账号不一致时醒目提示）。</summary>
    private void UpdateLiveRoleBanner(OasActivityApi.LiveRole? live)
    {
        if (live is null || string.IsNullOrEmpty(live.AvatarId))
        {
            LiveRoleText.Text = "";
            return;
        }
        var ctx = GlobalAccountContext.Instance;
        var selectedAvatar = ctx.Current?.RoleKey?.Split('@').FirstOrDefault() ?? "";
        var text = $"游戏内登录：{live.Name} Lv{live.Level} · {live.ServerShowName}";
        if (string.IsNullOrEmpty(selectedAvatar))
        {
            LiveRoleText.Text = text;
            return;
        }
        LiveRoleText.Text = live.AvatarId == selectedAvatar
            ? $"✓ {text}（与所选账号一致）"
            : $"⚠️ {text} —— 与所选账号不符，以上数据属于游戏内角色";
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

            _lastBoard = board;
            _lastHasTasks = (board.Daily?.Count ?? 0) > 0;
            UpdateLiveRoleBanner(board.LiveRole);
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
            var aliases = LoadLiveNames();
            var live = (board.LiveWindows ?? [])
                .OrderBy(w => w.End)
                .Select(w => new LiveWindowVm
                {
                    Key = w.Key,
                    Label = aliases.TryGetValue(w.Key, out var alias) ? alias : w.Key,
                    EndText = $"至 {w.EndText}",
                })
                .ToList();
            LiveList.ItemsSource = live;
            LiveEmptyText.Visibility = live.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            // 当前活动（名称来自客户端活动表）：过滤已过期（end 早于今天），
            // 按截止日期升序——此前会把二月/九月截止的历史活动一直挂在列表里。
            var today = DateTime.Today;
            ActivityList.ItemsSource = (board.CurrentActivities ?? [])
                .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .Where(a => EndDate(a.End) is not { } end || end >= today)
                .OrderBy(a => EndDate(a.End) ?? DateTime.MaxValue)
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
    /// <summary>本地时段别名（游戏内部 key → 中文名），存设置键 liveWindowNames。</summary>
    private static Dictionary<string, string> LoadLiveNames()
    {
        var raw = OasSettings.Get("liveWindowNames", "");
        if (string.IsNullOrWhiteSpace(raw))
        {
            // 内置：已确认的映射（其余留空由用户自己起名）
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["evening_event"] = "逢魔之时",
            };
        }
        try
        {
            var map = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, string>>(raw);
            if (map is null)
            {
                return [];
            }
            map["evening_event"] ??= "逢魔之时";
            return map;
        }
        catch
        {
            return [];
        }
    }

    private static void SaveLiveNames(Dictionary<string, string> map) =>
        OasSettings.Set("liveWindowNames",
            System.Text.Json.JsonSerializer.Serialize(map));

    /// <summary>双击时段行 → 起中文名（本地保存，下次刷新沿用）。</summary>
    private async void LiveRow_DoubleClick(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not FrameworkElement { DataContext: LiveWindowVm vm }) return;
        var input = await PromptTextAsync("给这个时段起个名字",
            $"内部 key：{vm.Key}", vm.Label == vm.Key ? "" : vm.Label);
        if (input is null) return;
        var map = LoadLiveNames();
        if (string.IsNullOrWhiteSpace(input))
        {
            map.Remove(vm.Key);
        }
        else
        {
            map[vm.Key] = input.Trim();
        }
        SaveLiveNames(map);
        await LoadAsync();
    }

    /// <summary>Fluent 输入对话框（与工作台同款）。</summary>
    private async Task<string?> PromptTextAsync(string title, string hint, string initial)
    {
        var host = (Window.GetWindow(this) as MainWindow)?.DialogHost;
        if (host is null)
        {
            return null;
        }
        var tb = new Wpf.Ui.Controls.TextBox
        {
            Text = initial,
            MinWidth = 320,
            PlaceholderText = hint,
        };
        var panel = new StackPanel { Margin = new Thickness(0, 12, 0, 8) };
        panel.Children.Add(tb);
        var dialog = new Wpf.Ui.Controls.ContentDialog(host)
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();
        return result == Wpf.Ui.Controls.ContentDialogResult.Primary ? tb.Text : null;
    }

    /// <summary>解析客户端 end 元组 "(2026,2,3)" → 日期；失败/缺字段返回 null。</summary>
    private static DateTime? EndDate(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }
        var digits = raw.Trim('(', ')').Split(',')
            .Select(x => x.Trim())
            .Where(x => int.TryParse(x, out _))
            .Select(int.Parse)
            .ToArray();
        if (digits.Length < 3)
        {
            return null;
        }
        try
        {
            return new DateTime(digits[0], digits[1], digits[2]);
        }
        catch
        {
            return null;
        }
    }

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