using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>OAS 后端 API 客户端（对应 Flutter 的 api_client_*.dart）。</summary>
public static class OasApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static HttpClient? _http;

    /// <summary>共享 HttpClient；后端地址变化后调用 Reset()。</summary>
    public static HttpClient Http =>
        _http ??= new HttpClient
        {
            BaseAddress = new Uri(OasSettings.BackendAddress),
            Timeout = TimeSpan.FromSeconds(10),
        };

    /// <summary>后端地址变更后重建 HttpClient。</summary>
    public static void Reset()
    {
        _http?.Dispose();
        _http = null;
    }

    /// <summary>GET /config_list → ["chess_e2e", ...]</summary>
    public static async Task<List<string>> GetConfigListAsync()
    {
        var list = await Http.GetFromJsonAsync<List<string>>("/config_list", JsonOptions);
        return list ?? [];
    }

    public record ResourceItem(string Id, string Name, long Count, long Delta);

    public record ResourceDay(
        string ConfigName,
        string Date,
        string UpdatedAt,
        List<ResourceItem> Items);

    /// <summary>GET /resource/{config}?date=… → 当日资产明细</summary>
    public static async Task<ResourceDay?> GetResourceDayAsync(
        string config, string? date = null)
    {
        var url = $"/resource/{Uri.EscapeDataString(config)}";
        if (!string.IsNullOrEmpty(date)) url += $"?date={date}";
        return await Http.GetFromJsonAsync<ResourceDay>(url, JsonOptions);
    }

    public record ScriptState(string State, string Time);

    /// <summary>GET /{script}/state → 脚本运行状态。
    /// ⚠️ 后端在特定状态下长时间不返回（实测阻塞 120s+），
    /// 必须 5s 超时按"未知"处理，不能让调用方干等。</summary>
    public static async Task<ScriptState?> GetScriptStateAsync(string script)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            return await Http.GetFromJsonAsync<ScriptState>(
                $"/{Uri.EscapeDataString(script)}/state", JsonOptions, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>GET /{script}/start → 启动脚本。返回 (是否成功, 原始响应)。</summary>
    public static async Task<(bool Ok, string Body)> StartScriptAsync(string script)
    {
        var res = await Http.GetAsync($"/{Uri.EscapeDataString(script)}/start");
        var body = await res.Content.ReadAsStringAsync();
        return (res.IsSuccessStatusCode, body);
    }

    /// <summary>GET /{script}/stop → 停止脚本。</summary>
    public static async Task<(bool Ok, string Body)> StopScriptAsync(string script)
    {
        var res = await Http.GetAsync($"/{Uri.EscapeDataString(script)}/stop");
        var body = await res.Content.ReadAsStringAsync();
        return (res.IsSuccessStatusCode, body);
    }

    public record LogLines(
        string ScriptName,
        string? LiveCursor,
        List<string> Lines);

    /// <summary>GET /logs/{script}?limit=… → 历史日志（文本行）</summary>
    public static async Task<LogLines?> GetLogHistoryAsync(
        string script, int limit = 200)
    {
        return await Http.GetFromJsonAsync<LogLines>(
            $"/logs/{Uri.EscapeDataString(script)}?limit={limit}", JsonOptions);
    }
}
