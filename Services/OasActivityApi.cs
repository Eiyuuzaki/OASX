using System.Net.Http.Json;
using System.Text.Json;

namespace OASX.Services;

/// <summary>活动/每日任务 API（数据由游戏客户端内存读取，完成状态自动判定）。</summary>
public static class OasActivityApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record DailyTask(
        int Id, string Name, string Title, int Cur, int Max, int Score,
        bool Done, string ProgressText);

    public record MonsterTask(string Id, int Process, int Need, bool Fin, int? Type);

    public record ActivityBoard(
        bool Success, string ConfigName,
        List<DailyTask> Daily, int DailyDone, int DailyTotal,
        List<MonsterTask> Monster, int MonsterDone,
        List<string> Activity, bool ActivityAvailable,
        int ServerWeekDay, string ServerTimeText,
        string? Error, string? Hint);

    /// <summary>GET /activity/{config}?force= → 任务进度（后端 30s 缓存）。</summary>
    public static async Task<ActivityBoard?> GetBoardAsync(string config, bool force = false)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var url = $"/activity/{Uri.EscapeDataString(config)}" + (force ? "?force=true" : "");
        return await OasApi.Http.GetFromJsonAsync<ActivityBoard>(url, JsonOptions, cts.Token);
    }
}