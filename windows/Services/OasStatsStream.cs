using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using OASX.Services;

namespace OASX.Services;

/// <summary>当日统计更新载荷：totals 为全量，changed_tasks 为变化任务的
/// 全量数据，removed_tasks 为消失的任务名（后端 log_stats._build_update_payload）。</summary>
public sealed class StatsUpdate
{
    public string ScriptName { get; init; } = "";
    public double TotalRuntimeSeconds { get; init; }
    public int TotalTaskRunCount { get; init; }
    public int TotalBattleCount { get; init; }
    public Dictionary<string, OasStatsApi.TaskStat> ChangedTasks { get; init; } = [];
    public List<string> RemovedTasks { get; init; } = [];
}

/// <summary>
/// 当日统计 SSE 流（/stats/{script}/stream?date=today）。
/// 事件：snapshot（全天数据）/ update（增量）/ heartbeat。
/// ⚠️ 与 OasLogStream 同款纪律：Task.Run 启动（不占 UI 线程同步上下文），
/// 只用 await ReadLineAsync（EndOfStream 会同步阻塞挂死 UI），
/// 断线指数退避重连 ≤5s。
/// </summary>
public sealed class OasStatsStream : IAsyncDisposable
{
    public event Action<OasStatsApi.StatsDay>? SnapshotReceived;
    public event Action<StatsUpdate>? UpdateReceived;
    public event Action<string>? StatusChanged;

    public bool IsRunning { get; private set; }

    private CancellationTokenSource? _cts;
    private string _script = "";
    private string _date = "";

    public void Start(string script, string date)
    {
        Stop();
        _script = script;
        _date = date;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromMilliseconds(500);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                StatusChanged?.Invoke("实时连接中…");
                using var http = new HttpClient
                {
                    BaseAddress = new Uri(OasSettings.BackendAddress),
                    Timeout = Timeout.InfiniteTimeSpan,
                };
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    $"/stats/{Uri.EscapeDataString(_script)}/stream?date={Uri.EscapeDataString(_date)}");
                req.Headers.Accept.ParseAdd("text/event-stream");
                using var res = await http.SendAsync(req,
                    HttpCompletionOption.ResponseHeadersRead, ct);
                res.EnsureSuccessStatusCode();

                await using var stream = await res.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);
                StatusChanged?.Invoke("实时已连接");

                backoff = TimeSpan.FromMilliseconds(500);
                var eventName = "";
                var dataLines = new List<string>();
                while (await reader.ReadLineAsync(ct) is { } line)
                {
                    if (line.Length == 0)
                    {
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
                StatusChanged?.Invoke($"实时断开：{ex.Message}");
            }

            if (ct.IsCancellationRequested) break;
            StatusChanged?.Invoke("实时重连中…");
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
        StatusChanged?.Invoke("实时已停止");
    }

    private void Dispatch(string eventName, List<string> dataLines)
    {
        if (dataLines.Count == 0) return;
        var data = string.Join("\n", dataLines);
        try
        {
            switch (eventName)
            {
                case "snapshot":
                {
                    var day = JsonSerializer.Deserialize<OasStatsApi.StatsDay>(
                        data, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        });
                    if (day is not null) SnapshotReceived?.Invoke(day);
                    break;
                }
                case "update":
                {
                    var upd = JsonSerializer.Deserialize<StatsUpdate>(
                        data, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        });
                    if (upd is not null) UpdateReceived?.Invoke(upd);
                    break;
                }
                case "heartbeat":
                default:
                    break;
            }
        }
        catch (JsonException)
        {
            // 坏帧丢弃，不影响流
        }
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
