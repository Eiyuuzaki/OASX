using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>
/// 更新检查：查 GitHub Releases 最新版本，与当前程序集版本比较。
/// 网络不可达/仓库无 Release 时返回明确状态，绝不阻塞 UI。
/// </summary>
public static class UpdateChecker
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/Eiyuuzaki/OASX/releases/latest";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record ReleaseInfo(
        string TagName, string Name, string HtmlUrl, string Body,
        bool Prerelease, List<ReleaseAsset> Assets);

    public record ReleaseAsset(string Name, string BrowserDownloadUrl, long Size);

    public record CheckResult(
        bool Success, bool HasUpdate, string CurrentVersion,
        string? LatestVersion, string? DownloadUrl, string? ReleaseUrl,
        string? Note, string? Error);

    /// <summary>当前版本（与关于页显示口径一致）。</summary>
    public static string CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>查询最新 Release 并比较版本（异步、10 秒超时）。</summary>
    public static async Task<CheckResult> CheckAsync()
    {
        var current = CurrentVersion;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("OASX-UpdateChecker");
            using var resp = await http.GetAsync(LatestReleaseUrl);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new CheckResult(true, false, current, null, null, null,
                    "仓库还没有发布版本", null);
            }
            if (!resp.IsSuccessStatusCode)
            {
                return new CheckResult(false, false, current, null, null, null, null,
                    $"GitHub 返回 {(int)resp.StatusCode}");
            }
            var json = await resp.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<ReleaseInfo>(json, JsonOptions);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new CheckResult(false, false, current, null, null, null, null,
                    "解析 Release 失败");
            }

            var latest = release.TagName.TrimStart('v', 'V');
            var hasUpdate = CompareVersions(latest, current) > 0;
            var asset = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            return new CheckResult(
                true, hasUpdate, current, release.TagName,
                asset?.BrowserDownloadUrl, release.HtmlUrl,
                FirstLine(release.Body), null);
        }
        catch (TaskCanceledException)
        {
            return new CheckResult(false, false, current, null, null, null, null,
                "检查超时（网络不可达？）");
        }
        catch (Exception ex)
        {
            return new CheckResult(false, false, current, null, null, null, null,
                ex.Message.Length > 80 ? ex.Message[..80] : ex.Message);
        }
    }

    /// <summary>版本比较：1.2.3 逐段数值比较，返回 -1/0/1。</summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var pb = b.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var va = i < pa.Length && int.TryParse(pa[i], out var x) ? x : 0;
            var vb = i < pb.Length && int.TryParse(pb[i], out var y) ? y : 0;
            if (va != vb)
            {
                return va > vb ? 1 : -1;
            }
        }
        return 0;
    }

    private static string? FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var line = text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim();
        return line is { Length: > 80 } ? line[..80] : line;
    }
}