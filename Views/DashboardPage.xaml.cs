using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OASX.Services;

namespace OASX.Views;

/// <summary>任务队列行。</summary>
public sealed class QueueVm
{
    public string Name { get; init; } = "";
    public string NextRunLabel { get; init; } = "";
}

/// <summary>最近执行记录行。</summary>
public sealed class RunRecordVm
{
    public string Label { get; init; } = "";
    public string DurationLabel { get; init; } = "";
}

/// <summary>
/// Dashboard（规范：OAS 自动化运行控制中心）。
/// 数据全部来自真实接口：全局账号上下文、/ws 调度器实时状态与队列、
/// /device/current 模拟器连接、/stats 今日统计。
/// 无 pause/resume 端点——不提供假按钮；失败计数无数据源——不展示。
/// </summary>
public partial class DashboardPage : UserControl
{
    private OasScriptSocket? _socket;

    /// <summary>假数据预览开关（验收用，验收后删除）。</summary>
    private const bool ScrollBadgeDemo = true;

    private static string TodayKey => DateTime.Today.ToString("yyyy-MM-dd");
    private bool _schedulerRunning;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private string _currentConfig = "";

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _ = RefreshAllAsync();
            _timer.Tick += async (_, _) => await RefreshEnvironmentAsync();
            _timer.Start();
        };
        Unloaded += Page_Unloaded;
    }

    // ==================== 生命周期 ====================

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _socket?.Stop();
    }

    // ==================== 刷新主流程 ====================

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await RefreshAllAsync();

    private async Task RefreshAllAsync()
    {
        var ctx = GlobalAccountContext.Instance;
        if (ctx.Accounts.Count == 0 && !ctx.IsLoading)
        {
            await ctx.LoadAccountsAsync();
        }

        // demo 模式：假数据渲染不依赖账号状态
        if (ScrollBadgeDemo)
        {
            RenderScrollDemo();
        }

        var entry = ctx.Current;
        if (entry is null)
        {
            AccountNameText.Text = "未选择账号";
            AccountDetailText.Text = ctx.Accounts.Count > 0
                ? "在资产页选择账号后展示概况"
                : (ctx.IsLoading ? "账号加载中…" : "未发现任何账号（后端无角色数据）");
            SetOasService(false, "不可达");
            SetScheduler(null, null);
            SetDevice(null);
            TodayDoneText.Text = "—";
            TodayPendingText.Text = "—";
            TodayRuntimeText.Text = "—";
            QueueList.ItemsSource = null;
            QueueEmptyText.Visibility = Visibility.Visible;
            RunsList.ItemsSource = null;
            RunsEmptyText.Visibility = Visibility.Visible;
            ReminderText.Text = "未选择账号——在资产页选择后开始管理运行";
            return;
        }

        AccountNameText.Text = entry.Name;
        AccountDetailText.Text = $"Lv{entry.Level} · {entry.ServerName} · 调度器 {entry.Config}";

        // 调度器配置变化 → 重连 WS
        if (entry.Config != _currentConfig)
        {
            _currentConfig = entry.Config;
            _socket?.Stop();
            _socket = new OasScriptSocket();
            _socket.StateReceived += OnStateReceived;
            _socket.ScheduleReceived += OnScheduleReceived;
            _socket.Start(entry.Config);
        }

        await RefreshEnvironmentAsync();
        await LoadScrollBadgeAsync(entry);
        await RefreshTodayStatsAsync(entry.Config);
    }

    /// <summary>绘卷分项行：图标优先（小/中/大有本地图标），无图标回退短文本。</summary>
    public sealed class ScrollPartVm
    {
        public ImageSource? IconImage { get; init; }
        public Visibility IconVisibility { get; init; } = Visibility.Collapsed;
        public string Label { get; init; } = "";
        public Visibility LabelVisibility { get; init; } = Visibility.Visible;
        public string CountText { get; init; } = "";
        public string DeltaText { get; init; } = "";
        public Brush DeltaBrush { get; init; } = Brushes.Transparent;
    }

    /// <summary>绘卷积分徽标：仅当日采样含绘卷数据时显示（活动期标记）。
    /// 非活动期（最新采样非今日 / 无绘卷项）自动隐藏。</summary>
    private async Task LoadScrollBadgeAsync(AccountEntry entry)
    {
        try
        {
            if (ScrollBadgeDemo)
            {
                RenderScrollDemo();
                return;
            }
            if (entry.Dates.Count == 0)
            {
                ScrollBadge.Visibility = Visibility.Collapsed;
                return;
            }
            // 活动期判定（数据驱动）：最新采样必须是今天——
            // 非活动期没有新采样，上期遗留值不展示
            if (entry.Dates[0] != TodayKey)
            {
                ScrollBadge.Visibility = Visibility.Collapsed;
                return;
            }
            var date = entry.Dates[0];
            var day = await OasResourceApi.GetResourceDayAsync(
                entry.Config, date, entry.RoleKey);
            var scrollItems = (day?.Items ?? [])
                .Where(i => i.Name.Contains("绘卷", StringComparison.Ordinal) && i.Count > 0)
                .ToList();
            if (scrollItems.Count == 0)
            {
                ScrollBadge.Visibility = Visibility.Collapsed;
                return;
            }
            if (scrollItems.Count == 0)
            {
                ScrollBadge.Visibility = Visibility.Collapsed;
                return;
            }
            var total = scrollItems.Sum(i => i.Count);
            var delta = scrollItems.Sum(i => i.Delta ?? 0);
            ScrollTotalText.Text = FormatCount(total);
            ScrollDeltaText.Text = delta == 0 ? "" : FormatDelta(delta);
            ScrollDeltaText.Foreground = new SolidColorBrush(delta >= 0
                ? Color.FromRgb(0x16, 0xA3, 0x4A)
                : Color.FromRgb(0xDC, 0x45, 0x45));

            // 分项行：按名称聚合（绘卷碎片·小/中/大 或 绘卷积分），
            // 短标签取 · 后缀（无 · 用原名），每行 数量 + 今日增量
            var groups = scrollItems
                .GroupBy(i => i.Name)
                .Select(g => new
                {
                    Name = g.Key,
                    Icon = LoadIcon(g.First().Icon),
                    Count = g.Sum(x => x.Count),
                    Delta = g.Sum(x => x.Delta ?? 0),
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            ScrollParts.ItemsSource = groups.Select(g =>
            {
                var hasIcon = g.Icon is not null;
                return new ScrollPartVm
                {
                    IconImage = g.Icon,
                    IconVisibility = hasIcon ? Visibility.Visible : Visibility.Collapsed,
                    Label = ShortLabel(g.Name),
                    LabelVisibility = hasIcon ? Visibility.Collapsed : Visibility.Visible,
                    CountText = FormatCount(g.Count),
                    DeltaText = g.Delta == 0 ? "" : FormatDelta(g.Delta),
                    DeltaBrush = new SolidColorBrush(g.Delta >= 0
                        ? Color.FromRgb(0x16, 0xA3, 0x4A)
                        : Color.FromRgb(0xDC, 0x45, 0x45)),
                };
            }).ToList();

            // 仅一个分项时总分与分项重复——隐藏分项区
            ScrollParts.Visibility = groups.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
            ScrollBadge.Visibility = Visibility.Visible;
        }
        catch
        {
            ScrollBadge.Visibility = Visibility.Collapsed;
        }
    }

    // ==================== 环境（OAS/模拟器） ====================

    private async Task RefreshEnvironmentAsync()
    {
        // OAS 服务：轻量请求探测
        try
        {
            _ = await OasApi.GetConfigListAsync();
            SetOasService(true, "正常");
        }
        catch
        {
            SetOasService(false, "不可达");
            ReminderText.Text = "OAS 服务不可达——检查后端是否运行";
            return;
        }

        // 模拟器连接（当前账号所属调度器绑定的设备）
        if (GlobalAccountContext.Instance.Current is { } entry)
        {
            try
            {
                var dev = await OasDeviceApi.GetCurrentAsync(entry.Config);
                SetDevice(dev);
                UpdateReminders(entry.Config, _schedulerRunning, dev);
            }
            catch
            {
                SetDevice(null);
            }
        }
    }

    private void SetOasService(bool ok, string label)
    {
        OasServiceDot.Fill = new SolidColorBrush(ok
            ? Color.FromRgb(0x16, 0xA3, 0x4A) : Color.FromRgb(0xDC, 0x45, 0x45));
        OasServiceText.Text = label;
    }

    private void SetDevice(DeviceCurrent? dev)
    {
        if (dev is null)
        {
            DeviceDot.Fill = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
            DeviceText.Text = "未知";
            return;
        }
        DeviceDot.Fill = new SolidColorBrush(dev.Online
            ? Color.FromRgb(0x16, 0xA3, 0x4A) : Color.FromRgb(0xD9, 0x77, 0x06));
        DeviceText.Text = dev.Online
            ? $"已连接 · {dev.EmulatorName ?? dev.Serial}"
            : "离线";
    }

    // ==================== 调度器（WS 实时） ====================

    private void OnStateReceived(string state)
    {
        Dispatcher.Invoke(() => SetScheduler(state, null));
    }

    private void OnScheduleReceived(ScheduleData sched)
    {
        Dispatcher.Invoke(() =>
        {
            SetScheduler(null, sched);

            // 今日待执行 = pending + waiting
            TodayPendingText.Text = (sched.Pending.Count + sched.Waiting.Count).ToString("N0");

            // 任务队列：pending + waiting 取前 5
            var items = sched.Pending.Concat(sched.Waiting).ToList();
            QueueList.ItemsSource = items.Take(5).Select(q => new QueueVm
            {
                Name = I18nMenu.Tr(q.Name),
                NextRunLabel = q.NextRun.Length > 0 ? $"下次 {q.NextRun[Min(10, q.NextRun.Length)..]}" : "",
            }).ToList();
            QueueEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // 当前执行任务（running 语义 = 下一个要跑的；显示与否看调度器状态）
            if (_schedulerRunning && sched.Running is { } run && run.Name.Length > 0)
            {
                CurrentTaskName.Text = I18nMenu.Tr(run.Name);
                CurrentTaskDetail.Text = run.NextRun.Length > 0 ? $"计划 {run.NextRun}" : "执行中";
            }
            else if (!_schedulerRunning)
            {
                CurrentTaskName.Text = "当前没有执行任务";
                CurrentTaskDetail.Text = "调度器未运行——在工作台启动后开始执行";
            }
        });
    }

    private static int Min(int a, int b) => a < b ? a : b;

    /// <summary>调度器状态（state 优先，其次从队列推断）。</summary>
    private void SetScheduler(string? state, ScheduleData? sched)
    {
        var running = state is not null
            ? state.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)
            : sched?.Running is not null;
        _schedulerRunning = running;

        SchedulerDot.Fill = new SolidColorBrush(running
            ? Color.FromRgb(0x16, 0xA3, 0x4A) : Color.FromRgb(0x8A, 0x8A, 0x8A));
        SchedulerStateText.Text = running ? "运行中" : (state ?? "空闲/停止");
        StartBtn.IsEnabled = !running;
        StopBtn.IsEnabled = running;

        // ⚠️ schedule.running 语义 = "调度器下一个要跑的任务"（停止时也有值），
        // 不是"正在执行"。当前任务卡必须结合调度器运行态：
        if (!running)
        {
            CurrentTaskName.Text = "当前没有执行任务";
            CurrentTaskDetail.Text = "调度器未运行——在工作台启动后开始执行";
        }
    }

    // ==================== 今日统计 + 执行记录 ====================

    private async Task RefreshTodayStatsAsync(string config)
    {
        try
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var day = await OasStatsApi.GetDayAsync(config, today);
            if (day is null || day.TotalTaskRunCount == 0)
            {
                TodayDoneText.Text = "0";
                TodayRuntimeText.Text = "0秒";
                RunsList.ItemsSource = null;
                RunsEmptyText.Visibility = Visibility.Visible;
                return;
            }

            TodayDoneText.Text = day.TotalTaskRunCount.ToString("N0");
            TodayRuntimeText.Text = FormatDuration(day.TotalRuntimeSeconds);

            // 最近执行记录：全部任务的 runs 按开始时间倒序取 5
            var runs = day.Tasks
                .SelectMany(kv => kv.Value.Runs.Select(r => new { Task = kv.Key, Run = r }))
                .OrderByDescending(x => x.Run.StartTime)
                .Take(5)
                .Select(x => new RunRecordVm
                {
                    Label = $"{ShortTime(x.Run.StartTime)} → {ShortTime(x.Run.EndTime)}　{I18nMenu.Tr(x.Task)}",
                    DurationLabel = FormatDuration(x.Run.DurationSeconds),
                }).ToList();
            RunsList.ItemsSource = runs;
            RunsEmptyText.Visibility = runs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TodayDoneText.Text = "—";
            TodayRuntimeText.Text = "—";
            StatusHint(ex.Message);
        }
    }

    private void StatusHint(string msg) => ReminderText.Text = $"统计数据暂不可获取：{msg}";

    // ==================== 提醒（真实异常才展示） ====================

    private void UpdateReminders(string config, bool schedulerRunning,
        DeviceCurrent? dev)
    {
        var reminders = new List<string>();
        if (!schedulerRunning && OasServiceText.Text == "正常")
        {
            reminders.Add($"调度器 {config} 未运行——在工作台启动后开始执行任务");
        }
        if (dev is { Online: false })
        {
            reminders.Add($"模拟器 {dev.EmulatorName ?? dev.Serial} 离线——任务将无法执行");
        }
        ReminderText.Text = reminders.Count > 0
            ? string.Join("\n", reminders)
            : "一切正常";
    }

    // ==================== 操作（仅真实端点） ====================

    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (GlobalAccountContext.Instance.Current is not { } entry) return;
        StartBtn.IsEnabled = false;
        var (ok, body) = await OasApi.StartScriptAsync(entry.Config);
        StateTextFromOp(ok, ok ? "已发送启动" : $"启动失败：{body}");
        await Task.Delay(1200);   // 等后端状态传播
        await RefreshAllAsync();
    }

    private async void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        if (GlobalAccountContext.Instance.Current is not { } entry) return;
        StopBtn.IsEnabled = false;
        var (ok, body) = await OasApi.StopScriptAsync(entry.Config);
        StateTextFromOp(ok, ok ? "已发送停止" : $"停止失败：{body}");
        await Task.Delay(1200);
        await RefreshAllAsync();
    }

    private void StateTextFromOp(bool ok, string msg) =>
        CurrentTaskDetail.Text = msg;

    // ==================== 导航 ====================

    private void GoWorkbench_Click(object sender, RoutedEventArgs e) =>
        ((MainWindow)Window.GetWindow(this)).RootNav.Navigate(typeof(WorkbenchPage));

    private void GoLog_Click(object sender, RoutedEventArgs e) =>
        ((MainWindow)Window.GetWindow(this)).RootNav.Navigate(typeof(LogPage));

    // ==================== 工具 ====================

    /// <summary>资源图标名 → 本地图标（Assets/currency）。</summary>
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

    /// <summary>按键取主题画刷（语义色）。</summary>
    private static Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

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

    /// <summary>假数据预览（验收用，验收后删除 demoMode 开关）。</summary>
    private void RenderScrollDemo()
    {
        ScrollTotalText.Text = "8,532";
        ScrollDeltaText.Text = "+1,240";
        ScrollDeltaText.Foreground = new SolidColorBrush(
            Color.FromRgb(0x16, 0xA3, 0x4A));
        var rows = new (string Name, long Count, long Delta)[]
        {
            ("绘卷碎片·小", 3200, 500),
            ("绘卷碎片·中", 1800, 300),
            ("绘卷碎片·大", 120, 40),
        };
        ScrollParts.ItemsSource = rows.Select(r => new ScrollPartVm
        {
            IconImage = LoadIcon(r.Name.Contains('小') ? "pic_suipian"
                : r.Name.Contains('中') ? "pic_suipian2" : "pic_suipian3"),
            IconVisibility = Visibility.Visible,
            Label = ShortLabel(r.Name),
            LabelVisibility = Visibility.Collapsed,
            CountText = FormatCount(r.Count),
            DeltaText = FormatDelta(r.Delta),
            DeltaBrush = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
        }).ToList();
        ScrollParts.Visibility = Visibility.Visible;
        ScrollBadge.Visibility = Visibility.Visible;
    }

    /// <summary>绘卷项短标签：'绘卷碎片·小' → '小'，'绘卷积分' → '积分'。</summary>
    private static string ShortLabel(string name)
    {
        if (name.Contains('·', StringComparison.Ordinal))
        {
            return name[(name.LastIndexOf('·') + 1)..];
        }
        if (name.StartsWith("绘卷", StringComparison.Ordinal) && name.Length > 2)
        {
            return name[2..];
        }
        return name;
    }

    private static string ShortTime(string raw)
    {
        var sp = raw.Split(' ');
        return sp.Length > 1 ? sp[1][..8] : raw;
    }

    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}时{ts.Minutes}分";
        if (ts.TotalMinutes >= 1) return $"{ts.Minutes}分{ts.Seconds}秒";
        return $"{seconds:0.#}秒";
    }
}
