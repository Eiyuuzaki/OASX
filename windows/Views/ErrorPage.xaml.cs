using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OASX.Services;

namespace OASX.Views;

/// <summary>错误记录页：列出后端归档的异常目录，右侧看 log.txt 与截图。
///
/// 对齐 Flutter error_records_panel + error_records_repository。
/// 数据源：GET /logs/errors、/logs/errors/{id}、/logs/errors/{id}/images/{name}。
/// </summary>
public partial class ErrorPage : UserControl
{
    public sealed class ErrorRow
    {
        public string Id { get; init; } = "";
        public string TimeText { get; init; } = "";
        public string SubText { get; init; } = "";
        public OasErrorApi.ErrorItem Source { get; init; } = null!;
    }

    /// <summary>当前打开的详情（复制/保存用）。</summary>
    private OasErrorApi.ErrorDetail? _current;

    public ErrorPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private string? CurrentScript()
    {
        if (ScopeSelector?.SelectedIndex != 1) return null;
        return GlobalAccountContext.Instance.Current?.Config;
    }

    private async void ScopeSelector_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await RefreshAsync();

    private async Task RefreshAsync()
    {
        var items = await OasErrorApi.ListAsync(CurrentScript());
        var rows = items.Select(x => new ErrorRow
        {
            Id = x.Id,
            TimeText = x.Time.Replace('T', ' ')[..Math.Min(19, x.Time.Length)],
            SubText = $"{(x.ScriptName ?? "旧格式")} · {x.LogSize / 1024} KB"
                      + (x.ImageCount > 0 ? $" · {x.ImageCount} 图" : ""),
            Source = x,
        }).ToList();

        ErrorList.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = rows.Count == 0 ? "" : $"{rows.Count} 条";

        if (rows.Count > 0 && ErrorList.SelectedIndex < 0)
        {
            ErrorList.SelectedIndex = 0;
        }
        else if (rows.Count == 0)
        {
            DetailTitle.Text = "没有错误记录";
            DetailMeta.Text = "运行异常时后端会自动归档";
            LogText.Text = "";
            ImagePanel.Children.Clear();
        }
    }

    private async void ErrorList_SelectionChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (ErrorList.SelectedItem is not ErrorRow row) return;
        DetailTitle.Text = "加载中…";
        DetailMeta.Text = "";
        LogText.Text = "";
        ImagePanel.Children.Clear();
        _current = null;
        CopyLogButton.IsEnabled = false;
        SaveLogButton.IsEnabled = false;
        SaveAllImagesButton.IsEnabled = false;

        var detail = await OasErrorApi.DetailAsync(row.Id);
        if (detail is null)
        {
            DetailTitle.Text = "详情加载失败";
            DetailMeta.Text = "后端未返回内容";
            return;
        }

        DetailTitle.Text = (detail.ScriptName ?? "未知脚本")
                           + " · " + detail.Time.Replace('T', ' ');
        DetailMeta.Text = $"{detail.Log?.FileName ?? "log.txt"} · "
                          + $"{detail.Log?.Content.Length ?? 0} 字符 · "
                          + $"{detail.Images.Count} 张截图";
        _current = detail;
        LogText.Text = detail.Log?.Content ?? "（无内容）";
        CopyLogButton.IsEnabled = true;
        SaveLogButton.IsEnabled = true;
        SaveAllImagesButton.IsEnabled = detail.Images.Count > 0;

        // 截图：逐张加载
        foreach (var img in detail.Images)
        {
            var bmp = await LoadImageAsync(OasErrorApi.ImageUrl(detail.Id, img.Name));
            if (bmp is null) continue;
            ImagePanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new Image
                {
                    Source = bmp,
                    Stretch = Stretch.Uniform,
                    MaxHeight = 240,
                },
            });
        }
    }

    /// <summary>下载并解码一张截图（失败返回 null，不抛）。</summary>
    private static async Task<BitmapImage?> LoadImageAsync(string url)
    {
        try
        {
            var bytes = await OasApi.Http.GetByteArrayAsync(url);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(bytes);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    // ==================== 复制 / 保存（对齐 Flutter error_records_panel） ====================

    /// <summary>把当前错误日志复制到剪贴板。</summary>
    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        var text = _current?.Log?.Content;
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            Clipboard.SetText(text);
            DetailMeta.Text = "已复制日志到剪贴板";
        }
        catch (Exception ex)
        {
            DetailMeta.Text = $"复制失败：{ex.Message}";
        }
    }

    /// <summary>另存为 .txt（默认文件名对齐 Flutter：错误日志-{id}.txt）。</summary>
    private void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        var detail = _current;
        var text = detail?.Log?.Content;
        if (detail is null || string.IsNullOrEmpty(text)) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存错误日志",
            FileName = $"错误日志-{detail.Id}.txt",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            System.IO.File.WriteAllText(dlg.FileName, text, System.Text.Encoding.UTF8);
            DetailMeta.Text = $"已保存到 {dlg.FileName}";
        }
        catch (Exception ex)
        {
            DetailMeta.Text = $"保存失败：{ex.Message}";
        }
    }

    /// <summary>把该记录的所有截图保存到用户选择的目录。</summary>
    private async void SaveAllImages_Click(object sender, RoutedEventArgs e)
    {
        var detail = _current;
        if (detail is null || detail.Images.Count == 0) return;

        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择截图保存目录" };
        if (dlg.ShowDialog() != true) return;

        var ok = 0;
        var fail = 0;
        foreach (var img in detail.Images)
        {
            try
            {
                var bytes = await OasApi.Http.GetByteArrayAsync(
                    OasErrorApi.ImageUrl(detail.Id, img.Name));
                var safe = img.Name;
                foreach (var ch in new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
                    safe = safe.Replace(ch, '_');
                var path = System.IO.Path.Combine(dlg.FolderName, safe);
                await System.IO.File.WriteAllBytesAsync(path, bytes);
                ok++;
            }
            catch
            {
                fail++;
            }
        }
        DetailMeta.Text = fail == 0
            ? $"已保存 {ok} 张截图到 {dlg.FolderName}"
            : $"已保存 {ok} 张，{fail} 张失败";
    }
}
