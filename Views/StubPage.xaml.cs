using System.Windows.Controls;

namespace OASX.Views;

/// <summary>占位页：并行开发期间尚未迁移的页面。</summary>
/// ⚠️ 必须有无参构造：WPF-UI 的 PageService 走 Activator.CreateInstance(Type)
/// 实例化页面——只有带参构造时 MissingMethodException 会被
/// DispatcherUnhandledException 吞掉，表现为"点击导航没反应"。
public partial class StubPage : UserControl
{
    public StubPage()
    {
        InitializeComponent();
        TitleText.Text = "开发中";
    }

    public StubPage(string title)
    {
        InitializeComponent();
        TitleText.Text = title;
    }
}
