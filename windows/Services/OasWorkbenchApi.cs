using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>工作台 API（/script_menu 分组树）。</summary>
public static class OasWorkbenchApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>GET /script_menu → { 分组名: [任务名, …] }（保序字典）。</summary>
    public static async Task<Dictionary<string, List<string>>> GetScriptMenuAsync()
    {
        var d = await OasApi.Http.GetFromJsonAsync<Dictionary<string, List<string>>>(
            "/script_menu", JsonOptions);
        return d ?? [];
    }

    /// <summary>PUT /{script}/{task}/sync_next_run → 把任务调度提前到 target_dt
    /// （传当前时刻 = 立即执行）。仅脚本运行中有效（后端校验）。</summary>
    public static async Task<bool> SyncNextRunAsync(string script, string task)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var target = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var url = $"/{Uri.EscapeDataString(script)}/{Uri.EscapeDataString(task)}/sync_next_run" +
                  $"?target_dt={Uri.EscapeDataString(target)}";
        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        var res = await OasApi.Http.SendAsync(req, cts.Token);
        return res.IsSuccessStatusCode;
    }
}
