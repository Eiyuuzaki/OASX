using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

/// <summary>
/// 关于页：应用信息（版本/后端状态）、组件与致谢、环境信息。
/// 全部为真实数据（程序集版本 / OS / .NET / 目录 / 后端探测）。
/// </summary>
public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        VersionText.Text = $"v{Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "?"}";
        BackendText.Text = OasSettings.BackendAddress;
        UpdateStatusText.Text = $"当前版本 v{UpdateChecker.CurrentVersion}";

        BuildCredits();
        BuildEnvironment();
        await PingBackendAsync();
    }

    /// <summary>组件与致谢行（图标 + 名称 + 说明）。</summary>
    private void BuildCredits()
    {
        var rows = new (string Symbol, string Name, string Detail)[]
        {
            ("Server24", "OAS（OnmyojiAutoScript）",
                "后端脚本引擎与调度器 · 由 runhey 及社区开发"),
            ("PaintBrush24", "MiSans",
                "界面字体 · 小米发布，免费商用"),
            ("Info24", "WPF-UI 4.3.0",
                "Fluent 控件与主题 · lepoco（MIT）"),
            ("TasksApp24", "WPF / .NET 8",
                "桌面应用框架（Microsoft，MIT）"),
        };
        foreach (var (symbol, name, detail) in rows)
        {
            CreditsPanel.Children.Add(BuildRow(symbol, name, detail));
        }
    }

    /// <summary>检查 GitHub Releases 是否有新版本。</summary>
    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "检查中…";
        try
        {
            var result = await UpdateChecker.CheckAsync();
            if (!result.Success)
            {
                UpdateStatusText.Text = $"检查失败：{result.Error}";
                return;
            }
            if (!result.HasUpdate)
            {
                UpdateStatusText.Text = result.Note is null
                    ? $"已是最新（v{result.CurrentVersion}）"
                    : $"v{result.CurrentVersion} · {result.Note}";
                return;
            }
            UpdateStatusText.Text =
                $"发现新版本 {result.LatestVersion}（当前 v{result.CurrentVersion}）";
            var msg = result.Note ?? "";
            var choice = System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                string.Join(Environment.NewLine,
                    $"发现新版本 {result.LatestVersion}（当前 v{result.CurrentVersion}）",
                    msg,
                    "",
                    "要打开发布页下载吗？"),
                "OASX 更新", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (choice == MessageBoxResult.Yes)
            {
                OpenUrl(result.DownloadUrl ?? result.ReleaseUrl
                        ?? "https://github.com/Eiyuuzaki/OASX/releases");
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"检查失败：{ex.Message}";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    /// <summary>仓库主页（用系统默认浏览器打开）。</summary>
    private void Repo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        OpenUrl("https://github.com/Eiyuuzaki/OASX");

    /// <summary>问题反馈（GitHub Issues）。</summary>
    private void Issues_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        OpenUrl("https://github.com/Eiyuuzaki/OASX/issues");

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打不开浏览器不影响页面
        }
    }

    /// <summary>环境信息行。</summary>
    private void BuildEnvironment()
    {
        var rows = new (string Symbol, string Name, string Detail)[]
        {
            ("WindowNew24", "操作系统", RuntimeInformation.OSDescription),
            ("Rocket24", "运行时", RuntimeInformation.FrameworkDescription),
            ("Folder24", "程序目录", AppContext.BaseDirectory.TrimEnd('\\')),
        };
        foreach (var (symbol, name, detail) in rows)
        {
            EnvPanel.Children.Add(BuildRow(symbol, name, detail));
        }
    }

    private static UIElement BuildRow(string symbol, string name, string detail)
    {
        var icon = new Wpf.Ui.Controls.SymbolIcon
        {
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        if (Enum.TryParse<Wpf.Ui.Controls.SymbolRegular>(symbol, out var sym))
        {
            icon.Symbol = sym;
        }
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 13,
            Foreground = FindBrush("OasTextPrimaryBrush"),
        });
        text.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = FindBrush("OasTextSecondaryBrush"),
        });
        var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(icon);
        row.Children.Add(text);
        return row;
    }

    private async Task PingBackendAsync()
    {
        BackendStateText.Text = "检测中…";
        try
        {
            _ = await OasApi.GetConfigListAsync();
            BackendDot.Fill = new SolidColorBrush(
                Color.FromRgb(0x16, 0xA3, 0x4A));
            BackendStateText.Text = "已连接";
        }
        catch
        {
            BackendDot.Fill = new SolidColorBrush(
                Color.FromRgb(0xDC, 0x45, 0x45));
            BackendStateText.Text = "不可达";
        }
    }

    private static Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
}