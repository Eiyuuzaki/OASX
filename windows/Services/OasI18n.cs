using System.Net.Http.Json;

namespace OASX.Services;

/// <summary>
/// 参数页翻译（后端 /home/i18n_full 的完整 zh-CN 映射）。
///
/// 为什么不用 I18nMenu：那份是 WPF 本地硬编码的菜单/任务名表，
/// 没有参数名、参数说明，也没有小写枚举值（ignore / wait_10s …），
/// 所以参数页大量显示英文原值。这里改为向后端拉一次完整映射并缓存，
/// 查不到时再回退 I18nMenu，最后回退原文。
/// </summary>
public static class OasI18n
{
    private static Dictionary<string, string>? _map;
    private static bool _loading;

    /// <summary>已加载的映射条数（0 表示没加载成功，或后端未提供）。</summary>
    public static int Count => _map?.Count ?? 0;

    /// <summary>拉取完整翻译表（进程内只拉一次；失败不阻塞调用方）。</summary>
    public static async Task EnsureLoadedAsync()
    {
        if (_map is not null || _loading) return;
        _loading = true;
        try
        {
            var data = await OasApi.Http.GetFromJsonAsync<Dictionary<string, string>>(
                "/home/i18n_full", OasApi.JsonOptions);
            if (data is not null && data.Count > 0)
            {
                _map = new Dictionary<string, string>(data, StringComparer.Ordinal);
            }
        }
        catch
        {
            // 后端旧版本没有这个接口：静默退回本地表
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>后端地址变更后清空缓存。</summary>
    public static void Reset() => _map = null;

    /// <summary>翻译：后端映射 → 本地菜单表 → 原文。</summary>
    public static string Tr(string? key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (_map is not null && _map.TryGetValue(key, out var v) && v.Length > 0)
            return v;
        return I18nMenu.Tr(key);
    }
}
