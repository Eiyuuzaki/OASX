using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OASX.Services;

/// <summary>
/// 分享图渲染（纯前端）：把当前账号资产渲染成 1080 宽 PNG。
/// 2x DPI 渲染保证清晰度；保存到「图片\OASX」并返回路径。
/// </summary>
public static class ShareImageRenderer
{
    private const double ImageWidth = 1080;

    /// <summary>渲染资产分享图；assets 为 (名称, 数量, 增减)。</summary>
    public static string RenderAssets(
        string accountName, string accountDetail, string date,
        IReadOnlyList<(string Name, string Count, string Delta, bool Up)> assets)
    {
        var root = BuildVisual(accountName, accountDetail, date, assets);
        return SavePng(root, $"OASX资产_{date}_{DateTime.Now:HHmmss}.png");
    }

    private static FrameworkElement BuildVisual(string accountName,
        string accountDetail, string date,
        IReadOnlyList<(string Name, string Count, string Delta, bool Up)> assets)
    {
        var surface = Brush("OasSurfaceBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
        var border = Brush("OasBorderBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
        var text1 = Brush("OasTextPrimaryBrush", Color.FromRgb(0x24, 0x24, 0x24));
        var text2 = Brush("OasTextSecondaryBrush", Color.FromRgb(0x61, 0x61, 0x61));
        var accent = Brush("OasAccentBrush", Color.FromRgb(0x25, 0x63, 0xEB));
        var up = Brush("OasSuccessBrush", Color.FromRgb(0x16, 0xA3, 0x4A));
        var down = Brush("OasDangerBrush", Color.FromRgb(0xDC, 0x45, 0x45));

        var panel = new StackPanel
        {
            Width = ImageWidth,
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6)),
        };

        // 头部
        var header = new Border
        {
            Background = surface,
            Padding = new Thickness(36, 28, 36, 24),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "OASX · 资产快照",
                        FontSize = 34,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = accent,
                        FontFamily = AppFont(),
                    },
                    new TextBlock
                    {
                        Text = $"{accountName}　{accountDetail}",
                        FontSize = 20,
                        Margin = new Thickness(0, 10, 0, 0),
                        Foreground = text1,
                        FontFamily = AppFont(),
                    },
                    new TextBlock
                    {
                        Text = $"数据日期 {date}　·　生成于 {DateTime.Now:yyyy-MM-dd HH:mm}",
                        FontSize = 16,
                        Margin = new Thickness(0, 6, 0, 0),
                        Foreground = text2,
                        FontFamily = AppFont(),
                    },
                },
            },
        };
        panel.Children.Add(header);

        // 资产网格（每行 4 个）
        var grid = new WrapPanel
        {
            Margin = new Thickness(20, 20, 20, 8),
            ItemWidth = 250,
            ItemHeight = 108,
        };
        foreach (var a in assets.Take(20))
        {
            grid.Children.Add(new Border
            {
                Width = 244,
                Height = 100,
                Margin = new Thickness(4, 0, 4, 8),
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(10),
                Background = surface,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = a.Name,
                            FontSize = 16,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = text1,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            FontFamily = AppFont(),
                        },
                        new TextBlock
                        {
                            Text = a.Count,
                            FontSize = 26,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = text1,
                            FontFamily = AppFont(),
                        },
                        new TextBlock
                        {
                            Text = string.IsNullOrEmpty(a.Delta)
                                ? " " : (a.Up ? "+" : "") + a.Delta,
                            FontSize = 15,
                            Foreground = a.Up ? up : down,
                            FontFamily = AppFont(),
                        },
                    },
                },
            });
        }
        panel.Children.Add(grid);

        // 页脚
        panel.Children.Add(new Border
        {
            Background = surface,
            Padding = new Thickness(36, 14, 36, 14),
            Child = new TextBlock
            {
                Text = "OASX · OAS 自动化客户端　|　数据来自游戏内资源采集",
                FontSize = 14,
                Foreground = text2,
                FontFamily = AppFont(),
            },
        });

        return panel;
    }

    /// <summary>RenderTargetBitmap 2x 渲染并落盘到「图片\OASX」。</summary>
    private static string SavePng(FrameworkElement visual, string fileName)
    {
        const double scale = 2.0;
        visual.Measure(new Size(ImageWidth, double.PositiveInfinity));
        visual.Arrange(new Rect(new Size(ImageWidth, visual.DesiredSize.Height)));
        visual.UpdateLayout();

        var w = (int)Math.Ceiling(ImageWidth * scale);
        var h = (int)Math.Ceiling(visual.ActualHeight * scale);
        var bmp = new RenderTargetBitmap(w, h, 96 * scale, 96 * scale,
            PixelFormats.Pbgra32);
        bmp.Render(visual);

        var dir = ResolveOutputDir();
        var path = Path.Combine(dir, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
        return path;
    }

    /// <summary>输出目录：图片 → 文档 → exe 目录（宿主/ACL 限制下的可写回退）。</summary>
    private static string ResolveOutputDir()
    {
        var candidates = new List<string>();
        var pictures = Environment.GetFolderPath(
            Environment.SpecialFolder.MyPictures);
        var documents = Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments);
        if (pictures.Length > 0) candidates.Add(Path.Combine(pictures, "OASX"));
        if (documents.Length > 0) candidates.Add(Path.Combine(documents, "OASX"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "share"));

        foreach (var dir in candidates)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var probe = Path.Combine(dir, ".probe");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return dir;
            }
            catch
            {
                // 该目录不可写，试下一个
            }
        }
        return candidates[^1];
    }

    private static FontFamily AppFont() =>
        Application.Current.TryFindResource("AppFontFamily") as FontFamily
        ?? new FontFamily("Microsoft YaHei UI");

    private static SolidColorBrush Brush(string key, Color fallback) =>
        Application.Current.TryFindResource(key) as SolidColorBrush
        ?? new SolidColorBrush(fallback);
}