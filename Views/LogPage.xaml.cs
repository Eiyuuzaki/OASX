using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

/// <summary>实时日志页：SSE 流 + 启停控制。
/// 排版约定：页头（标题+连接状态）+ 工具条卡片 + 日志卡片（等宽字体、空态、自动滚动）。</summary>
public partial class LogPage : UserControl
{
    private readonly OasLogStream _stream = new();
    private readonly ObservableCollection<string> _lines = [];

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
    }

    private async Task LoadAsync()
    {
        try
        {
            var configs = await OasApi.GetConfigListAsync();
            ScriptSelector.ItemsSource = configs;
            if (configs.Count > 0) ScriptSelector.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            SetStatus($"后端连接失败：{ex.Message}");
            SetConn(false);
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
                foreach (var line in lines) _lines.Add(line);
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
            foreach (var line in lines) _lines.Add(line);
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
            StartButton.IsEnabled = state?.State != "running";
            StopButton.IsEnabled = state?.State == "running";
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
    private void UpdateLogMeta()
    {
        EmptyText.Visibility = _lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LineCountText.Text = _lines.Count == 0 ? "" : $"{_lines.Count:N0} 行";
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void SetConn(bool ok)
    {
        ConnDot.Fill = new SolidColorBrush(ok
            ? Color.FromRgb(0x16, 0xA3, 0x4A)
            : Color.FromRgb(0xDC, 0x45, 0x45));
    }
}