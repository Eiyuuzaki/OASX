using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

/// <summary>单卷展示模型。</summary>
public sealed class ScrollCardVm
{
    /// <summary>卷序号（01..）：按卷 ID 升序，与游戏内"卷一/卷二"顺序一致。</summary>
    public string IndexText { get; init; } = "";

    public string Name { get; init; } = "";
    public double Percent { get; init; }
    public string PercentText { get; init; } = "";
    public string ScoreText { get; init; } = "";

    /// <summary>我在该卷的积分（getSelfTotalScore(卷ID)）；0 时隐藏。</summary>
    public string SelfText { get; init; } = "";
    public System.Windows.Visibility SelfVisibility { get; init; } = System.Windows.Visibility.Collapsed;

    /// <summary>我在该卷的排名（每卷独立榜）；无数据时隐藏。</summary>
    public string SelfRankText { get; init; } = "";
    public System.Windows.Visibility SelfRankVisibility { get; init; } =
        System.Windows.Visibility.Collapsed;
    public string WindowText { get; init; } = "";
    public string StateText { get; init; } = "";
    public Brush StateBrush { get; init; } = Brushes.Gray;
}

/// <summary>
/// 绘卷页：本区服绘卷棋盘（区服维度，数据来自运行中客户端的 HuiJuanInfoMgr）。
/// 一个账号只能看到自己所在区服的数据——客户端本身只拉当前登录服。
/// 活动期外显示"本期已结束"，不造假进度。
/// </summary>
public partial class HuijuanPage : UserControl
{
    public HuijuanPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var ctx = GlobalAccountContext.Instance;
            ctx.CurrentChanged += OnContextChanged;
            ctx.AccountsLoaded += OnAccountsLoaded;
            _ = LoadAsync();
        };
        Unloaded += (_, _) =>
        {
            var ctx = GlobalAccountContext.Instance;
            ctx.CurrentChanged -= OnContextChanged;
            ctx.AccountsLoaded -= OnAccountsLoaded;
        };
    }

    /// <summary>全局账号变化 → 重载本页（页面不自持选择状态）。</summary>
    private void OnContextChanged(AccountEntry? entry) => _ = LoadAsync();

    /// <summary>账号列表加载完成 → 若此前是空态则补载。</summary>
    private void OnAccountsLoaded() => _ = LoadAsync();

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // 页面无后台任务；预留
    }

    /// <summary>活动期 UI 预览（--demo 启动参数；验收后删）。</summary>
    private static bool DemoActive =>
        Environment.GetCommandLineArgs().Contains("--demo");

    /// <summary>造一套"进行中"的棋盘：前两卷已满、其余在填，含个人积分排名。</summary>
    private static OasHuijuanApi.HuijuanBoard BuildDemoBoard()
    {
        const long threshold = 20700000;
        (string Name, long Score, bool Finished, string FinishTime, long Mine, int? MyRank)[] rows =
        {
            ("双生纪元", 20701340, true, "2026-11-12 09:41:22", 1860, 412),
            ("神未曾应", 20700210, true, "2026-11-14 21:03:47", 1120, 903),
            ("交汇之迹", 16228000, false, "", 906, 1204),
            ("一生一刹", 8940600, false, "", 300, 2891),
            ("长夜亘古", 2401500, false, "", 100, 5017),
            ("生之颂祝", 294500, false, "", 0, null),
        };
        var scrolls = rows.Select((r, i) => new OasHuijuanApi.ScrollEntry(
            Id: (627 + i).ToString(),
            Score: r.Score,
            Percent: Math.Round(r.Score * 100.0 / threshold, 2),
            Finished: r.Finished,
            FinishTimeText: r.FinishTime,
            Name: r.Name,
            HeroId: 609,
            Img: "pic_ssr_xy_01",
            BeginText: "2026-11-10 08:00",
            EndText: "2026-11-30 23:59",
            SelfScore: r.Mine,
            SelfRank: r.MyRank)).ToList();

        var self = JsonDocument.Parse(
            "{\"score\": 4286, \"rank\": 1204}").RootElement.Clone();
        return new OasHuijuanApi.HuijuanBoard(
            Success: true, ConfigName: "demo", Issue: 85, Threshold: threshold,
            Active: true, Scrolls: scrolls, SelfTotal: 4286, Self: self,
            Rank: [], Error: null, Hint: null);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await LoadAsync(force: true);

    private async Task LoadAsync(bool force = false)
    {
        var ctx = GlobalAccountContext.Instance;
        if (ctx.Accounts.Count == 0 && !ctx.IsLoading)
        {
            // 直达本页时账号可能尚未加载（启动加载可能仍在进行）
            await ctx.LoadAccountsAsync();
        }
        var entry = ctx.Current;
        if (entry is null)
        {
            AccountNameText.Text = "未选择账号";
            AccountDetailText.Text = "在资产页选择账号后查看其区服绘卷";
            EmptyText.Text = "未选择账号";
            ScrollList.ItemsSource = null;
            SelfCard.Visibility = Visibility.Collapsed;
            SetBadge("—", Brushes.Gray);
            return;
        }

        AccountNameText.Text = entry.Name;
        AccountDetailText.Text = $"Lv{entry.Level} · {entry.ServerName} · 调度器 {entry.Config}";
        EmptyText.Text = "正在读取…";
        ScrollList.ItemsSource = null;

        try
        {
            var board = DemoActive
                ? BuildDemoBoard()
                : await OasHuijuanApi.GetBoardAsync(entry.Config, force);
            if (board is null || !board.Success)
            {
                SetBadge("读取失败", Brushes.Gray);
                EmptyText.Text = board?.Error ?? "读取失败";
                if (!string.IsNullOrEmpty(board?.Hint))
                {
                    EmptyText.Text += $"（{board.Hint}）";
                }
                SubtitleText.Text = "需要游戏运行才能读取绘卷数据（内存桥依赖游戏内嵌解释器）";
                return;
            }

            IssueText.Text = board.Issue.ToString(CultureInfo.InvariantCulture);
            ThresholdText.Text = board.Threshold.ToString("N0");
            SubtitleText.Text = DemoActive
                ? "演示数据（活动期 UI 预览，非真实采集）"
                : board.Active
                    ? "本期绘卷进行中——数据来自运行中的游戏客户端（20 秒缓存）"
                    : "本期已结束——显示上一期最终结果";

            SetBadge(board.Active ? "进行中" : "已结束",
                board.Active ? Brush("OasSuccessBrush", Color.FromRgb(0x16, 0xA3, 0x4A))
                             : Brush("OasTextSecondaryBrush", Color.FromRgb(0x61, 0x61, 0x61)));

            var scrolls = board.Scrolls ?? [];
            EmptyText.Text = scrolls.Count == 0 ? "本区服暂无绘卷数据" : "";
            EmptyText.Visibility = scrolls.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            ScrollList.ItemsSource = scrolls.Select((s, i) => new ScrollCardVm
            {
                IndexText = (i + 1).ToString("00"),
                Name = s.Name,
                Percent = Math.Clamp(s.Percent, 0, 100),
                PercentText = $"{s.Percent:0.##}%",
                ScoreText = $"区服总分 {s.Score:N0} / {board.Threshold:N0}",
                SelfText = $"我的积分 {s.SelfScore:N0}",
                SelfVisibility = s.SelfScore > 0 || s.SelfRank is not null
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed,
                SelfRankText = s.SelfRank is null ? "" : $"排名 {s.SelfRank.Value:N0}",
                SelfRankVisibility = s.SelfRank is null || s.SelfRank <= 0
                    ? System.Windows.Visibility.Collapsed
                    : System.Windows.Visibility.Visible,
                WindowText = string.IsNullOrEmpty(s.BeginText)
                    ? (s.Finished ? $"满卷于 {s.FinishTimeText}" : "")
                    : $"{s.BeginText} → {s.EndText}" +
                      (s.Finished ? $"　满卷 {s.FinishTimeText}" : ""),
                StateText = s.Finished ? "已满卷" : (board.Active ? "进行中" : "未满"),
                StateBrush = s.Finished
                    ? Brush("OasSuccessBrush", Color.FromRgb(0x16, 0xA3, 0x4A))
                    : Brush("OasWarningBrush", Color.FromRgb(0xD9, 0x77, 0x06)),
            }).ToList();

            RenderSelf(board);
        }
        catch (Exception ex)
        {
            SetBadge("读取失败", Brushes.Gray);
            EmptyText.Visibility = Visibility.Visible;
            EmptyText.Text = $"读取失败：{ex.Message}";
        }
    }

    /// <summary>我的积分/排名。
    /// 积分口径：各卷 getSelfTotalScore(卷ID) 求和（实测验证）；
    /// 排名来自 rankInfo（需游戏内拉过榜单），没有就如实显示未拉取。</summary>
    private void RenderSelf(OasHuijuanApi.HuijuanBoard board)
    {
        SelfCard.Visibility = Visibility.Visible;
        var score = board.SelfTotal;
        var joined = board.Scrolls?.Count(x => x.SelfScore > 0) ?? 0;
        SelfScoreText.Text = score > 0 ? score.ToString("N0") : "—";
        SelfRankText.Text = joined > 0 ? $"{joined} / {board.Scrolls?.Count ?? 0}" : "—";
        SelfRankLabel.Text = "参与卷";

        if (score > 0)
        {
            SelfHintText.Text = "本期参与数据（各卷积分求和；排名为每卷独立榜，见下方各卷）";
        }
        else if (board.Active)
        {
            SelfHintText.Text = "本期尚未参与（各卷积分均为 0）";
        }
        else
        {
            SelfHintText.Text = "本期已结束，无参与数据";
        }
    }

    private void SetBadge(string text, Brush brush)
    {
        StatusBadgeText.Text = text;
        StatusBadgeText.Foreground = brush;
    }

    private static Brush Brush(string key, Color fallback) =>
        Application.Current.TryFindResource(key) as Brush
        ?? new SolidColorBrush(fallback);
}