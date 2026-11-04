using System.Collections.Generic;
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
    /// <summary>
    /// 设置文件路径：多候选 + 可写探测。
    /// 某些宿主环境（沙箱/继承 ACL）下 %LOCALAPPDATA%\OASX 可能不可写，
    /// 因此按序探测：LocalAppData → RoamingAppData → exe 目录，取第一个可写的。
    /// </summary>
    private static string? _resolvedPath;

    private static string FilePath => _resolvedPath ??= ResolveWritablePath();

    private static string ResolveWritablePath()
    {
        var local = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);
        var candidates = new List<string>();
        if (local.Length > 0) candidates.Add(Path.Combine(local, "OASX", "settings.json"));
        if (roaming.Length > 0) candidates.Add(Path.Combine(roaming, "OASX", "settings.json"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "settings.json"));

        foreach (var c in candidates)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(c)!);
                var probe = c + ".probe";
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return c;
            }
            catch
            {
                // 该候选不可写，试下一个
            }
        }
        return candidates[0];   // 全失败：仍用首选，Save 会落错误日志
    }

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

    /// <summary>保存失败诊断日志（exe 同目录）——不再静默吞异常。</summary>
    private static readonly string ErrorLogPath = Path.Combine(
        AppContext.BaseDirectory, "settings_error.log");

    private static void Save()
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath,
                    JsonSerializer.Serialize(_cache, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }));
            }
            catch (Exception ex)
            {
                // 保存失败不打断 UI，但必须留证据：路径/异常全量落盘
                try
                {
                    File.AppendAllText(ErrorLogPath,
                        $"{DateTime.Now:HH:mm:ss} path={FilePath}\n{ex}\n\n");
                }
                catch
                {
                    // 连日志都写不了——放弃（避免递归）
                }
            }
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

    /// <summary>背景材质：mica / acrylic / none（BGI 同款选项）。</summary>
    public static string BackdropMode
    {
        get => Get("backdropMode", "mica");
        set => Set("backdropMode", value);
    }
}
