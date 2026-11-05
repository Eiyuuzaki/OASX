using System.Net.Http.Json;
using System.Text.Json;

namespace OASX.Services;

/// <summary>当前登录角色（用于标注哪个账号是此刻在线的）。</summary>
public static class OasRoleApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record LiveRole(
        bool Success, string ConfigName, string AvatarId, string ServerId,
        string ShortId, string Name, int Level, string ServerShowName,
        string RoleKey, string? Error, string? Hint);

    /// <summary>最近一次成功读取的在线角色（菜单打开时先用缓存，后台再刷新）。</summary>
    public static LiveRole? Cached { get; set; }

    /// <summary>GET /role/current/{config} → 当前在线角色。</summary>
    public static async Task<LiveRole?> GetLiveAsync(string config)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var url = $"/role/current/{Uri.EscapeDataString(config)}";
            var role = await OasApi.Http.GetFromJsonAsync<LiveRole>(url, JsonOptions, cts.Token);
            if (role is { Success: true } && !string.IsNullOrEmpty(role.AvatarId))
            {
                Cached = role;
            }
            return role;
        }
        catch
        {
            return null;
        }
    }
}