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

            DailySummaryText.Text = $"{board.DailyDone}/{board.DailyTotal}";
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

    private static Brush Brush(string key, Color fallback) =>
        Application.Current.TryFindResource(key) as Brush
        ?? new SolidColorBrush(fallback);
}