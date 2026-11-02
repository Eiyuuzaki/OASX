using System.Windows;
using System.Windows.Controls;
using OASX.Views;
using Wpf.Ui.Controls;

namespace OASX;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        App.MainWindow = this;
        // ⚠️ 初始导航必须推迟到 Loaded：构造函数里控件模板尚未应用，
        // NavigateInternal → UpdateContent 会因内部 presenter 为 null 而
        // NRE（crash.log 实锤，表现为窗口永远打不开）。
        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }

    private void RootNavigation_SelectionChanged(object sender, RoutedEventArgs e)
    {
        System.IO.File.AppendAllText("nav.log", "SelectionChanged fired");
        // ⚠️ 导航必须推迟出事件分发期：在 NavigationView 内部选中处理
        // 的栈里同步 Frame.Navigate 会重入其内部状态机（实测进程无
        // 声明地消失，连托管异常处理器都来不及写 crash.log）。
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (RootNavigation.SelectedItem is NavigationViewItem { Tag: { } tag })
                {
                    System.IO.File.AppendAllText("nav.log", " -> " + tag);
                    ContentFrame.Navigate(tag switch
                    {
                        "dashboard" => new DashboardPage(),
                        "workbench" => new StubPage("工作台"),
                        "statistics" => new StubPage("统计"),
                        "resource" => new StubPage("资产"),
                        "log" => new LogPage(),
                        "settings" => new SettingsPage(),
                        _ => new DashboardPage(),
                    });
                    System.IO.File.AppendAllText("nav.log", " navigated");
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("nav.log", " ERROR: " + ex.Message);
            }
        });
    }
}
