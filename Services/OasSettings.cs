using System.IO;
using System.Text.Json;

namespace OASX.Services;

/// <summary>
/// 应用设置：%LOCALAPPDATA%\OASX\settings.json 持久化。
///
/// ⚠️ 故意不用 ApplicationData.Current / LocalSettings——
/// 这套 WinRT 存储对 unpackaged 应用的 API 面在 WASDK 1.x/2.x 之间
/// 反复变动（Current 要求包标识，unpackaged 调用直接以 0x80073D54
/// 崩溃），纯文件存储零平台耦合，也方便排查。
/// </summary>
public static class OasSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OASX", "settings.json");

    private static readonly object Gate = new();
    private static Dictionary<string, string>? _cache;

    private static Dictionary<string, string> Load()
    {
        if (_cache is not null) return _cache;
        try
        {
            if (File.Exists(FilePath))
            {
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(FilePath)) ?? [];
                return _cache;
            }
        }
        catch
        {
            // 损坏就重置（与 GetStorage 的容错一致）
        }
        _cache = [];
        return _cache;
    }

    private static void Save()
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(_cache, new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));
        }
    }

    public static string Get(string key, string fallback) =>
        Load().TryGetValue(key, out var v) ? v : fallback;

    public static void Set(string key, string value)
    {
        lock (Gate)
        {
            Load()[key] = value;
            Save();
        }
    }

    /// <summary>OAS 后端地址（默认本机 22288）。</summary>
    public static string BackendAddress
    {
        get => Get("backendAddress", "http://127.0.0.1:22288");
        set => Set("backendAddress", value);
    }

    /// <summary>主题模式：system / light / dark。</summary>
    public static string ThemeMode
    {
        get => Get("themeMode", "system");
        set => Set("themeMode", value);
    }
}
