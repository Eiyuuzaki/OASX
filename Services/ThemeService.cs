using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace OASX.Services;

/// <summary>
/// 主题服务（单例门面）：浅色/深色/跟随系统 + 背景材质。
/// 职责：应用主题、语义色字典热切换、用户偏好持久化、Windows 系统主题监听。
/// 页面不得直接调用 ApplicationThemeManager，一律经由本服务。
/// </summary>
public static class ThemeService
{
    private static bool _watching;

    /// <summary>当前生效的深浅色（含 system 解析结果）。</summary>
    public static bool IsDark { get; private set; }

    /// <summary>应用持久化的主题与背景材质（启动与设置切换共用入口）。</summary>
    public static void Apply()
    {
        var mode = OasSettings.ThemeMode;
        var dark = mode switch
        {
            "light" => false,
            "dark" => true,
            _ => IsSystemDark(),
        };
        Apply(dark);
    }

    /// <summary>应用指定深浅色（含语义色字典热切换与持久化触发方的偏好）。</summary>
    public static void Apply(bool dark)
    {
        IsDark = dark;

        var backdrop = ResolveBackdropType();
        ApplicationThemeManager.Apply(
            dark ? ApplicationTheme.Dark : ApplicationTheme.Light, backdrop);

        // ⚠️ 直接把背景材质写到主窗口 DP：MainWindow.xaml 不再硬编码
        // WindowBackdropType，否则 XAML 的 Mica 会在 SourceInitialized 时
        // 覆盖掉这里应用的 Acrylic（设置页标签 acrylic、实际 Mica 的根因）。
        if (Application.Current?.MainWindow is FluentWindow fw)
        {
            fw.WindowBackdropType = backdrop;
        }

        ApplySemanticDictionary(dark);
    }

    /// <summary>设置项 → 窗口背景材质类型。</summary>
    public static WindowBackdropType ResolveBackdropType() =>
        OasSettings.BackdropMode switch
        {
            "acrylic" => WindowBackdropType.Acrylic,
            "none" => WindowBackdropType.None,
            _ => WindowBackdropType.Mica,
        };

    /// <summary>监听 Windows 系统主题变化（仅 system 模式跟随）。
    /// 进程内挂一次；SystemEvents 是全局静态，App 退出自动释放。</summary>
    public static void WatchSystem()
    {
        if (_watching) return;
        _watching = true;
        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category != UserPreferenceCategory.General) return;
            if (OasSettings.ThemeMode != "system") return;
            var dark = IsSystemDark();
            if (dark != IsDark)
            {
                // 系统主题变化 → 整链重应用（含语义色热切换）
                Apply(dark);
            }
        };
    }

    /// <summary>读取 Windows 个性化设置的 AppsUseLightTheme。</summary>
    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>语义色字典热切换：替换合并字典列表中的 Light/Dark.xaml。
    /// WPF DynamicResource 会跟随新字典重新求值，全窗颜色即时更新。</summary>
    private static void ApplySemanticDictionary(bool dark)
    {
        var dicts = Application.Current.Resources.MergedDictionaries;
        var source = new Uri(
            $"pack://application:,,,/Themes/{(dark ? "Dark" : "Light")}.xaml");

        // 找到已有的语义字典实例并原位替换（保持合并顺序）
        for (var i = 0; i < dicts.Count; i++)
        {
            var src = dicts[i].Source;
            if (src is not null &&
                (src.OriginalString.Contains("/Themes/Light.xaml") ||
                 src.OriginalString.Contains("/Themes/Dark.xaml")))
            {
                dicts[i] = new ResourceDictionary { Source = source };
                return;
            }
        }
        // 不存在则追加（首次）
        dicts.Add(new ResourceDictionary { Source = source });
    }
}
