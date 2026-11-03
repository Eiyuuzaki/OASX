using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OASX.Services;

namespace OASX.Views;

/// <summary>任务明细行。</summary>
public sealed class TaskStatVm
{
    public string DisplayName { get; init; } = "";
    public string Summary { get; init; } = "";
    public List<RunVm> Runs { get; init; } = [];
}

public sealed class RunVm
{
    public string Text { get; init; } = "";
}

/// <summary>
/// 统计页：汇总磁贴（总时长/任务次数/战斗场次）+ 日期导航 +
/// 任务明细（每次运行的起止与时长）。任务名经 I18nMenu 本地化。
/// 历史趋势图与当日 SSE 实时流为后续增量。
/// </summary>
public partial class StatisticsPage : UserControl
{
    private List<string> _configs = [];
    private List<string> _dates = [];
    private int _dateIndex = -1;
    private bool _suppressDateEvent;
    private OasStatsStream? _stream;

    private static string TodayKey => DateTime.Today.ToString("yyyy-MM-dd");

    public StatisticsPage()
    {
        InitializeComponent();
        _ = LoadAsync();
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

    /// <summary>配置切换 → 加载可用日期 → 加载最新一天。</summary>
    private async void ConfigSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ConfigSelector.SelectedItem is not string config) return;
        try
        {
            _dates = await OasStatsApi.GetDatesAsync(config);
            _suppressDateEvent = true;
            DateSelector.ItemsSource = _dates;
            _dateIndex = _dates.Count > 0 ? 0 : -1;
            DateSelector.SelectedIndex = _dateIndex;
            _suppressDateEvent = false;

            if (CurrentDate is string date)
            {
                await LoadDayAsync(config, date);
            }
            else
            {
                ClearTiles();
                TaskList.ItemsSource = null;
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

    /// <summary>日期切换 → 加载该日统计。</summary>
    private async void DateSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressDateEvent) return;
        if (ConfigSelector.SelectedItem is string config && CurrentDate is string date)
        {
            await LoadDayAsync(config, date);
        }
    }

    /// <summary>← = 更新的一天（日期倒序）。</summary>
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

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigSelector.SelectedItem is string config && CurrentDate is string date)
        {
            await LoadDayAsync(config, date);
        }
    }

    private async Task LoadCurrentDayAsync()
    {
        if (ConfigSelector.SelectedItem is string config && CurrentDate is string date)
        {
            await LoadDayAsync(config, date);
        }
    }

    /// <summary>加载某天的运行统计并渲染。当日自动订阅 SSE 实时流。</summary>
    private async Task LoadDayAsync(string config, string date)
    {
        EmptyText.Visibility = Visibility.Collapsed;

        // 流切换：先停旧流
        _stream?.Stop();

        if (date == TodayKey)
        {
            _stream = new OasStatsStream();
            _stream.SnapshotReceived += ApplyDay;
            _stream.UpdateReceived += ApplyUpdate;
            _stream.StatusChanged += msg => Dispatcher.Invoke(() => StatusText.Text = msg);
            _stream.Start(config, date);
            return;
        }

        try
        {
            var day = await OasStatsApi.GetDayAsync(config, date);
            if (day is null || day.TotalTaskRunCount == 0)
            {
                ClearTiles();
                TaskList.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                StatusText.Text = $"{date} 无统计";
                return;
            }
            ApplyDay(day);
            StatusText.Text = $"{date} · {day.Tasks.Count} 个任务";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败：{ex.Message}";
        }
    }

    /// <summary>渲染全天统计（SSE snapshot 与历史 GET 共用）。</summary>
    private void ApplyDay(OasStatsApi.StatsDay day)
    {
        EmptyText.Visibility = Visibility.Collapsed;

        RuntimeTile.Text = FormatDuration(day.TotalRuntimeSeconds);
        TaskCountTile.Text = day.TotalTaskRunCount.ToString("N0");
        BattleCountTile.Text = day.TotalBattleCount.ToString("N0");

        TaskList.ItemsSource = day.Tasks.Select(kv =>
        {
            var (taskName, stat) = (kv.Key, kv.Value);
            return new TaskStatVm
            {
                DisplayName = I18nMenu.Tr(taskName),
                Summary = $"{stat.RunCount} 次 · {FormatDuration(stat.TotalDurationSeconds)}",
                Runs = stat.Runs.Select(r => new RunVm
                {
                    Text = $"{ShortTime(r.StartTime)} → {ShortTime(r.EndTime)}　{FormatDuration(r.DurationSeconds)}",
                }).ToList(),
            };
        }).ToList();
    }

    /// <summary>应用实时增量：totals 全量 + changed_tasks 合并 + removed 移除。</summary>
    private void ApplyUpdate(StatsUpdate upd)
    {
        RuntimeTile.Text = FormatDuration(upd.TotalRuntimeSeconds);
        TaskCountTile.Text = upd.TotalTaskRunCount.ToString("N0");
        BattleCountTile.Text = upd.TotalBattleCount.ToString("N0");

        if (TaskList.ItemsSource is not IEnumerable<TaskStatVm> current)
        {
            return;
        }
        var byName = current.ToDictionary(vm => vm.DisplayName, vm => vm);
        foreach (var (name, stat) in upd.ChangedTasks)
        {
            byName[I18nMenu.Tr(name)] = new TaskStatVm
            {
                DisplayName = I18nMenu.Tr(name),
                Summary = $"{stat.RunCount} 次 · {FormatDuration(stat.TotalDurationSeconds)}",
                Runs = stat.Runs.Select(r => new RunVm
                {
                    Text = $"{ShortTime(r.StartTime)} → {ShortTime(r.EndTime)}　{FormatDuration(r.DurationSeconds)}",
                }).ToList(),
            };
        }
        foreach (var removed in upd.RemovedTasks)
        {
            byName.Remove(I18nMenu.Tr(removed));
        }
        TaskList.ItemsSource = byName.Values
            .OrderByDescending(vm => vm.Summary)
            .ToList();
    }

    /// <summary>离开页面停流（UserControl 无 OnNavigatedFrom，用 Unloaded）。</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _stream?.Stop();
    }

    private void ClearTiles()
    {
        RuntimeTile.Text = "—";
        TaskCountTile.Text = "—";
        BattleCountTile.Text = "—";
    }

    /// <summary>"2026-10-23 01:40:40.259" → "01:40:40"。</summary>
    private static string ShortTime(string raw)
    {
        var sp = raw.Split(' ');
        return sp.Length > 1 ? sp[1][..8] : raw;
    }

    /// <summary>秒数 → "49分53秒" / "2时7分" / "49.9分"。</summary>
    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}时{ts.Minutes}分";
        }
        if (ts.TotalMinutes >= 1)
        {
            return $"{ts.Minutes}分{ts.Seconds}秒";
        }
        return $"{seconds:0.#}秒";
    }
}
