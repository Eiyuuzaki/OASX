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

    /// <summary>应用指定深浅色（含语义色字典热切换与持久化触发方的偏好）。
    /// **必须在 UI 线程执行**：SystemEvents 回调在后台线程，
    /// 直接改 WPF 主题/字典会出现半更新（深浅色乱串的根因）。</summary>
    /// <summary>已应用主题的窗口（幂等判据之一）。</summary>
    private static WeakReference<Window>? _themedWindow;
    private static bool _lastDark;
    private static WindowBackdropType _lastBackdrop;

    public static void Apply(bool dark)
    {
        try
        {
            ApplyCore(dark);
        }
        catch (Exception ex)
        {
            // 主题应用失败不能阻断调用方（曾导致导航中断、页面空白）
            Log("Apply failed: " + ex);
        }
    }

    /// <summary>强制完整应用一次：绕过幂等缓存。
    /// 启动时 ApplyWindowChromeOnly 只设了窗口级 DWM 属性且写入了幂等缓存，
    /// 导致随后的 Apply 直接早退——材质"属性正确但画面无效果"。
    /// 窗口渲染完成后调用本方法补上合成（ApplicationThemeManager.Apply）。</summary>
    public static void ApplyFull()
    {
        var mode = OasSettings.ThemeMode;
        var dark = mode switch
        {
            "light" => false,
            "dark" => true,
            _ => IsSystemDark(),
        };
        try
        {
            ApplyCore(dark, force: true);
            Log("startup ApplyFull completed");
        }
        catch (Exception ex)
        {
            Log("startup ApplyFull failed: " + ex);
        }
    }

    private static void ApplyCore(bool dark, bool force = false)
    {
        var app = Application.Current;
        if (app is not null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() => Apply(dark)));
            return;
        }

        // 幂等：同一个窗口 + 相同的深浅色/材质 → 直接返回。
        // 反复调用 ApplicationThemeManager.Apply 会重建窗口 chrome，
        // 触发 FluentWindow.SetWindowChrome 的 Freezable 上下文异常（整页黑屏）。
        var backdropNow = ResolveBackdropType();
        var currentWindow = app?.MainWindow;
        var alreadyThemed = !force && currentWindow is not null
                            && _themedWindow is not null
                            && _themedWindow.TryGetTarget(out var themed)
                            && ReferenceEquals(themed, currentWindow)
                            && dark == _lastDark
                            && backdropNow == _lastBackdrop;
        if (alreadyThemed)
        {
            // 仍确保深色标志在位（廉价、无 chrome 重建）
            if (currentWindow is FluentWindow fwKeep)
            {
                SetImmersiveDarkMode(fwKeep, dark);
            }
            return;
        }

        IsDark = dark;

        var backdrop = ResolveBackdropType();

        // 1) 先把**窗口级**的深色标志与材质用 DWM 直接设好。
        //    不能依赖下面的 ApplicationThemeManager.Apply——它内部会重建窗口
        //    chrome，在本项目字典结构下可能抛 Freezable 异常；一旦抛出，
        //    排在它后面的窗口设置就永远执行不到（表现为"切材质没反应"）。
        if (Application.Current?.MainWindow is FluentWindow fw)
        {
            SetImmersiveDarkMode(fw, dark);
            ApplyBackdrop(fw, backdrop);
        }

        // 2) 再切语义色字典（纯资源替换，不碰 chrome）
        ApplySemanticDictionary(dark);

        // 3) 最后交给 WPF-UI 的应用级主题（失败只记日志，不影响上面的效果）
        try
        {
            ApplicationThemeManager.Apply(
                dark ? ApplicationTheme.Dark : ApplicationTheme.Light, backdrop);
        }
        catch (Exception ex)
        {
            Log("ApplicationThemeManager.Apply failed: " + ex.Message);
        }

        _lastDark = dark;
        _lastBackdrop = backdrop;
        if (currentWindow is not null)
        {
            _themedWindow = new WeakReference<Window>(currentWindow);
        }

        // 诊断：WPF-UI 认为的主题 + 合并字典里的 ThemesDictionary 状态
        try
        {
            var appTheme = ApplicationThemeManager.GetAppTheme();
            var sb = new System.Text.StringBuilder();
            sb.Append($"appTheme={appTheme} requestedDark={dark} dicts=[");
            foreach (var d in Application.Current.Resources.MergedDictionaries)
            {
                var src = d.Source?.OriginalString ?? "(inline)";
                var kind = d is Wpf.Ui.Markup.ThemesDictionary ? " (ThemesDictionary)"
                    : d is Wpf.Ui.Markup.ControlsDictionary ? " (ControlsDictionary)" : "";
                sb.Append(System.IO.Path.GetFileName(src)).Append(kind).Append(';');
            }
            sb.Append(']');
            Log(sb.ToString());
        }
        catch (Exception ex)
        {
            Log("theme diag failed: " + ex.Message);
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE = 20（旧版 19）。</summary>
    public static void SetImmersiveDarkMode(Window window, bool dark)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                Log($"immersive: hwnd=0 (窗口句柄未就绪) dark={dark}");
                return;
            }
            var flag = dark ? 1 : 0;
            var hr20 = DwmSetWindowAttribute(hwnd, 20, ref flag, sizeof(int));
            var hr19 = hr20 != 0
                ? DwmSetWindowAttribute(hwnd, 19, ref flag, sizeof(int))
                : 0;
            // 读回校验
            var read = -1;
            DwmGetWindowAttributeInt(hwnd, 20, ref read);
            Log($"immersive: hwnd={hwnd.ToInt64():X} dark={dark} " +
                $"hr20={hr20} hr19={hr19} readback={read}");
        }
        catch (Exception ex)
        {
            Log("immersive EXCEPTION: " + ex.Message);
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd, int attr, ref int value, int size);

    private static void DwmGetWindowAttributeInt(IntPtr hwnd, int attr, ref int value)
    {
        try
        {
            DwmGetWindowAttribute(hwnd, attr, ref value, sizeof(int));
        }
        catch
        {
            // 读取失败保留 -1
        }
    }

    private static void Log(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "theme.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {msg}" + Environment.NewLine);
        }
        catch
        {
            // 日志失败不影响功能
        }
    }

    /// <summary>DWMWA_SYSTEMBACKDROP_TYPE：1=None 2=Mica 3=Acrylic 4=Tabbed。</summary>
    private const int DwmBackdropAttr = 38;

    /// <summary>直接通过 DWM 设置窗口材质。
    /// **不走 WPF-UI 的 WindowBackdropType setter**——它内部会重建窗口 chrome，
    /// 在本项目的资源字典结构下会抛 Freezable 上下文异常，
    /// 表现为"设置页切了材质没反应"。</summary>
    public static void ApplyBackdrop(Window window, WindowBackdropType backdrop)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            Log("backdrop: hwnd=0（句柄未就绪）");
            return;
        }
        // 优先用 WPF-UI 的 setter：它会同时处理窗口 chrome 与合成，
        // 只改 DWM 属性会导致材质不合成（整块变黑）。字典重复的坑修掉后，
        // setter 不再抛 Freezable 异常；万一仍失败，退回 DWM 直调。
        try
        {
            if (window is FluentWindow fluent)
            {
                fluent.WindowBackdropType = backdrop;
                // 校验：WPF-UI setter 不保证落到 DWM 属性（实测有时保持 1=无，
                // 表现为"设置 acrylic 重启后效果消失"）——不符则 DWM 直设兜底。
                var expect = backdrop switch
                {
                    WindowBackdropType.Mica => 2,
                    WindowBackdropType.Acrylic => 3,
                    WindowBackdropType.Tabbed => 4,
                    _ => 1,
                };
                var actual = -1;
                DwmGetWindowAttributeInt(hwnd, DwmBackdropAttr, ref actual);
                if (actual != expect)
                {
                    var force = expect;
                    DwmSetWindowAttribute(hwnd, DwmBackdropAttr, ref force, sizeof(int));
                    Log($"backdrop={backdrop} setter 未生效(read={actual})→ DWM 直设");
                }
                else
                {
                    Log($"backdrop={backdrop} via WPF-UI setter (dwm={actual})");
                }
                return;
            }
        }
        catch (Exception ex)
        {
            Log($"WPF-UI setter failed ({backdrop}): {ex.Message}；回退 DWM");
        }

        var value = backdrop switch
        {
            WindowBackdropType.Mica => 2,
            WindowBackdropType.Acrylic => 3,
            WindowBackdropType.Tabbed => 4,
            _ => 1,
        };
        var hr = DwmSetWindowAttribute(
            hwnd, DwmBackdropAttr, ref value, sizeof(int));
        // Mica 旧属性（Win10 21H2 / 早期 Win11）
        if (backdrop == WindowBackdropType.Mica)
        {
            var on = 1;
            DwmSetWindowAttribute(hwnd, 1029, ref on, sizeof(int));
        }
        var read = -1;
        DwmGetWindowAttributeInt(hwnd, DwmBackdropAttr, ref read);
        Log($"backdrop={backdrop} hr={hr} readback={read}");
    }

    /// <summary>只处理窗口级主题（深色标志 + 材质），**不调用
    /// ApplicationThemeManager.Apply**：后者会重建窗口 chrome，
    /// 在句柄刚就绪时可能抛 Freezable 上下文异常并中断调用方（导航）。</summary>
    public static void ApplyWindowChromeOnly()
    {
        try
        {
            if (Application.Current?.MainWindow is not FluentWindow fw)
            {
                return;
            }
            var mode = OasSettings.ThemeMode;
            var dark = mode switch
            {
                "light" => false,
                "dark" => true,
                _ => IsSystemDark(),
            };
            IsDark = dark;
            var backdrop = ResolveBackdropType();
            SetImmersiveDarkMode(fw, dark);
            ApplyBackdrop(fw, backdrop);
            _lastDark = dark;
            _lastBackdrop = backdrop;
            _themedWindow = new WeakReference<Window>(fw);
        }
        catch (Exception ex)
        {
            Log("ApplyWindowChromeOnly failed: " + ex.Message);
        }
    }

    /// <summary>QA：记录 WPF-UI setter 的结果（成功/异常类型）。</summary>
    public static void LogUiSetterResult(WindowBackdropType backdrop, Exception? ex)
    {
        Log(ex is null
            ? $"WPF-UI setter OK: {backdrop}"
            : $"WPF-UI setter EX: {backdrop} -> {ex.GetType().Name}: {ex.Message}");
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
            // 主题切换会同时触发 General 与 Color 两类通知，两个都接；
            // 回调运行在后台线程 → Apply 内部会切回 UI 线程。
            if (args.Category != UserPreferenceCategory.General &&
                args.Category != UserPreferenceCategory.Color)
            {
                return;
            }
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
    /// <summary>语义色字典热切换：替换合并字典中「我们自己的」Light/Dark.xaml。
    /// 判定必须精确——WPF-UI 自己的主题字典文件名也是 Dark.xaml，
    /// 模糊匹配会误伤（曾导致 pane 用旧主题、深浅色乱串 + 字典无限追加）。</summary>
    private static void ApplySemanticDictionary(bool dark)
    {
        var dicts = Application.Current.Resources.MergedDictionaries;
        var target = new Uri(
            $"pack://application:,,,/Themes/{(dark ? "Dark" : "Light")}.xaml");

        static bool IsOurs(ResourceDictionary d)
        {
            var src = d.Source?.OriginalString ?? "";
            if (src.Length == 0)
            {
                return false;
            }
            var isThemeFile =
                src.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                src.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase);
            if (!isThemeFile)
            {
                return false;
            }
            // 相对路径（App.xaml 声明）或本程序集 component URI
            return !src.Contains(";component/", StringComparison.OrdinalIgnoreCase) ||
                   src.Contains("OASX.WPF;component/", StringComparison.OrdinalIgnoreCase);
        }

        // 移除所有旧语义字典（含历史重复/错误追加项），记住原位置
        var insertAt = -1;
        for (var i = dicts.Count - 1; i >= 0; i--)
        {
            if (!IsOurs(dicts[i]))
            {
                continue;
            }
            insertAt = i;
            dicts.RemoveAt(i);
        }

        var dict = new ResourceDictionary { Source = target };
        if (insertAt >= 0 && insertAt <= dicts.Count)
        {
            dicts.Insert(insertAt, dict);
        }
        else
        {
            dicts.Add(dict);
        }
    }
}
