using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>运行统计 API（/stats/*）。</summary>
public static class OasStatsApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record DatesResponse(string ScriptName, List<string> Dates);

    public record TaskRun(string StartTime, string EndTime, double DurationSeconds);

    public record TaskStat(
        int RunCount, double TotalDurationSeconds,
        JsonElement? Battle, List<TaskRun> Runs);

    public record StatsDay(
        string ScriptName, double TotalRuntimeSeconds,
        int TotalTaskRunCount, int TotalBattleCount,
        Dictionary<string, TaskStat> Tasks);

    /// <summary>GET /stats/{script}/dates → 可用日期（倒序，最新在前）。</summary>
    public static async Task<List<string>> GetDatesAsync(string script)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var res = await OasApi.Http.GetFromJsonAsync<DatesResponse>(
            $"/stats/{Uri.EscapeDataString(script)}/dates", JsonOptions, cts.Token);
        return res?.Dates ?? [];
    }

    /// <summary>GET /stats/{script}?date= → 某日运行统计。</summary>
    public static async Task<StatsDay?> GetDayAsync(string script, string date)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        return await OasApi.Http.GetFromJsonAsync<StatsDay>(
            $"/stats/{Uri.EscapeDataString(script)}?date={Uri.EscapeDataString(date)}",
            JsonOptions, cts.Token);
    }
}
