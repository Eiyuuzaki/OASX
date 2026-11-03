using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>资产统计 API（/resource/*）。</summary>
public static class OasResourceApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record RoleInfo(
        string RoleKey, string Name, int Level,
        string ServerId, string ServerName, string ShortId,
        string AvatarId, string LastUpdated, List<string> Dates);

    public record RolesResponse(string ConfigName, List<RoleInfo> Roles);

    public record ResourceItem(string Id, string Name, string Icon, long Count, long Delta);

    public record ResourceDay(
        string ConfigName, string RoleKey, string Date, string UpdatedAt,
        List<ResourceItem> Items);

    /// <summary>GET /resource/{config}/roles → 角色列表（含可用日期）。</summary>
    public static async Task<RolesResponse?> GetRolesAsync(string config)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await OasApi.Http.GetFromJsonAsync<RolesResponse>(
            $"/resource/{Uri.EscapeDataString(config)}/roles", JsonOptions, cts.Token);
    }

    /// <summary>GET /resource/{config}?date=… → 某日资产明细（4000+ 项）。</summary>
    public static async Task<ResourceDay?> GetResourceDayAsync(
        string config, string? date = null, string? roleKey = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var q = new List<string>();
        if (!string.IsNullOrEmpty(date)) q.Add($"date={Uri.EscapeDataString(date)}");
        if (!string.IsNullOrEmpty(roleKey)) q.Add($"role_key={Uri.EscapeDataString(roleKey)}");
        var url = $"/resource/{Uri.EscapeDataString(config)}" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
        return await OasApi.Http.GetFromJsonAsync<ResourceDay>(url, JsonOptions, cts.Token);
    }
}
