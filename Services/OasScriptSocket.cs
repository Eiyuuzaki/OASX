using System.Net.WebSockets;
using System.Text.Json;

namespace OASX.Services;

/// <summary>任务队列条目（schedule.running/pending/waiting 的元素）。</summary>
public sealed class QueueItem
{
    public string Name { get; init; } = "";
    public string NextRun { get; init; } = "";
}

/// <summary>调度队列快照：running 为当前执行（空对象 = 无），pending/waiting 为队列。</summary>
public sealed class ScheduleData
{
    public QueueItem? Running { get; init; }
    public List<QueueItem> Pending { get; init; } = [];
    public List<QueueItem> Waiting { get; init; } = [];
}

/// <summary>
/// 调度器实时状态 WebSocket（/ws/{script}）。
/// 消息两类：{"state": "..."} 与 {"schedule": {running, pending, waiting}}。
/// ⚠️ REST GET /{script}/state 是后端占位假实现（Hello, SSE!），真实状态只走本通道。
/// </summary>
public sealed class OasScriptSocket : IDisposable
{
    public event Action<string>? StateReceived;
    public event Action<ScheduleData>? ScheduleReceived;
    public event Action<string>? StatusChanged;

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private string _script = "";

    public bool IsRunning { get; private set; }

    public void Start(string script)
    {
        Stop();
        _script = script;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();
        try { _ws?.Dispose(); } catch { /* ignore */ }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromMilliseconds(500);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                StatusChanged?.Invoke("实时连接中…");
                var wsUri = new Uri(OasSettings.BackendAddress
                    .Replace("http://", "ws://")
                    .Replace("https://", "wss://")
                    + $"/ws/{Uri.EscapeDataString(_script)}");
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(wsUri, ct);
                _ws = ws;
                StatusChanged?.Invoke("实时已连接");

                backoff = TimeSpan.FromMilliseconds(500);
                var buffer = new byte[64 * 1024];
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    var text = System.Text.Encoding.UTF8.GetString(
                        buffer, 0, result.Count);
                    Dispatch(text);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"实时断开：{ex.Message}");
            }

            if (ct.IsCancellationRequested) break;
            StatusChanged?.Invoke("实时重连中…");
            try { await Task.Delay(backoff, ct); }
            catch (OperationCanceledException) { break; }
            backoff = TimeSpan.FromMilliseconds(Math.Min(backoff.TotalMilliseconds * 2, 5000));
        }
        IsRunning = false;
    }

    private void Dispatch(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("state", out var stateEl))
            {
                var state = stateEl.ValueKind == JsonValueKind.String
                    ? stateEl.GetString() ?? "" : stateEl.GetRawText();
                if (state.Length > 0) StateReceived?.Invoke(state);
            }
            if (root.TryGetProperty("schedule", out var schedEl) &&
                schedEl.ValueKind == JsonValueKind.Object)
            {
                ScheduleReceived?.Invoke(ParseSchedule(schedEl));
            }
        }
        catch (JsonException)
        {
            // 坏帧丢弃
        }
    }

    internal static ScheduleData ParseSchedule(JsonElement el)
    {
        QueueItem? running = null;
        if (el.TryGetProperty("running", out var runEl) &&
            runEl.ValueKind == JsonValueKind.Object &&
            runEl.TryGetProperty("name", out var rn))
        {
            running = new QueueItem
            {
                Name = rn.GetString() ?? "",
                NextRun = runEl.TryGetProperty("next_run", out var nr)
                    ? nr.GetString() ?? "" : "",
            };
        }

        List<QueueItem> ParseList(string prop) =>
            el.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().Select(i => new QueueItem
                {
                    Name = i.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    NextRun = i.TryGetProperty("next_run", out var t) ? t.GetString() ?? "" : "",
                }).ToList()
                : [];

        return new ScheduleData
        {
            Running = running,
            Pending = ParseList("pending"),
            Waiting = ParseList("waiting"),
        };
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
