using System.IO;
using OASX.Services;
using System.Windows;

namespace OASX;

public partial class App : Application
{
    /// <summary>主窗口引用（设置页切主题时用）。</summary>
    public static System.Windows.Window? MainWindow { get; set; }

    private static readonly string CrashLogPath = Path.Combine(
        AppContext.BaseDirectory, "crash.log");

    public App()
    {
        // 全局异常落盘：启动后延迟崩溃没有控制台可看，落文件才能定位
        DispatcherUnhandledException += (_, e) =>
        {
            File.WriteAllText(CrashLogPath, "Dispatcher: " + e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            File.AppendAllText(CrashLogPath,
                "AppDomain: " + e.ExceptionObject + "\n");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            File.AppendAllText(CrashLogPath,
                "UnobservedTask: " + e.Exception + "\n");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.WatchSystem();
        ThemeService.Apply();

        // 自动拉起后端（设置项，失败不阻塞 UI）
        _ = OasBackendLauncher.EnsureRunningAsync();
    }

    /// <summary>把持久化的主题应用到全局（设置页切换时也会调用）。</summary>
    public static void ApplySavedTheme() => ThemeService.Apply();
}
