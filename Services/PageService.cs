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

    /// <summary>非泛型入口（NavigationView 内部导航走这里）。</summary>
    public object? GetPage(Type pageType)
    {
        return Activator.CreateInstance(pageType);
    }
}
