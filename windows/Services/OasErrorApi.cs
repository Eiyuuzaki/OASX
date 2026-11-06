using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>错误记录 API（后端 log/error 目录归档）。
///
/// GET /logs/errors                        → 列表（date / script_name 过滤）
/// GET /logs/errors/{id}                   → 详情（log.txt 全文 + PNG 截图元信息）
/// GET /logs/errors/{id}/images/{name}     → 单张截图
///
/// 对齐 Flutter error_records_repository。
/// </summary>
public static class OasErrorApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record ErrorItem(
        string Id, string Directory, string? ScriptName,
        long TimestampMs, string Time, bool Legacy,
        long LogSize, int ImageCount);

    public record ErrorListResponse(
        string? Date, string? ScriptName,
        List<ErrorItem> Items, string? NextCursor, bool HasMore);

    public record ErrorLogText(string FileName, string Content);

    public record ErrorImageInfo(string Name, long ByteSize, int Width, int Height);

    public record ErrorDetail(
        string Id, string Directory, string? ScriptName,
        long TimestampMs, string Time, bool Legacy,
        ErrorLogText? Log, List<ErrorImageInfo> Images);

    /// <summary>GET /logs/errors → 错误记录列表。</summary>
    public static async Task<List<ErrorItem>> ListAsync(
        string? scriptName = null, int limit = 30)
    {
        var q = $"?limit={limit}";
        if (!string.IsNullOrWhiteSpace(scriptName))
            q += $"&script_name={Uri.EscapeDataString(scriptName)}";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var res = await OasApi.Http.GetFromJsonAsync<ErrorListResponse>(
                $"/logs/errors{q}", JsonOptions, cts.Token);
            return res?.Items ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>GET /logs/errors/{id} → 错误详情。</summary>
    public static async Task<ErrorDetail?> DetailAsync(string id)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            return await OasApi.Http.GetFromJsonAsync<ErrorDetail>(
                $"/logs/errors/{Uri.EscapeDataString(id)}", JsonOptions, cts.Token);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>截图 URL（交给 Image 控件直接绑定）。</summary>
    public static string ImageUrl(string id, string name) =>
        $"{OasSettings.BackendAddress.TrimEnd('/')}/logs/errors/{Uri.EscapeDataString(id)}/images/{Uri.EscapeDataString(name)}";
}
