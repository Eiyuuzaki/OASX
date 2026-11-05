using System.Net.Http.Json;
using System.Text.Json;

namespace OASX.Services;

/// <summary>通知（复用 OAS 本家 onepush）+ 寄养状态 API。</summary>
public static class OasNotifyApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record FosterSlot(
        bool? Active, long? T, string? StartText, string? EndText,
        int? Hours, string? CardItem, string? CardName,
        int? Remaining, bool? Empty, bool? Unknown, string? Error);

    public record FosterState(
        bool Success, string ConfigName,
        FosterSlot? Foster, FosterSlot? Own,
        Dictionary<string, string>? CardNames,
        bool NotifyEnabled, string? CardType,
        string? CardTypeSource, OasActivityApi.LiveRole? LiveRole, string? Error);

    public record ProviderParam(string Name);

    public record ProviderInfo(List<string> Required, List<string> Optional);

    public record NotifyConfigState(
        bool Success, bool NotifyEnable, string NotifyConfig, int CycleHours,
        string? ManualCardType, string? AutoCardType);

    private record ConfigBody(
        bool NotifyEnable, string Provider,
        Dictionary<string, string>? Params, int CycleHours,
        string? ManualCardType);

    /// <summary>POST /notify/foster/refresh → 当前寄养倒计时。</summary>
    public static async Task<FosterState?> GetFosterAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var resp = await OasApi.Http.PostAsync("notify/foster/refresh", null, cts.Token);
        var text = await resp.Content.ReadAsStringAsync(cts.Token);
        return JsonSerializer.Deserialize<FosterState>(text, JsonOptions);
    }

    /// <summary>GET /notify/config → 当前通知配置（解析 YAML 为 provider+params）。</summary>
    public record ConfigSnapshot(bool Enable, string Provider,
        Dictionary<string, string> Params, int CycleHours,
        string ManualCardType, string AutoCardType);

    public static async Task<ConfigSnapshot?> GetConfigAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var state = await OasApi.Http.GetFromJsonAsync<NotifyConfigState>(
            "notify/config", JsonOptions, cts.Token);
        if (state is null || !state.Success)
        {
            return null;
        }
        var (provider, kv) = ParseYaml(state.NotifyConfig);
        return new ConfigSnapshot(state.NotifyEnable, provider, kv,
            state.CycleHours, state.ManualCardType ?? "",
            state.AutoCardType ?? "");
    }

    /// <summary>极简 YAML 解析：顶层 key: value 行。</summary>
    public static (string Provider, Dictionary<string, string> Params) ParseYaml(string yaml)
    {
        var result = new Dictionary<string, string>();
        var provider = "null";
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return (provider, result);
        }
        foreach (var raw in yaml.Split('\n'))
        {
            var line = raw.Trim();
            var idx = line.IndexOf(':');
            if (idx <= 0)
            {
                continue;
            }
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            if (key == "provider")
            {
                provider = val;
            }
            else if (val.Length > 0)
            {
                result[key] = val;
            }
        }
        return (provider, result);
    }

    /// <summary>保存通知配置（后端生成 YAML 并写入 OAS 配置）。</summary>
    public static async Task<bool> SaveConfigAsync(
        bool enable, string provider, Dictionary<string, string> @params,
        int cycleHours, string? manualCardType = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var body = new ConfigBody(enable, provider,
            @params.Count > 0 ? @params : null, cycleHours, manualCardType);
        var resp = await OasApi.Http.PutAsJsonAsync("notify/config", body, JsonOptions, cts.Token);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>GET /notify/providers → 渠道参数表。</summary>
    public static async Task<Dictionary<string, ProviderInfo>?> GetProvidersAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var wrap = await OasApi.Http.GetFromJsonAsync<ProvidersWrap>(
            "notify/providers", JsonOptions, cts.Token);
        return wrap?.Providers;
    }

    private record ProvidersWrap(bool Success,
        Dictionary<string, ProviderInfo> Providers);

    /// <summary>发送测试通知。</summary>
    public static async Task<string> TestAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var resp = await OasApi.Http.PostAsync("notify/test", null, cts.Token);
        var text = await resp.Content.ReadAsStringAsync(cts.Token);
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("result", out var r))
            {
                return r.GetString() ?? "";
            }
        }
        catch
        {
            // 原样返回
        }
        return text;
    }
}