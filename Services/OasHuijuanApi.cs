using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>绘卷棋盘 API（区服维度；数据由运行中的游戏客户端内存读取）。</summary>
public static class OasHuijuanApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record ScrollEntry(
        string Id, long Score, double Percent, bool Finished,
        string FinishTimeText, string Name, int? HeroId, string Img,
        string BeginText, string EndText, long SelfScore, int? SelfRank,
        long Remain);

    public record FragmentEntry(string Name, long Count, string Icon);

    public record HuijuanBoard(
        bool Success, string ConfigName, int Issue, long Threshold, bool Active,
        List<ScrollEntry> Scrolls, long SelfTotal, long? BestRank,
        string BestRankScroll, List<FragmentEntry> Fragments,
        JsonElement? Self, List<JsonElement> Rank,
        string? Error, string? Hint);

    /// <summary>GET /huijuan/{config}?force= → 棋盘（后端 20s 缓存）。</summary>
    public static async Task<HuijuanBoard?> GetBoardAsync(
        string config, bool force = false)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var url = $"/huijuan/{Uri.EscapeDataString(config)}" +
                  (force ? "?force=true" : "");
        return await OasApi.Http.GetFromJsonAsync<HuijuanBoard>(
            url, JsonOptions, cts.Token);
    }
}