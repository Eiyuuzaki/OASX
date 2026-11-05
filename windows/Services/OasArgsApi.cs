using System.Net.Http.Json;
using System.Text.Json;

namespace OASX.Services;

/// <summary>任务参数 API（GET args / PUT value）。</summary>
public static class OasArgsApi
{
    /// <summary>GET /{script}/{task}/args → { 参数组: [参数…] }。
    /// 参数字段：name/title/description/default/value/type/enumEnum。</summary>
    public static async Task<Dictionary<string, List<JsonElement>>> GetTaskArgsAsync(
        string script, string task)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var url = $"/{Uri.EscapeDataString(script)}/{Uri.EscapeDataString(task)}/args";
        var d = await OasApi.Http.GetFromJsonAsync<Dictionary<string, List<JsonElement>>>(
            url, OasApi.JsonOptions, cts.Token);
        return d ?? [];
    }

    /// <summary>PUT /{script}/{task}/{group}/{arg}/value?types=&amp;value= 保存单个参数。
    /// （与 Flutter 版 putScriptArg 相同的查询参数格式，无请求体）</summary>
    public static async Task<bool> PutArgValueAsync(
        string script, string task, string group, string argName, string type, string value)
    {
        var url = $"/{Uri.EscapeDataString(script)}/{Uri.EscapeDataString(task)}/" +
                  $"{Uri.EscapeDataString(group)}/{Uri.EscapeDataString(argName)}/value" +
                  $"?types={Uri.EscapeDataString(type)}&value={Uri.EscapeDataString(value)}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var res = await OasApi.Http.PutAsync(url, null, cts.Token);
        return res.IsSuccessStatusCode;
    }
}
