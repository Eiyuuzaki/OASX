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
}
