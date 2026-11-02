using System.IO;
using OASX.Services;
using OASX.Views;
using Wpf.Ui.Controls;

namespace OASX;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // BGI 同款：页面服务 + TargetPageType，导航全部由 NavigationView
        // 内部接管（点击 → 内部 Navigate → 页面服务出实例）。
        // 不要订阅它的 SelectionChanged 再自管 Frame——ContentOverlay/
        // 自管 Frame 模式会替换掉内部内容管线，事件链整个断开
        // （实测踩坑：条目能高亮但事件不发、点击无声失败）。
        RootNavigation.SetPageProviderService(new PageService());

        // 初始导航推迟到 Loaded：构造函数里模板未应用，
        // 内部 presenter 为 null 会 NRE。
        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }
}
