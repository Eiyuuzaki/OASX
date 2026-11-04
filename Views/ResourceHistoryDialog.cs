using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OASX.Services;

namespace OASX.Views;

/// <summary>
/// 资源涨势图弹窗：Canvas 自绘折线（无第三方图表库）。
/// 数据源 /resource/{config}/history（按名称跨期聚合）。
/// </summary>
public static class ResourceHistoryDialog
{
    private const double ChartWidth = 560;
    private const double ChartHeight = 210;
    private const double PadLeft = 52;
    private const double PadRight = 14;
    private const double PadTop = 14;
    private const double PadBottom = 26;

    public static void Show(Window? owner, string itemName,
        List<OasResourceApi.HistoryPoint> points)
    {
        var win = new Wpf.Ui.Controls.FluentWindow
        {
            Title = $"{itemName} · 涨势",
            Width = 640,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            ExtendsContentIntoTitleBar = false,
        };

        var root = new StackPanel { Margin = new Thickness(20) };

        // 头部：名称 + 统计
        root.Children.Add(new TextBlock
        {
            Text = itemName,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            FontFamily = (FontFamily)Application.Current
                .FindResource("AppFontFamily"),
            Foreground = FindBrush("OasTextPrimaryBrush"),
        });

        if (points.Count == 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = "暂无历史数据",
                FontSize = 12,
                Margin = new Thickness(0, 10, 0, 0),
                Foreground = FindBrush("OasTextSecondaryBrush"),
            });
        }
        else
        {
            var first = points[0];
            var last = points[^1];
            var max = points.Max(p => p.Count);
            var min = points.Min(p => p.Count);
            var delta = last.Count - first.Count;
            var deltaText = delta == 0 ? "持平" : (delta > 0 ? $"+{delta:N0}" : $"{delta:N0}");

            root.Children.Add(new TextBlock
            {
                Text = $"当前 {last.Count:N0}　{deltaText}（{first.Date} → {last.Date}）" +
                       $"　最高 {max:N0} / 最低 {min:N0}",
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 12),
                Foreground = delta >= 0 ? FindBrush("OasSuccessBrush")
                    : FindBrush("OasDangerBrush"),
            });

            root.Children.Add(BuildChart(points));
        }

        var close = new Wpf.Ui.Controls.Button
        {
            Content = "关闭",
            MinWidth = 88,
            MinHeight = 32,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        close.Click += (_, _) => win.Close();
        root.Children.Add(close);

        win.Content = root;
        win.ShowDialog();
    }

    /// <summary>自绘折线：网格 + 折线 + 数据点 + 轴标签。</summary>
    private static Canvas BuildChart(List<OasResourceApi.HistoryPoint> points)
    {
        var canvas = new Canvas
        {
            Width = ChartWidth,
            Height = ChartHeight,
        };

        var max = points.Max(p => p.Count);
        var min = points.Min(p => p.Count);
        // Y 轴范围留 10% 余量；max==min 时给最小跨度避免除零
        var span = Math.Max(max - min, 1);
        var top = max + span * 0.1;
        var bottom = Math.Max(0, min - span * 0.1);
        var plotW = ChartWidth - PadLeft - PadRight;
        var plotH = ChartHeight - PadTop - PadBottom;

        double X(int i) => points.Count == 1
            ? PadLeft + plotW / 2
            : PadLeft + plotW * i / (points.Count - 1);
        double Y(long count) => PadTop + plotH * (1 - (count - bottom) / (top - bottom));

        // 网格（横向 4 条）+ Y 轴标签（3 个刻度）
        var gridBrush = FindBrush("OasBorderBrush");
        for (var g = 0; g <= 3; g++)
        {
            var y = PadTop + plotH * g / 3;
            canvas.Children.Add(new Line
            {
                X1 = PadLeft, X2 = ChartWidth - PadRight,
                Y1 = y, Y2 = y,
                Stroke = gridBrush, StrokeThickness = 1,
            });
            var value = (long)Math.Round(top - (top - bottom) * g / 3);
            var label = new TextBlock
            {
                Text = value.ToString("N0"),
                FontSize = 10,
                Foreground = FindBrush("OasTextTertiaryBrush"),
            };
            Canvas.SetLeft(label, 4);
            Canvas.SetTop(label, y - 8);
            canvas.Children.Add(label);
        }

        // 折线 + 数据点
        var lineBrush = FindBrush("OasAccentBrush");
        var poly = new Polyline
        {
            Stroke = lineBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
        };
        for (var i = 0; i < points.Count; i++)
        {
            poly.Points.Add(new Point(X(i), Y(points[i].Count)));
        }
        canvas.Children.Add(poly);

        for (var i = 0; i < points.Count; i++)
        {
            var dot = new Ellipse
            {
                Width = 6, Height = 6,
                Fill = lineBrush,
            };
            Canvas.SetLeft(dot, X(i) - 3);
            Canvas.SetTop(dot, Y(points[i].Count) - 3);
            dot.ToolTip = $"{points[i].Date}　{points[i].Count:N0}";
            canvas.Children.Add(dot);
        }

        // X 轴标签：首、尾（点多时中间补一个）
        var dates = new List<(int Index, string Text)>
        {
            (0, ShortDate(points[0].Date)),
            (points.Count - 1, ShortDate(points[^1].Date)),
        };
        if (points.Count >= 6)
        {
            dates.Insert(1, (points.Count / 2, ShortDate(points[points.Count / 2].Date)));
        }
        foreach (var (index, text) in dates)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = FindBrush("OasTextTertiaryBrush"),
            };
            Canvas.SetLeft(label, Math.Min(X(index) - 16, ChartWidth - 60));
            Canvas.SetTop(label, ChartHeight - 16);
            canvas.Children.Add(label);
        }

        return canvas;
    }

    private static string ShortDate(string date) =>
        date.Length >= 10 && date[0] == '2' ? date[5..] : date;

    private static Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
}
