using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OASX.Services;
using OASX.Views;
using Wpf.Ui.Controls;

namespace OASX;

public partial class MainWindow : FluentWindow
{
    /// <summary>ContentDialog 浮层宿主。</summary>
    public System.Windows.Controls.ContentPresenter DialogHost => RootDialogHost;

    /// <summary>导航视图（子页面跳转入口，如 Dashboard → 工作台/日志）。</summary>
    public NavigationView RootNav => RootNavigation;

    private Wpf.Ui.Tray.Controls.NotifyIcon? _trayIcon;
    private bool _forceExit;

    public MainWindow()
    {
        InitializeComponent();

        // 句柄就绪即时应用主题：Deep色标志必须在材质生效前设置，
        // 否则 Acrylic/Mica 按浅色上色（深浅乱串）。此处不再提前设置材质。
        SourceInitialized += (_, _) => ThemeService.ApplyWindowChromeOnly();

        // 激活时重申深色标志：部分场景（DWM 重排、切换材质、系统事件）
        // 会把标志冲掉，导致材质按浅色上色（深浅乱串）。
        Activated += (_, _) => ThemeService.SetImmersiveDarkMode(
            this, ThemeService.IsDark);

        // QA：--backdrop-ui-test 用 WPF-UI 的 setter 切材质（验证是否仍抛异常）
        if (Environment.GetCommandLineArgs().Contains("--backdrop-ui-test"))
        {
            var t = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3),
            };
            var seqUi = new[]
            {
                WindowBackdropType.Acrylic,
                WindowBackdropType.Mica,
                WindowBackdropType.Acrylic,
            };
            var k = 0;
            t.Tick += (_, _) =>
            {
                if (k >= seqUi.Length) { t.Stop(); return; }
                try
                {
                    WindowBackdropType = seqUi[k];
                    ThemeService.LogUiSetterResult(seqUi[k], null);
                }
                catch (Exception ex)
                {
                    ThemeService.LogUiSetterResult(seqUi[k], ex);
                }
                k++;
            };
            t.Start();
        }

        // QA：--backdrop-test 走设置页路径，依次切换 无/Mica/Acrylic
        if (Environment.GetCommandLineArgs().Contains("--backdrop-test"))
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3),
            };
            // 走设置页的真实路径：写设置 → ThemeService.Apply()
            var seq = new[] { "none", "mica", "acrylic" };
            var idx = 0;
            timer.Tick += (_, _) =>
            {
                if (idx >= seq.Length)
                {
                    timer.Stop();
                    return;
                }
                OasSettings.BackdropMode = seq[idx];
                ThemeService.Apply();
                idx++;
            };
            timer.Start();
        }

        // BGI 同款：页面服务 + TargetPageType，导航全部由 NavigationView
        // 内部接管（点击 → 内部 Navigate → 页面服务出实例）。
        RootNavigation.SetPageProviderService(new PageService());

        // 初始导航推迟到 Loaded：构造函数里模板未应用，
        // 内部 presenter 为 null 会 NRE。
        Loaded += (_, _) =>
        {
            WireNavigationParent();
            InitAccountSwitcher();

            // 窗口级主题（材质 + 深色标志）：不重建 chrome，避免异常中断导航
            ThemeService.ApplyWindowChromeOnly();

            // QA：--switch-account-test 账号加载完成后自动切到"另一个"账号，
            // 用于验证各页面是否即时跟随（不需要切 tab）
            if (Environment.GetCommandLineArgs().Contains("--switch-account-test"))
            {
                // 延迟 8 秒：确保目标页面已加载并订阅（模拟用户在看到页面后切换）
                var sw = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(8),
                };
                sw.Tick += (_, _) =>
                {
                    sw.Stop();
                    Once();
                };
                sw.Start();

                void Once()
                {
                    var opts = GlobalAccountContext.Instance.BuildOptions();
                    if (opts.Count < 2)
                    {
                        LogNav($"switch-account-test: only {opts.Count} account(s)");
                        return;
                    }
                    var target = opts.First(o =>
                        GlobalAccountContext.Instance.Current is not { } c
                        || o.Entry.Config != c.Config
                        || o.Entry.RoleKey != c.RoleKey);
                    GlobalAccountContext.Instance.SetCurrent(target.Entry);
                    LogNav($"switch-account-test -> {target.Entry.Config}/{target.Entry.RoleKey}");
                }
            }

            // QA 钩子：--page=<仪表盘|工作台|统计|资产|绘卷|日志|设置|关于>
            // 启动直达指定页（自动化验收用，不影响正常启动）
            var pageArg = Environment.GetCommandLineArgs()
                .FirstOrDefault(a => a.StartsWith("--page=", StringComparison.Ordinal));
            var target = pageArg is null
                ? typeof(DashboardPage)
                : pageArg["--page=".Length..] switch
                {
                    "工作台" or "workbench" => typeof(WorkbenchPage),
                    "统计" or "stats" => typeof(StatisticsPage),
                    "资产" or "resource" => typeof(ResourcePage),
                    "绘卷" or "huijuan" => typeof(HuijuanPage),
                    "日志" or "log" => typeof(LogPage),
                    "设置" or "settings" => typeof(SettingsPage),
                    "关于" or "about" => typeof(AboutPage),
                    _ => typeof(DashboardPage),
                };
            LogNav($"target={target.Name} arg={pageArg}");
            RootNavigation.Navigate(target);
            Dispatcher.BeginInvoke(
                new Action(() => LogNav(
                    $"after navigate: selected={RootNavigation.SelectedItem switch
                    {
                        Wpf.Ui.Controls.NavigationViewItem nvi => nvi.TargetPageType?.Name,
                        _ => "null",
                    }}")),
                System.Windows.Threading.DispatcherPriority.Background);
        };

        RestoreWindowBounds();
        StateChanged += (_, _) => HandleMinimizeToTray();
        Closing += MainWindow_Closing;
    }

    // ==================== 窗口位置记忆 ====================

    private bool RememberBounds =>
        OasSettings.Get("rememberWindowBounds", "false") == "true";

    private void RestoreWindowBounds()
    {
        if (!RememberBounds) return;
        var raw = OasSettings.Get("windowBounds", "");
        if (string.IsNullOrEmpty(raw)) return;
        var parts = raw.Split(',');
        if (parts.Length != 4) return;
        try
        {
            var left = double.Parse(parts[0], CultureInfo.InvariantCulture);
            var top = double.Parse(parts[1], CultureInfo.InvariantCulture);
            Width = double.Parse(parts[2], CultureInfo.InvariantCulture);
            Height = double.Parse(parts[3], CultureInfo.InvariantCulture);
            // 多显示器拔掉后旧坐标可能落在屏幕外——超界则交还系统居中
            var vsLeft = SystemParameters.VirtualScreenLeft;
            var vsTop = SystemParameters.VirtualScreenTop;
            var vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            var vsBottom = vsTop + SystemParameters.VirtualScreenHeight;
            if (left >= vsLeft - 50 && left < vsRight && top >= vsTop - 50 && top < vsBottom)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }
        }
        catch (FormatException)
        {
            // 坏数据忽略，用默认位置
        }
    }

    private void SaveWindowBounds()
    {
        if (!RememberBounds) return;
        if (WindowState != WindowState.Normal) return;
        OasSettings.Set("windowBounds", string.Format(CultureInfo.InvariantCulture,
            "{0:0},{1:0},{2:0},{3:0}", Left, Top, Width, Height));
    }

    // ==================== 最小化到托盘 ====================

    private bool MinimizeToTray =>
        OasSettings.Get("minimizeToTray", "false") == "true";

    private void HandleMinimizeToTray()
    {
        if (!_forceExit && MinimizeToTray && WindowState == WindowState.Minimized)
        {
            EnsureTrayIcon();
            Hide();
        }
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null) return;
        _trayIcon = new Wpf.Ui.Tray.Controls.NotifyIcon
        {
            TooltipText = "OASX",
            Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/appicon.png")),
            FocusOnLeftClick = true,
            MenuOnRightClick = true,
        };
        _trayIcon.LeftClick += (_, _) => RestoreFromTray();

        var menu = new System.Windows.Controls.ContextMenu();
        var open = new System.Windows.Controls.MenuItem { Header = "打开" };
        open.Click += (_, _) => RestoreFromTray();
        var exit = new System.Windows.Controls.MenuItem { Header = "退出" };
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(open);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exit);
        _trayIcon.ContextMenu = menu;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _forceExit = true;
        _trayIcon?.Dispose();
        Application.Current.Shutdown();
    }

    // ==================== 退出流程 ====================

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_forceExit && MinimizeToTray && WindowState != WindowState.Minimized)
        {
            // 用户点 X：有托盘设置时缩到托盘而不是退出（托盘菜单可真退出）
            e.Cancel = true;
            EnsureTrayIcon();
            WindowState = WindowState.Minimized;   // 触发 StateChanged → Hide
            return;
        }

        SaveWindowBounds();

        if (OasSettings.Get("stopBackendOnExit", "false") == "true")
        {
            // 发后即忘：退出不等后端响应
            _ = Task.Run(async () =>
            {
                try
                {
                    using var http = new System.Net.Http.HttpClient
                    {
                        BaseAddress = new Uri(OasSettings.BackendAddress),
                        Timeout = TimeSpan.FromSeconds(3),
                    };
                    await http.GetAsync("/home/kill_server");
                }
                catch
                {
                    // 后端不在/已停，忽略
                }
            });
        }
    }

    // ==================== 导航 ====================

    /// <summary>
    /// ⚠️ WPF-UI 4.3.0 的致命缺陷 workaround：NavigationViewItem.OnClick
    /// 第一行是 GetNavigationParent(this) 为 null 则静默 return——而库自身
    /// **从不给 MenuItems 设置 NavigationParent 附加属性**（只在导航后的
    /// 页面实例上设置）。没有它，所有条目点击都是死按钮。
    /// 这里用反射取 internal 的 NavigationParentProperty，手动补挂到
    /// 每个条目上。（BGI 能工作是因为它的 DI/行为链间接触发了别的路径。）
    /// </summary>
    // ==================== 全局账号切换（侧栏底部） ====================

    private bool _suppressAccountSwitch;

    /// <summary>账号块：订阅上下文，按钮文案与弹出列表随全局状态同步。</summary>
    private static void LogNav(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "nav.log"),
                $"{DateTime.Now:HH:mm:ss} {msg}" + Environment.NewLine);
        }
        catch
        {
            // 日志失败不影响功能
        }
    }

    private void InitAccountSwitcher()
    {
        var ctx = GlobalAccountContext.Instance;
        ctx.AccountsLoaded += RefreshAccountSwitcher;
        ctx.CurrentChanged += _ => RefreshAccountSwitcher();
        RefreshAccountSwitcher();
    }

    private void RefreshAccountSwitcher()
    {
        var ctx = GlobalAccountContext.Instance;
        var cur = ctx.Current;
        AccountButtonText.Text = cur?.DisplayName
            ?? (ctx.IsLoading ? "账号加载中…" : "未选择账号");
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "account_restore.log"),
                $"{DateTime.Now:HH:mm:ss} switcher label='{AccountButtonText.Text}' " +
                $"current={(cur is null ? "null" : cur.Config + "/" + cur.DisplayName)} " +
                $"accounts={ctx.Accounts.Count}\n");
        }
        catch
        {
            // 日志失败不影响功能
        }
    }

    /// <summary>点击账号块 → 弹出账号列表（窗口内 Popup + 按钮，命中可靠）。</summary>
    private void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        var ctx = GlobalAccountContext.Instance;
        var options = ctx.BuildOptions();
        LogNav($"account button clicked: options={options.Count} " +
               $"loading={ctx.IsLoading} accounts={ctx.Accounts.Count}");
        if (options.Count == 0)
        {
            AccountButtonText.Text = ctx.IsLoading ? "账号加载中…" : "未选择账号";
            return;
        }

        AccountPopupPanel.Children.Clear();
        var accent = Application.Current.TryFindResource("OasAccentBrush")
            as System.Windows.Media.SolidColorBrush
            ?? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB));
        var accentTint = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(
                0x1F, accent.Color.R, accent.Color.G, accent.Color.B));

        foreach (var opt in options)
        {
            var current = ctx.Current is { } cur
                          && cur.Config == opt.Entry.Config
                          && cur.RoleKey == opt.Entry.RoleKey;

            // 行内容：✓ + 名称 / 调度器副标题（副标题始终 secondary 色，不进蓝底）
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(22),
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
            });

            var check = new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = current
                    ? Wpf.Ui.Controls.SymbolRegular.Checkmark24
                    : Wpf.Ui.Controls.SymbolRegular.Spacebar24,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = current ? Visibility.Visible : Visibility.Hidden,
                Foreground = accent,
            };
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);

            var textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
            };
            textPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = opt.Label,
                FontSize = 13,
                FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = FindBrush("OasTextPrimaryBrush"),
            });
            if (opt.Schedulers.Count > 1)
            {
                textPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = $"调度器：{string.Join("、", opt.Schedulers)}",
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = FindBrush("OasTextSecondaryBrush"),
                });
            }
            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            // 行容器：选中=淡 accent 底；未选中=透明（hover 由 Button 提供）
            var row = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Background = current ? accentTint : System.Windows.Media.Brushes.Transparent,
                Child = grid,
            };

            var btn = new Wpf.Ui.Controls.Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 2),
                Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
                Content = row,
                Tag = opt,
            };
            btn.Click += (obj, _) =>
            {
                if (obj is Wpf.Ui.Controls.Button
                    {
                        Tag: GlobalAccountContext.AccountOption o
                    })
                {
                    GlobalAccountContext.Instance.SetCurrent(o.Entry);
                    LogNav($"account switched -> {o.Entry.Config}/{o.Entry.RoleKey}");
                }
                AccountPopup.IsOpen = false;
            };
            AccountPopupPanel.Children.Add(btn);
        }

        AccountPopup.PlacementTarget = AccountButton;
        // 延迟到输入队列之后打开：StaysOpen=False 的弹层会被"同一次左键抬起"
        // 当作外部点击而立刻关闭（表现为点了没反应）。
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                AccountPopup.IsOpen = true;
                LogNav($"account popup opened: items={AccountPopupPanel.Children.Count} " +
                       $"isOpen={AccountPopup.IsOpen}");
            }),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private static System.Windows.Media.Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as System.Windows.Media.Brush
        ?? System.Windows.Media.Brushes.Transparent;

    private void WireNavigationParent()
    {
        var dpField = typeof(NavigationView).GetField(
            "NavigationParentProperty", BindingFlags.NonPublic | BindingFlags.Static);
        if (dpField?.GetValue(null) is not DependencyProperty dp)
        {
            File.AppendAllText("nav.log", "WARN: NavigationParentProperty not found\n");
            return;
        }

        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.GetValue(dp) is not NavigationView)
            {
                item.SetValue(dp, RootNavigation);
            }
        }
        foreach (var item in RootNavigation.FooterMenuItems.OfType<NavigationViewItem>())
        {
            if (item.GetValue(dp) is not NavigationView)
            {
                item.SetValue(dp, RootNavigation);
            }
        }
    }
}
