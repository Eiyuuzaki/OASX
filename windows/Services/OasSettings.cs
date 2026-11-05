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
        // 设置必须落在**用户级固定位置**：早期版本按「第一个可写」回退到
        // exe 旁，导致每个构建目录各一份 settings.json——换 exe 启动
        // （重新发布/换构建）时设置全丢，表现为「材质/主题重启就掉」。
        var candidates = new List<string>();
        // LOCALAPPDATA 环境变量最可靠（SpecialFolder 在个别宿主下返回空）
        var localEnv = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localEnv))
        {
            candidates.Add(Path.Combine(localEnv, "OASX", "settings.json"));
        }
        var local = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            candidates.Add(Path.Combine(local, "OASX", "settings.json"));
        }
        var roaming = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(roaming))
        {
            candidates.Add(Path.Combine(roaming, "OASX", "settings.json"));
        }
        // 最后才是 exe 目录（便携/完全无权限环境兜底）
        var exeDir = Path.Combine(AppContext.BaseDirectory, "settings.json");
        candidates.Add(exeDir);

        foreach (var c in candidates)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(c)!);
                var probe = c + ".probe";
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                // 一次性迁移：新位置没有设置，而 exe 旁有旧设置 → 导入
                if (!File.Exists(c) && File.Exists(exeDir)
                    && !string.Equals(c, exeDir, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Copy(exeDir, c, overwrite: false);
                    }
                    catch
                    {
                        // 迁移失败不影响使用（会以默认值启动）
                    }
                }
                try
                {
                    LogPaths.Append("settings_path.log",
                        $"{DateTime.Now:HH:mm:ss} OK   {c}");
                }
                catch
                {
                    // 诊断日志失败不影响主流程
                }
                return c;
            }
            catch (Exception ex)
            {
                // 该候选不可写，试下一个（记录原因：历史 bug 里这里静默过，
                // 导致"设置存到 exe 旁、换构建就丢"很久没被发现）
                try
                {
                    LogPaths.Append("settings_path.log",
                        $"{DateTime.Now:HH:mm:ss} FAIL {c} :: {ex.GetType().Name}: " +
                        $"{ex.Message}");
                }
                catch
                {
                    // 诊断日志失败不影响主流程
                }
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
    private static string ErrorLogPath => LogPaths.File("settings_error.log");

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
