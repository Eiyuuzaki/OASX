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
        if (RootNavigation.SelectedItem is NavigationViewItem { Tag: { } tag })
        {
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
        }
    }
}
