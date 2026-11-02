using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OASX.Services;

namespace OASX.Views;

/// <summary>实时日志页：SSE 流 + 启停控制（服务层与 WinUI3 版完全共用）。</summary>
public partial class LogPage : UserControl
{
    private readonly OasLogStream _stream = new();
    private readonly ObservableCollection<string> _lines = [];

    public LogPage()
    {
        InitializeComponent();
        LogList.ItemsSource = _lines;
        _stream.LinesReceived += OnLinesReceived;
        _stream.StatusChanged += s => Dispatcher.BeginInvoke(
            () => StatusText.Text = s);
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
            StatusText.Text = $"后端连接失败：{ex.Message}";
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
        }
        catch (Exception ex)
        {
            StatusText.Text = $"历史日志加载失败：{ex.Message}";
        }
        _stream.Start(script);
        await RefreshStateAsync(script);
    }

    private void OnLinesReceived(string[] lines)
    {
        // SSE 循环跑在线程池（OasLogStream.Start 用 Task.Run 启动），
        // 这里必须封送回 UI 线程——WPF 的 Dispatcher。
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var line in lines) _lines.Add(line);
            while (_lines.Count > 2000) _lines.RemoveAt(0);
        });
    }

    private async Task RefreshStateAsync(string script)
    {
        try
        {
            var state = await OasApi.GetScriptStateAsync(script);
            StartButton.IsEnabled = state?.State != "running";
            StopButton.IsEnabled = state?.State == "running";
            if (state is not null) StatusText.Text = $"状态：{state.State}";
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
        StatusText.Text = ok ? "已发送启动" : $"启动失败：{body}";
        await RefreshStateAsync(script);
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptSelector.SelectedItem is not string script) return;
        StopButton.IsEnabled = false;
        var (ok, body) = await OasApi.StopScriptAsync(script);
        StatusText.Text = ok ? "已发送停止" : $"停止失败：{body}";
        await RefreshStateAsync(script);
    }
}
