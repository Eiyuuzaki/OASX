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
        await RefreshTodayStatsAsync(entry.Config);
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
                UpdateReminders(entry.Config, schedulerRunning: SchedulerDot.Fill == FindBrush("OasSuccessBrush"), dev);
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
            ? Color.FromRgb(0x16A34A) : Color.FromRgb(0xDC4545));
        OasServiceText.Text = label;
    }

    private void SetDevice(OasConfigApi.DeviceCurrent? dev)
    {
        if (dev is null)
        {
            DeviceDot.Fill = new SolidColorBrush(Color.FromRgb(0x8A8A8A));
            DeviceText.Text = "未知";
            return;
        }
        DeviceDot.Fill = new SolidColorBrush(dev.Online
            ? Color.FromRgb(0x16A34A) : Color.FromRgb(0xD97706));
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

            // 当前执行任务
            if (sched.Running is { } run && run.Name.Length > 0)
            {
                CurrentTaskName.Text = I18nMenu.Tr(run.Name);
                CurrentTaskDetail.Text = run.NextRun.Length > 0 ? $"计划 {run.NextRun}" : "执行中";
            }
            else
            {
                CurrentTaskName.Text = "当前没有执行任务";
                CurrentTaskDetail.Text = "调度器空闲时不会执行任务";
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

        SchedulerDot.Fill = new SolidColorBrush(running
            ? Color.FromRgb(0x16A34A) : Color.FromRgb(0x8A8A8A));
        SchedulerStateText.Text = running ? "运行中" : (state ?? "空闲/停止");
        StartBtn.IsEnabled = !running;
        StopBtn.IsEnabled = running;
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
        OasConfigApi.DeviceCurrent? dev)
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
