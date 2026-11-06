using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>配置导入导出（对齐 Flutter config_transfer_api）。
///
/// 后端约定（实测 /config/transfer/capabilities）：
///   version=1, modes=[backup, share], config_import=true, task_import=true
///
/// 端点：
///   GET  /config/export?name=&mode=            → 整份配置 JSON
///   GET  /config/task/export?config_name=&task_name=&mode=  → 单任务参数块
///   POST /config/import                        → {name, payload}（只新建，不覆盖）
///   POST /config/task/import                   → {config_name, task_name, payload}
///
/// 导入按 JSON 发（后端无 python-multipart，明确约定不用 multipart）。
/// </summary>
public static class OasTransferApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record Capabilities(
        int Version,
        List<string> Modes,
        bool ConfigImport,
        bool TaskImport,
        long MaxBytes);

    public record ImportResult(
        bool Success,
        string? Name,
        List<string>? Warnings,
        string? Error);

    /// <summary>GET /config/transfer/capabilities → 后端能力声明。</summary>
    public static async Task<Capabilities?> GetCapabilitiesAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await OasApi.Http.GetFromJsonAsync<Capabilities>(
                "/config/transfer/capabilities", JsonOptions, cts.Token);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>GET /config/export → 整份配置的原始 JSON 文本（可直接落盘）。</summary>
    public static async Task<(bool Ok, string Body)> ExportConfigAsync(
        string name, string mode = "backup")
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var res = await OasApi.Http.GetAsync(
                $"/config/export?name={Uri.EscapeDataString(name)}&mode={mode}", cts.Token);
            return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync(cts.Token));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>GET /config/task/export → 单任务参数块 JSON。</summary>
    public static async Task<(bool Ok, string Body)> ExportTaskAsync(
        string configName, string taskName, string mode = "backup")
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var res = await OasApi.Http.GetAsync(
                $"/config/task/export?config_name={Uri.EscapeDataString(configName)}" +
                $"&task_name={Uri.EscapeDataString(taskName)}&mode={mode}", cts.Token);
            return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync(cts.Token));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>POST /config/import → 导入整份配置为新配置（只新建，不覆盖）。</summary>
    public static async Task<(bool Ok, string Body)> ImportConfigAsync(
        string filePath, string? newName = null)
    {
        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return (false, $"读取文件失败：{ex.Message}");
        }

        JsonNode? payload;
        try
        {
            payload = JsonNode.Parse(raw);
        }
        catch (JsonException ex)
        {
            return (false, $"不是合法 JSON：{ex.Message}");
        }

        var doc = new JsonObject
        {
            ["name"] = newName ?? "",
            ["task_name"] = "",
            ["payload"] = payload,
        };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var res = await OasApi.Http.PostAsync(
                "/config/import",
                new StringContent(doc.ToJsonString(), Encoding.UTF8, "application/json"),
                cts.Token);
            return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync(cts.Token));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>POST /config/task/import → 把任务参数导入现有配置。</summary>
    public static async Task<(bool Ok, string Body)> ImportTaskAsync(
        string configName, string filePath)
    {
        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return (false, $"读取文件失败：{ex.Message}");
        }

        JsonNode? payload;
        try
        {
            payload = JsonNode.Parse(raw);
        }
        catch (JsonException ex)
        {
            return (false, $"不是合法 JSON：{ex.Message}");
        }

        var taskName = (payload?["_transfer"]?["task"]?.GetValue<string>()) ?? "";

        var doc = new JsonObject
        {
            ["config_name"] = configName,
            ["task_name"] = taskName,
            ["payload"] = payload,
        };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var res = await OasApi.Http.PostAsync(
                "/config/task/import",
                new StringContent(doc.ToJsonString(), Encoding.UTF8, "application/json"),
                cts.Token);
            return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync(cts.Token));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
