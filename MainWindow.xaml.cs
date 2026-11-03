using System.IO;
using System.Reflection;
using System.Windows;
using OASX.Services;
using OASX.Views;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace OASX;

public partial class MainWindow : FluentWindow
{
    /// <summary>ContentDialog 浮层宿主（WPF-UI 对话框渲染点）。</summary>
    public ContentPresenter DialogHost => RootDialogHost;

    public MainWindow()
    {
        InitializeComponent();

        // BGI 同款：页面服务 + TargetPageType，导航全部由 NavigationView
        // 内部接管（点击 → 内部 Navigate → 页面服务出实例）。
        RootNavigation.SetPageProviderService(new PageService());

        // 初始导航推迟到 Loaded：构造函数里模板未应用，
        // 内部 presenter 为 null 会 NRE。
        Loaded += (_, _) =>
        {
            WireNavigationParent();
            RootNavigation.Navigate(typeof(DashboardPage));
        };
    }

    /// <summary>
    /// ⚠️ WPF-UI 4.3.0 的致命缺陷 workaround：NavigationViewItem.OnClick
    /// 第一行是 GetNavigationParent(this) 为 null 则静默 return——而库自身
    /// **从不给 MenuItems 设置 NavigationParent 附加属性**（只在导航后的
    /// 页面实例上设置）。没有它，所有条目点击都是死按钮。
    /// 这里用反射取 internal 的 NavigationParentProperty，手动补挂到
    /// 每个条目上。（BGI 能工作是因为它的 DI/行为链间接触发了别的路径。）
    /// </summary>
    private void WireNavigationParent()
    {
        var dpField = typeof(NavigationView).GetField(
            "NavigationParentProperty", BindingFlags.NonPublic | BindingFlags.Static);
        if (dpField?.GetValue(null) is not DependencyProperty dp)
        {
            File.AppendAllText("nav.log", "WARN: NavigationParentProperty not found\n");
            return;
        }

        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.GetValue(dp) is not NavigationView)
            {
                item.SetValue(dp, RootNavigation);
            }
        }
        foreach (var item in RootNavigation.FooterMenuItems.OfType<NavigationViewItem>())
        {
            if (item.GetValue(dp) is not NavigationView)
            {
                item.SetValue(dp, RootNavigation);
            }
        }
    }
}
