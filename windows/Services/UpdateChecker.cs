using System.Net.Http;
using System.Xml.Linq;

namespace OASX.Services;

/// <summary>
/// 更新检查：读 GitHub Releases 的 Atom 订阅源（releases.atom）。
/// 不用 REST API：api.github.com 对未认证请求按 IP 限流（代理出口 IP 常被打满，
/// 实测返回 403 "API rate limit exceeded"），而 atom 源没有这个限制。
/// </summary>
public static class UpdateChecker
{
    private const string AtomUrl =
        "https://github.com/Eiyuuzaki/OASX/releases.atom";

    public record CheckResult(
        bool Success, bool HasUpdate, string CurrentVersion,
        string? LatestVersion, string? DownloadUrl, string? ReleaseUrl,
        string? Note, string? Error);

    /// <summary>当前版本（与关于页显示口径一致）。</summary>
    public static string CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>查询最新 Release 并比较版本（异步、12 秒超时）。</summary>
    public static async Task<CheckResult> CheckAsync()
    {
        var current = CurrentVersion;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 OASX-UpdateChecker");
            using var resp = await http.GetAsync(AtomUrl);
            if (!resp.IsSuccessStatusCode)
            {
                return new CheckResult(false, false, current, null, null, null, null,
                    $"GitHub 返回 {(int)resp.StatusCode}");
            }
            var xml = await resp.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xml);
            XNamespace ns = "http://www.w3.org/2005/Atom";
            var entry = doc.Root?.Elements(ns + "entry").FirstOrDefault();
            if (entry is null)
            {
                return new CheckResult(true, false, current, null, null, null,
                    "仓库还没有发布版本", null);
            }
            var tag = entry.Element(ns + "title")?.Value?.Trim() ?? "";
            var link = entry.Element(ns + "link")?.Attribute("href")?.Value;
            var content = entry.Element(ns + "content")?.Value;
            if (tag.Length == 0)
            {
                return new CheckResult(false, false, current, null, null, null, null,
                    "解析 Release 标题失败");
            }
            var latest = tag.TrimStart('v', 'V');
            var hasUpdate = CompareVersions(latest, current) > 0;
            return new CheckResult(
                true, hasUpdate, current, tag, link, link,
                FirstLine(content), null);
        }
        catch (TaskCanceledException)
        {
            return new CheckResult(false, false, current, null, null, null, null,
                "检查超时（网络不可达？）");
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            return new CheckResult(false, false, current, null, null, null, null,
                msg.Length > 80 ? msg[..80] : msg);
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
        var line = text.Split('\n')
            .Select(l => System.Net.WebUtility.HtmlDecode(l).Trim())
            .FirstOrDefault(l => l.Length > 0);
        return line is { Length: > 80 } ? line[..80] : line;
    }
}