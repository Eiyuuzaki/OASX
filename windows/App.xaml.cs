using System.Collections.Generic;
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
        // 兜底：UI 线程以外的未处理异常（DispatcherUnhandledException 抓不到）
        // 也落盘，否则进程静默退出时完全没有线索
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                var ex = args.ExceptionObject as Exception;
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} AppDomain " +
                    $"(terminating={args.IsTerminating}) ===\n{ex}\n\n");
            }
            catch
            {
                // 兜底日志失败无从处理
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} unobserved task ===\n" +
                    $"{args.Exception}\n\n");
            }
            catch
            {
                // 同上
            }
        };

        ThemeService.WatchSystem();
        ThemeService.Apply();

        // 启动链：先确保后端就绪，**再**加载账号上下文。
        // 早期两行都是 fire-and-forget 并发发出——后端还在启动时账号已加载完，
        // 结果账号列表为空且不再重试（发布版首次启动必现）。
        _ = StartServicesAsync();

    }

    /// <summary>启动序列：后端就绪 → 账号上下文就绪。</summary>
    private static async Task StartServicesAsync()
    {
        try
        {
            await OasBackendLauncher.EnsureRunningAsync();
        }
        catch
        {
            // 后端启动失败不阻塞 UI；账号加载会记录错误
        }
        await GlobalAccountContext.Instance.LoadAccountsAsync();
    }

    /// <summary>把持久化的主题应用到全局（设置页切换时也会调用）。</summary>
    public static void ApplySavedTheme() => ThemeService.Apply();
}
