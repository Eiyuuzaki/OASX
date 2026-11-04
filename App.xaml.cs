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
        ThemeService.WatchSystem();
        ThemeService.Apply();

        // 自动拉起后端（设置项，失败不阻塞 UI）
        _ = OasBackendLauncher.EnsureRunningAsync();

        // 启动即加载账号上下文：任何页面（绘卷/资产/仪表盘）都能直接读 Current，
        // 不必各自触发加载——否则直达某页时账号为空。
        _ = GlobalAccountContext.Instance.LoadAccountsAsync();

        // QA 自检：--share-demo 渲染一张样例分享图后退出（验证渲染管线）
        if (e.Args.Contains("--share-demo"))
        {
            _ = RenderShareDemoAsync();
        }
    }

    /// <summary>分享图自检：渲染样例并落盘，供无 UI 环境下验证。</summary>
    private static async Task RenderShareDemoAsync()
    {
        await Task.Delay(2500);   // 等资源字典与主题就绪
        try
        {
            var sample = new List<(string Name, string Count, string Delta, bool Up)>
            {
                ("金币", "1.4亿", "410.7万", true),
                ("勾玉", "9,535", "4,416", true),
                ("体力", "63.9万", "424", false),
                ("荣誉", "510", "6,980", false),
            };
            var path = Services.ShareImageRenderer.RenderAssets(
                "样例角色", "Lv60 · 海外加速区",
                DateTime.Today.ToString("yyyy-MM-dd"), sample);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "share_demo.log"),
                path + Environment.NewLine);
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "share_demo.log"),
                "FAIL: " + ex + Environment.NewLine);
        }
        Current.Shutdown();
    }

    /// <summary>把持久化的主题应用到全局（设置页切换时也会调用）。</summary>
    public static void ApplySavedTheme() => ThemeService.Apply();
}
