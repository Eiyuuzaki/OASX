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
    public record LedgerItem(
        string Id, string Name, string Icon, long Count,
        long? FirstCount, long? Delta);

    public record LedgerResponse(
        string ConfigName, string RoleKey,
        string FirstDate, string LastDate, string BaseDate,
        int Days, List<LedgerItem> Items);

    /// <summary>GET /resource/{config}/ledger?days=&amp;role_key=
    /// 区间账本：最近 N 天首尾对比，每资产净增量（|delta| 降序）。</summary>
    public static async Task<LedgerResponse?> GetLedgerAsync(
        string config, string roleKey, int days = 7)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var url = $"/resource/{Uri.EscapeDataString(config)}/ledger" +
                  $"?days={days}&role_key={Uri.EscapeDataString(roleKey)}";
        return await OasApi.Http.GetFromJsonAsync<LedgerResponse>(
            url, JsonOptions, cts.Token);
    }

    public record HistoryPoint(string Date, long Count);

    public record HistoryResponse(
        string ConfigName, string RoleKey, string ItemId,
        List<HistoryPoint> Points);

    /// <summary>GET /resource/{config}/history?item_name=&amp;role_key=&amp;days=
    /// 单项资源历史（按名称跨期聚合，绘卷碎片这类每期换 ID 的项必需）。</summary>
    public static async Task<List<HistoryPoint>> GetHistoryAsync(
        string config, string roleKey, string itemName, int days = 60)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var url = $"/resource/{Uri.EscapeDataString(config)}/history" +
                  $"?item_name={Uri.EscapeDataString(itemName)}" +
                  $"&role_key={Uri.EscapeDataString(roleKey)}&days={days}";
        var res = await OasApi.Http.GetFromJsonAsync<HistoryResponse>(
            url, JsonOptions, cts.Token);
        return res?.Points ?? [];
    }
    // ==================== 采集控制（对齐 Flutter resource_controller） ====================

    /// <summary>采集器状态：{config_name, running}</summary>
    public record CollectorStatus(string ConfigName, bool Running);

    /// <summary>GET /resource/{config}/status → 采集器是否在周期运行。</summary>
    public static async Task<CollectorStatus?> GetStatusAsync(string config)
    {
        try
        {
            return await OasApi.Http.GetFromJsonAsync<CollectorStatus>(
                $"/resource/{Uri.EscapeDataString(config)}/status", OasApi.JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>POST /resource/{config}/collect → 立即采集一次。</summary>
    public static async Task<(bool Ok, string Body)> CollectNowAsync(string config)
    {
        var res = await OasApi.Http.PostAsync(
            $"/resource/{Uri.EscapeDataString(config)}/collect", null);
        return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    /// <summary>POST /resource/{config}/start → 启动周期采集。</summary>
    public static async Task<(bool Ok, string Body)> StartCollectAsync(string config)
    {
        var res = await OasApi.Http.PostAsync(
            $"/resource/{Uri.EscapeDataString(config)}/start", null);
        return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    /// <summary>POST /resource/{config}/stop → 停止周期采集。</summary>
    public static async Task<(bool Ok, string Body)> StopCollectAsync(string config)
    {
        var res = await OasApi.Http.PostAsync(
            $"/resource/{Uri.EscapeDataString(config)}/stop", null);
        return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
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
