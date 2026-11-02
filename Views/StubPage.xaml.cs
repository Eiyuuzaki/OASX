using System.Windows.Controls;

namespace OASX.Views;

/// <summary>占位页：并行开发期间尚未迁移的页面。</summary>
public partial class StubPage : UserControl
{
    public StubPage(string title)
    {
        InitializeComponent();
        TitleText.Text = title;
    }
}
