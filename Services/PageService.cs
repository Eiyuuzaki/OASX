using Wpf.Ui.Abstractions;

namespace OASX.Services;

/// <summary>
/// WPF-UI NavigationView 的页面服务：内部导航按 TargetPageType 从这里
/// 取页面实例（对应 BGI 的 NavigationViewPageProvider）。
/// </summary>
public class PageService : INavigationViewPageProvider
{
    /// <summary>每次导航创建新实例。LogPage 等有状态页面在
    /// OnNavigatedFrom 里自行清理资源。</summary>
    public T GetPage<T>() where T : class
    {
        return (T)Activator.CreateInstance(typeof(T))!;
    }

    /// <summary>非泛型入口（NavigationView 内部导航走这里）。
    /// 实例化失败不再静默：异常落 page_fail.log（导航链会吞掉异常，
    /// 表现成"点了没反应"，没有日志几乎无法定位）。</summary>
    public object? GetPage(Type pageType)
    {
        try
        {
            return Activator.CreateInstance(pageType);
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "page_fail.log"),
                    $"{DateTime.Now:HH:mm:ss} {pageType.FullName}\n{ex}\n\n");
            }
            catch
            {
                // 连日志都写不了则放弃（避免递归）
            }
            return null;
        }
    }
}
