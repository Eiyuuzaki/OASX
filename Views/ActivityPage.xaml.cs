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