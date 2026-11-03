using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>资产统计 API（/resource/*：角色、日期、每日明细）。</summary>
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

    /// <summary>GET /resource/{config}/roles → 角色列表（含可用日期）。</summary>
    public static async Task<List<RoleInfo>> GetRolesAsync(string config)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var res = await OasApi.Http.GetFromJsonAsync<RolesResponse>(
            $"/resource/{Uri.EscapeDataString(config)}/roles", JsonOptions, cts.Token);
        return res?.Roles ?? [];
    }

    public record ResourceItem(string Id, string Name, string Icon, long Count, long? Delta);

    public record ResourceDay(
        string ConfigName, string RoleKey, string Date,
        string UpdatedAt, List<ResourceItem> Items);

    /// <summary>GET /resource/{config}?date=&amp;role_key= → 某日资产明细
    /// （4000+ 项原始数据，展示层负责过滤聚合）。</summary>
    public static async Task<ResourceDay?> GetResourceDayAsync(
        string config, string? date = null, string? roleKey = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var q = new List<string>();
        if (!string.IsNullOrEmpty(date)) q.Add($"date={Uri.EscapeDataString(date)}");
        if (!string.IsNullOrEmpty(roleKey)) q.Add($"role_key={Uri.EscapeDataString(roleKey)}");
        var url = $"/resource/{Uri.EscapeDataString(config)}" +
                  (q.Count > 0 ? "?" + string.Join("&", q) : "");
        return await OasApi.Http.GetFromJsonAsync<ResourceDay>(url, JsonOptions, cts.Token);
    }
}

/// <summary>图标白名单：Assets/currency/ 下的 basename（与 Flutter 版
/// IconRegistry 同源——只展示本地有图标的资源，190+ 项 → 十几项）。</summary>
public static class CurrencyIconRegistry
{
    private static HashSet<string>? _cache;

    public static HashSet<string> Get() =>
        _cache ??= Build();

    private static HashSet<string> Build()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "currency");
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir, "*.png"))
            {
                var baseName = Path.GetFileNameWithoutExtension(f);
                if (baseName.Length > 0 && baseName.ToLowerInvariant() != "readme")
                {
                    set.Add(baseName);
                }
            }
        }
        return set;
    }

    /// <summary>后端 icon 可能是 icon_jinbi 或 icon_jinbi.png → 统一 basename。</summary>
    public static string BaseName(string icon)
    {
        var name = icon.Trim();
        if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }
        return name;
    }

    public static bool Has(string icon) => Get().Contains(BaseName(icon));
}
