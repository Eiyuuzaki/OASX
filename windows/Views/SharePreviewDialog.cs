using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OASX.Views;

/// <summary>
/// 分享图预览弹窗：展示刚生成的 PNG + 复制路径。
/// 刻意**不启动任何外部进程**（explorer 在受限环境会以 0xc0000142 崩掉并弹系统错误框，
/// 被误认为 OASX 崩溃）。
/// </summary>
public static class SharePreviewDialog
{
    public static void Show(Window? owner, string imagePath)
    {
        var win = new Wpf.Ui.Controls.FluentWindow
        {
            Title = "分享图已生成",
            Width = 780,
            Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.CanResize,
            ExtendsContentIntoTitleBar = false,
        };

        var root = new DockPanel { Margin = new Thickness(16) };

        // 底部：路径 + 按钮
        var bottom = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var copy = new Wpf.Ui.Controls.Button
        {
            Content = "复制路径",
            MinWidth = 96,
            MinHeight = 32,
            Margin = new Thickness(0, 0, 8, 0),
        };
        copy.Click += (_, _) =>
        {
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    Clipboard.SetText(imagePath);
                    copy.Content = "已复制";
                    break;
                }
                catch
                {
                    System.Threading.Thread.Sleep(120);
                }
            }
        };
        var close = new Wpf.Ui.Controls.Button
        {
            Content = "关闭",
            MinWidth = 96,
            MinHeight = 32,
            Appearance = Wpf.Ui.Controls.ControlAppearance.Primary,
        };
        close.Click += (_, _) => win.Close();
        bottom.Children.Add(copy);
        bottom.Children.Add(close);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        var path = new TextBlock
        {
            Text = imagePath,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Application.Current.TryFindResource(
                "OasTextSecondaryBrush") as Brush ?? Brushes.Gray,
        };
        DockPanel.SetDock(path, Dock.Bottom);
        root.Children.Add(path);

        // 图片预览
        var image = new Image
        {
            Source = LoadImage(imagePath),
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 0),
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Application.Current.TryFindResource(
                "OasSurfaceSecondaryBrush") as Brush ?? Brushes.WhiteSmoke,
            Child = image,
        };
        root.Children.Add(border);

        win.Content = root;
        win.ShowDialog();
    }

    private static BitmapImage? LoadImage(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}