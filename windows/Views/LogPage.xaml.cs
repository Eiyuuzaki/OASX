using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

/// <summary>实时日志页：SSE 流 + 启停控制。
/// 排版约定：页头（标题+连接状态）+ 工具条卡片 + 日志卡片（等宽字体、空态、自动滚动）。</summary>
public partial class LogPage : UserControl
{
    /// <summary>一行日志的着色分段（对齐 Flutter log_widget_content 配色）。</summary>
    public sealed class LogRow
    {
        public string Time { get; init; } = "";
        public string Level { get; init; } = "";
        public string Rest { get; init; } = "";
        public Brush LevelBrush { get; init; } = Brushes.Gray;
        /// <summary>级别权重：0=其他 1=INFO 2=WARNING 3=ERROR+CRITICAL</summary>
        public int Rank { get; init; }
        public string Raw { get; init; } = "";
    }

    /// <summary>解析一行日志为着色分段；解析失败整行进 Rest（不丢内容）。</summary>
    private static LogRow ParseLine(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return new LogRow { Rest = "", Raw = raw };

        var text = raw.TrimEnd();
        // 级别关键字定位（避免依赖列宽，日志格式会变）
        var m = Regex.Match(text,
            @"^(.*?)(INFO|WARNING|ERROR|CRITICAL|DEBUG)(.*)$");
        if (!m.Success)
        {
            return new LogRow { Rest = text, Raw = text, LevelBrush = Brushes.Gray, Rank = 0 };
        }

        // 原样保留前后缀：Run 拼回去必须与原始文本一字不差，否则字会黏在一起
        var prefix = m.Groups[1].Value;   // 日期 + 时间 + 文件行号 + 竖线 + 空格
        var level = m.Groups[2].Value;
        var suffix = m.Groups[3].Value;   // " | " + 正文

        var (brush, rank) = level switch
        {
            "INFO" => (new SolidColorBrush(Color.FromRgb(0x37, 0x6D, 0x88)), 1),
            "DEBUG" => (new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)), 0),
            "WARNING" => (new SolidColorBrush(Color.FromRgb(0xE6, 0xA7, 0x00)), 2),
            "ERROR" => (new SolidColorBrush(Color.FromRgb(0xD9, 0x3A, 0x3A)), 3),
            "CRITICAL" => (new SolidColorBrush(Color.FromRgb(0xD9, 0x3A, 0x3A)), 3),
            _ => (Brushes.Gray, 0),
        };

        return new LogRow
        {
            Time = prefix,
            Level = level,
            Rest = suffix,
            LevelBrush = brush,
            Rank = rank,
            Raw = raw,
        };
    }

    private readonly OasLogStream _stream = new();
    /// <summary>当前展示行（筛选后）。</summary>
    private readonly ObservableCollection<LogRow> _lines = [];
    /// <summary>全量行缓存（筛选切换时重建 _lines，不丢数据）。</summary>
    private readonly List<LogRow> _allRows = [];

    /// <summary>用户手动向上滚动时暂停自动跟随（滚回底部自动恢复）。</summary>
    private bool _followTail = true;

    public LogPage()
    {
        InitializeComponent();
        LogList.ItemsSource = _lines;
        _lines.CollectionChanged += (_, _) => UpdateLogMeta();
        _stream.LinesReceived += OnLinesReceived;
        _stream.StatusChanged += s => Dispatcher.BeginInvoke(() => SetStatus(s));
        _ = LoadAsync();

        // 账号切换 → 日志页跟着换脚本
        var ctx = GlobalAccountContext.Instance;
        ctx.CurrentChanged += OnAccountChanged;
    }

    private async Task LoadAsync()
    {
        try
        {
            var configs = await OasApi.GetConfigListAsync();
            ScriptSelector.ItemsSource = configs;
            if (configs.Count == 0) return;

            // 优先跟随全局选中账号的调度器，而不是永远取列表第一个
            // （否则用户在仪表盘选了 oas1，这里却显示"契灵"的日志）
            var wanted = GlobalAccountContext.Instance.Current?.Config;
            var idx = wanted is null ? -1 : configs.IndexOf(wanted);
            ScriptSelector.SelectedIndex = idx >= 0 ? idx : 0;
        }
        catch (Exception ex)
        {
            SetStatus($"后端连接失败：{ex.Message}");
            SetConn(false);
        }
    }

    /// <summary>全局账号切换时同步切换日志页的脚本选择。</summary>
    private void OnAccountChanged(AccountEntry? entry)
    {
        if (entry is null) return;
        if (ScriptSelector.ItemsSource is not IEnumerable<string> items) return;
        var list = items.ToList();
        var idx = list.IndexOf(entry.Config);
        if (idx >= 0 && ScriptSelector.SelectedIndex != idx)
        {
            ScriptSelector.SelectedIndex = idx;
        }
    }

    private async void ScriptSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ScriptSelector.SelectedItem is not string script) return;
        _lines.Clear();
        try
        {
            var history = await OasApi.GetLogHistoryAsync(script);
            if (history?.Lines is { Count: > 0 } lines)
            {
                _allRows.Clear();
                _lines.Clear();
                foreach (var line in lines)
                {
                    var row = ParseLine(line.Text);
                    _allRows.Add(row);
                    _lines.Add(row);
                }
            }
            SetConn(true);
        }
        catch (Exception ex)
        {
            SetStatus($"历史日志加载失败：{ex.Message}");
            SetConn(false);
        }
        _stream.Start(script);
        await RefreshStateAsync(script);
        ScrollToEnd();
    }

    private void OnLinesReceived(string[] lines)
    {
        // SSE 循环跑在线程池（OasLogStream.Start 用 Task.Run 启动），
        // 这里必须封送回 UI 线程——WPF 的 Dispatcher。
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var raw in lines)
            {
                var row = ParseLine(raw);
                _allRows.Add(row);
                if (PassesFilter(row)) _lines.Add(row);
            }
            // 上限保护：全量与展示同步裁剪
            while (_allRows.Count > 3000) _allRows.RemoveAt(0);
            while (_lines.Count > 2000) _lines.RemoveAt(0);
            if (_followTail)
            {
                ScrollToEnd();
            }
        });
    }

    private async Task RefreshStateAsync(string script)
    {
        try
        {
            var state = await OasApi.GetScriptStateAsync(script);
            StartButton.IsEnabled = !IsRunningState(state?.State);
            StopButton.IsEnabled = IsRunningState(state?.State);
            if (state is not null) SetStatus($"状态：{state.State}");
        }
        catch
        {
            // 状态查询失败不阻塞页面
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptSelector.SelectedItem is not string script) return;
        StartButton.IsEnabled = false;
        var (ok, body) = await OasApi.StartScriptAsync(script);
        SetStatus(ok ? "已发送启动" : $"启动失败：{body}");
        await RefreshStateAsync(script);
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptSelector.SelectedItem is not string script) return;
        StopButton.IsEnabled = false;
        var (ok, body) = await OasApi.StopScriptAsync(script);
        SetStatus(ok ? "已发送停止" : $"停止失败：{body}");
        await RefreshStateAsync(script);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
        _followTail = true;
    }

    /// <summary>滚动到末尾（等待布局完成后执行，否则 ScrollIntoView 可能无效）。</summary>
    private void ScrollToEnd()
    {
        if (_lines.Count == 0) return;
        Dispatcher.BeginInvoke(() =>
        {
            var last = _lines[^1];
            LogList.ScrollIntoView(last);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>用户滚动：离开底部就暂停跟随，回到底部恢复。</summary>
    private void LogList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0)
        {
            return;
        }
        var atBottom = e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 8;
        _followTail = atBottom || AutoScrollSwitch.IsChecked == true;
    }

    /// <summary>日志数量与空态。</summary>
    /// <summary>当前筛选所需的最低级别（0=全部 2=警告+ 3=仅错误）。</summary>
    private int _minRank;

    private bool PassesFilter(LogRow row) => row.Rank >= _minRank;

    private void LevelFilter_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        // 全部=0 / 警告+错误=2 / 仅错误=3
        _minRank = LevelFilter?.SelectedIndex switch
        {
            1 => 2,
            2 => 3,
            _ => 0,
        };
        ApplyFilter();
    }

    /// <summary>按当前筛选重建展示集合（保留滚动位置语义：跟到底部）。</summary>
    private void ApplyFilter()
    {
        if (_allRows.Count == 0) return;
        var filtered = _minRank == 0 ? _allRows : _allRows.Where(PassesFilter).ToList();
        _lines.Clear();
        foreach (var row in filtered) _lines.Add(row);
        if (_followTail) ScrollToEnd();
        UpdateLogMeta();
    }

    private void UpdateLogMeta()
    {
        EmptyText.Visibility = _lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_lines.Count == 0)
        {
            LineCountText.Text = "";
            EmptyText.Text = _minRank == 0
                ? "暂无日志输出——启动任务后在此实时显示"
                : "当前筛选条件下没有匹配的日志";
            return;
        }
        EmptyText.Text = "暂无日志输出——启动任务后在此实时显示";
        LineCountText.Text = _minRank == 0
            ? $"{_lines.Count:N0} 行"
            : $"{_lines.Count:N0} / {_allRows.Count:N0} 行（已筛选）";
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void SetConn(bool ok)
    {
        ConnDot.Fill = new SolidColorBrush(ok
            ? Color.FromRgb(0x16, 0xA3, 0x4A)
            : Color.FromRgb(0xDC, 0x45, 0x45));
    }

    /// <summary>脚本是否处于运行态（大小写不敏感，兼容 RUNNING/running）。</summary>
    private static bool IsRunningState(string? state) =>
        state is not null && state.Equals("running", StringComparison.OrdinalIgnoreCase);
}
