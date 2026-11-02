using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace OASX.Services;

/// <summary>
/// SSE 日志流客户端（GET /logs/{script}/stream）。
///
/// 对应 Flutter 的 sse_client.dart。事件格式：
///   event: ready / heartbeat / lines / closed
///   data:  {json}
/// lines 事件携带 text/event-stream 的日志增量（每行一条 log 记录）。
/// </summary>
public sealed class OasLogStream : IAsyncDisposable
{
    private HttpClient? _http;
    private CancellationTokenSource? _cts;

    /// <summary>收到一批日志行（增量）。</summary>
    public event Action<string[]>? LinesReceived;

    /// <summary>连接状态变化（用于 UI 指示）。</summary>
    public event Action<string>? StatusChanged;

    public bool IsRunning { get; private set; }

    /// <summary>启动流订阅；内部自动重连（指数退避上限 5s）。</summary>
    public void Start(string script)
    {
        Stop();
        _cts = new CancellationTokenSource();
        // ★ 必须用 Task.Run 启动：若在 UI 线程调用 Start，RunAsync 的所有
        //   await 续体都会经 DispatcherQueueSynchronizationContext 回到
        //   UI 线程执行——一旦遇到同步阻塞点（如 EndOfStream 的 socket 读）
        //   就把 UI 线程挂死（实测踩过：未响应，CPU 0，dump 显示 UI 线程
        //   阻塞在 NetworkStream.Read）。Task.Run 让整个循环跑线程池。
        _ = Task.Run(() => RunAsync(script, _cts.Token));
    }

    public void Stop()
    {
        IsRunning = false;
        _cts?.Cancel();
        _cts = null;
        _http?.Dispose();
        _http = null;
    }

    private async Task RunAsync(string script, CancellationToken ct)
    {
        IsRunning = true;
        var backoff = TimeSpan.FromMilliseconds(500);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                StatusChanged?.Invoke("连接中…");
                using var http = new HttpClient
                {
                    BaseAddress = new Uri(OasSettings.BackendAddress),
                    Timeout = Timeout.InfiniteTimeSpan,
                };
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    $"/logs/{Uri.EscapeDataString(script)}/stream");
                req.Headers.Accept.ParseAdd("text/event-stream");
                using var res = await http.SendAsync(req,
                    HttpCompletionOption.ResponseHeadersRead, ct);
                res.EnsureSuccessStatusCode();

                await using var stream = await res.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);
                StatusChanged?.Invoke("已连接");

                backoff = TimeSpan.FromMilliseconds(500); // 成功连接，重置退避
                var eventName = "";
                var dataLines = new List<string>();
                // ⚠️ 只用 await ReadLineAsync 判定数据到达。
                // **绝不能用 reader.EndOfStream**——它是同步阻塞读（内部
                // NetworkStream.Read），在心跳间隔期间会把执行线程挂死；
                // 若这条循环跑在 UI 线程上（见 Start 的 Task.Run 注释），
                // 表现就是整个应用"未响应"。服务端断开时 ReadLineAsync
                // 返回 null，由外层重连逻辑接管。
                while (await reader.ReadLineAsync(ct) is { } line)
                {
                    if (line.Length == 0)
                    {
                        // 空行 = 事件边界
                        Dispatch(eventName, dataLines);
                        eventName = "";
                        dataLines.Clear();
                        continue;
                    }
                    if (line.StartsWith("event:", StringComparison.Ordinal))
                    {
                        eventName = line["event:".Length..].Trim();
                    }
                    else if (line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        dataLines.Add(line["data:".Length..].TrimStart());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"断开：{ex.Message}");
            }

            if (ct.IsCancellationRequested) break;
            StatusChanged?.Invoke("重连中…");
            try
            {
                await Task.Delay(backoff, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            backoff = TimeSpan.FromMilliseconds(
                Math.Min(backoff.TotalMilliseconds * 2, 5000));
        }
        IsRunning = false;
    }

    private void Dispatch(string eventName, List<string> dataLines)
    {
        if (dataLines.Count == 0) return;
        var data = string.Join("\n", dataLines);
        switch (eventName)
        {
            case "lines":
                // {"lines": ["…", "…"]} —— 日志增量
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("lines", out var arr)
                        && arr.ValueKind == JsonValueKind.Array)
                    {
                        var lines = arr.EnumerateArray()
                            .Select(e => e.GetString() ?? "")
                            .Where(s => s.Length > 0)
                            .ToArray();
                        if (lines.Length > 0) LinesReceived?.Invoke(lines);
                    }
                }
                catch (JsonException)
                {
                    // 单条坏行丢弃，不影响流
                }
                break;
            case "ready":
                StatusChanged?.Invoke("已连接");
                break;
            case "closed":
                StatusChanged?.Invoke("脚本已停止");
                break;
            case "heartbeat":
            default:
                break;
        }
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
