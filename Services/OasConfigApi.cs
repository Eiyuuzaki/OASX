using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>配置 CRUD（/config_copy / /config / /config_new_name）。
/// 注意：后端无独立 POST /config——新建 = 以 template 为模板复制出新名。</summary>
public static class OasConfigApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>GET /config_new_name → 后端生成的新配置名。</summary>
    public static async Task<string> GetNewNameAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var name = await OasApi.Http.GetFromJsonAsync<string>(
            "/config_new_name", JsonOptions, cts.Token);
        return name ?? "";
    }

    /// <summary>POST /config_copy?file=&amp;template= → 复制配置（新建的本质），
    /// 返回新配置列表。</summary>
    public static async Task<List<string>> CopyAsync(string file, string template = "template")
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var res = await OasApi.Http.PostAsync(
            $"/config_copy?file={Uri.EscapeDataString(file)}&template={Uri.EscapeDataString(template)}",
            null, cts.Token);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<string>>(JsonOptions, cts.Token) ?? [];
    }

    /// <summary>PUT /config?old_name=&amp;new_name= → 重命名（运行中会先停脚本）。</summary>
    public static async Task<bool> RenameAsync(string oldName, string newName)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var res = await OasApi.Http.PutAsync(
            $"/config?old_name={Uri.EscapeDataString(oldName)}&new_name={Uri.EscapeDataString(newName)}",
            null, cts.Token);
        if (res.StatusCode == System.Net.HttpStatusCode.BadRequest) return false;
        res.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>DELETE /config?name= → 删除配置（template 受保护）。</summary>
    public static async Task<bool> DeleteAsync(string name)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var res = await OasApi.Http.DeleteAsync(
            $"/config?name={Uri.EscapeDataString(name)}", cts.Token);
        if (res.StatusCode == System.Net.HttpStatusCode.BadRequest) return false;
        res.EnsureSuccessStatusCode();
        return true;
    }
}

/// <summary>当前配置绑定的设备（模拟器连接状态）。</summary>
public sealed record DeviceCurrent(
    string ConfigName, string Serial, string? EmulatorName,
    string? EmulatorType, bool Online);

public static class OasDeviceApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}

/// <summary>当前配置绑定的设备（模拟器连接状态）。</summary>
public sealed record DeviceCurrent(
    string ConfigName, string Serial, string? EmulatorName,
    string? EmulatorType, bool Online);

public static class OasDeviceApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>GET /device/current?config_name= → serial/online 等。</summary>
    public static async Task<DeviceCurrent?> GetCurrentAsync(string config)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await OasApi.Http.GetFromJsonAsync<DeviceCurrent>(
            $"/device/current?config_name={Uri.EscapeDataString(config)}",
            JsonOptions, cts.Token);
    }
}
