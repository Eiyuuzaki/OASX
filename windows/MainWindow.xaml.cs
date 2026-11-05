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
        SourceInitialized += (_, _) =>
        {
            ThemeService.ApplyWindowChromeOnly();
            WireGlobalHotkey();
        };
        PreviewKeyDown += Window_PreviewKeyDown;
        Closed += (_, _) => UnwireGlobalHotkey();
        // 首次渲染完成后再应用一次：句柄就绪时设的材质会被后续 chrome
        // 重建覆盖（实测同一份 settings 有时 acrylic 有时回落为无）。
        ContentRendered += (_, _) =>
        {
            ThemeService.ApplyWindowChromeOnly();
            // 再补一次**完整**主题应用：仅设 DWM 属性不会产生材质合成，
            // 表现为"设置里是 acrylic 但刚打开没效果，手动切一下才有"。
            // 延迟到渲染完成之后，避开导航期的 chrome 重建异常。
            var once = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400),
            };
            once.Tick += (_, _) =>
            {
                once.Stop();
                ThemeService.ApplyFull();
            };
            once.Start();
        };

        // 激活时重申深色标志：部分场景（DWM 重排、切换材质、系统事件）
        // 会把标志冲掉，导致材质按浅色上色（深浅乱串）。
        Activated += (_, _) => ThemeService.SetImmersiveDarkMode(
            this, ThemeService.IsDark);

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
                    "活动" or "activity" => typeof(ActivityPage),
                    "绘卷" or "huijuan" => typeof(HuijuanPage),
                    "日志" or "log" => typeof(LogPage),
                    "设置" or "settings" => typeof(SettingsPage),
                    "关于" or "about" => typeof(AboutPage),
                    _ => typeof(DashboardPage),
                };
            RootNavigation.Navigate(target);
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

    // ==================== 快捷键 ====================

    private const int HotkeyId = 0xC0BE;
    private string _hotkeyLabel = "";
    private System.Windows.Interop.HwndSource? _hwndSource;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>注册全局热键 Ctrl+Alt+O（显示/隐藏窗口）。</summary>
    private void WireGlobalHotkey()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }
            _hwndSource = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(HotkeyWndProc);
            // 候选热键按顺序尝试（前者被占用就换下一个）：
            //   Ctrl+Alt+O → Ctrl+Alt+Shift+O → Ctrl+Shift+F10
            // MOD_ALT=0x1 MOD_CONTROL=0x2 MOD_SHIFT=0x4
            var candidates = new (uint Mods, uint Vk, string Label)[]
            {
                (0x3, 0x4F, "Ctrl+Alt+O"),
                (0x7, 0x4F, "Ctrl+Alt+Shift+O"),
                (0x6, 0x79, "Ctrl+Shift+F10"),
            };
            foreach (var (mods, vk, label) in candidates)
            {
                if (RegisterHotKey(hwnd, HotkeyId, mods, vk))
                {
                    _hotkeyLabel = label;
                    Services.LogPaths.Append("hotkey.log",
                        $"{DateTime.Now:HH:mm:ss} registered {label}");
                    return;
                }
                Services.LogPaths.Append("hotkey.log",
                    $"{DateTime.Now:HH:mm:ss} {label} 占用（err=" +
                    $"{System.Runtime.InteropServices.Marshal.GetLastWin32Error()}）");
            }
            Services.LogPaths.Append("hotkey.log",
                $"{DateTime.Now:HH:mm:ss} 全部候选热键都被占用");
        }
        catch
        {
            // 热键注册失败不影响主流程
        }
    }

    private void UnwireGlobalHotkey()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(hwnd, HotkeyId);
            }
            _hwndSource?.RemoveHook(HotkeyWndProc);
        }
        catch
        {
            // 忽略
        }
    }

    private IntPtr HotkeyWndProc(IntPtr hwnd, int msg, IntPtr wParam,
        IntPtr lParam, ref bool handled)
    {
        const int WmHotkey = 0x0312;
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Services.LogPaths.Append("hotkey.log",
                $"{DateTime.Now:HH:mm:ss} WM_HOTKEY fired visible={IsVisible}");
            ToggleWindowVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>全局热键：可见则最小化到托盘，托盘态则还原。</summary>
    private void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized && IsActive)
        {
            Hide();
        }
        else if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
        }
        else
        {
            RestoreFromTray();
        }
    }

    /// <summary>页内快捷键：Ctrl+1..7 切页、Ctrl+, 设置、F5/Ctrl+R 刷新当前页。</summary>
    private void Window_PreviewKeyDown(object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        var ctrl = System.Windows.Input.Keyboard.Modifiers
                   == System.Windows.Input.ModifierKeys.Control;
        if (e.Key == System.Windows.Input.Key.F5 && !ctrl)
        {
            ReloadCurrentPage();
            e.Handled = true;
            return;
        }
        if (!ctrl)
        {
            return;
        }
        var pages = new[]
        {
            typeof(DashboardPage), typeof(WorkbenchPage), typeof(StatisticsPage),
            typeof(ResourcePage), typeof(ActivityPage), typeof(HuijuanPage),
            typeof(LogPage),
        };
        if (e.Key >= System.Windows.Input.Key.D1 && e.Key <= System.Windows.Input.Key.D7)
        {
            RootNavigation.Navigate(pages[e.Key - System.Windows.Input.Key.D1]);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.OemComma)
        {
            RootNavigation.Navigate(typeof(SettingsPage));
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.R)
        {
            ReloadCurrentPage();
            e.Handled = true;
        }
    }

    /// <summary>重新导航当前页（等价于重建页面 → 重读数据）。</summary>
    private void ReloadCurrentPage()
    {
        var current = RootNavigation.SelectedItem switch
        {
            Wpf.Ui.Controls.NavigationViewItem nvi => nvi.TargetPageType,
            _ => null,
        };
        if (current is not null)
        {
            RootNavigation.Navigate(current);
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Services.LogPaths.Dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Services.LogPaths.Dir,
                UseShellExecute = true,
            });
        }
        catch
        {
            // 打不开资源管理器不影响主流程
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
        var refresh = new System.Windows.Controls.MenuItem { Header = "刷新当前页" };
        refresh.Click += (_, _) => ReloadCurrentPage();
        var logs = new System.Windows.Controls.MenuItem { Header = "打开日志目录" };
        logs.Click += (_, _) => OpenLogFolder();
        var settings = new System.Windows.Controls.MenuItem { Header = "设置" };
        settings.Click += (_, _) =>
        {
            RestoreFromTray();
            RootNavigation.Navigate(typeof(SettingsPage));
        };
        var exit = new System.Windows.Controls.MenuItem { Header = "退出" };
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(open);
        if (_hotkeyLabel.Length > 0)
        {
            // 提示当前生效的全局热键（被占用时会自动降级，用户需要知道是哪一组）
            menu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = $"{_hotkeyLabel} 显示/隐藏",
                IsEnabled = false,
            });
        }
        menu.Items.Add(refresh);
        menu.Items.Add(settings);
        menu.Items.Add(logs);
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
    private void InitAccountSwitcher()
    {
        var ctx = GlobalAccountContext.Instance;
        ctx.AccountsLoaded += RefreshAccountSwitcher;
        ctx.CurrentChanged += _ => RefreshAccountSwitcher();
        // 账号就绪后立刻预热"在线角色"，这样第一次打开菜单就能显示标注
        ctx.AccountsLoaded += () => _ = RefreshLiveRoleAsync();
        if (ctx.Accounts.Count > 0)
        {
            _ = RefreshLiveRoleAsync();
        }
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
                LogPaths.File("account_restore.log"),
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
            // 标注：哪个账号是此刻在线的 / 哪条数据已过期
            var live = OasRoleApi.Cached;
            var isOnline = live is { Success: true } &&
                           opt.Entry.RoleKey.Split('@').FirstOrDefault() == live.AvatarId;
            var dataDate = opt.Entry.Dates.FirstOrDefault() ?? "";
            var stale = dataDate.Length > 0 &&
                        dataDate != DateTime.Today.ToString("yyyy-MM-dd");
            var suffix = (isOnline ? "　在线" : "")
                         + (stale ? $"　数据 {dataDate}" : "");

            textPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = opt.Label + suffix,
                FontSize = 13,
                FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = isOnline
                    ? FindBrush("OasSuccessBrush")
                    : FindBrush("OasTextPrimaryBrush"),
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
                }
                AccountPopup.IsOpen = false;
            };
            AccountPopupPanel.Children.Add(btn);
        }

        // 末尾：刷新账号列表（删掉数据后不必重启应用）
        AccountPopupPanel.Children.Add(new System.Windows.Controls.Border
        {
            Height = 1,
            Margin = new Thickness(4, 4, 4, 6),
            Background = Application.Current.TryFindResource("OasBorderBrush")
                         as System.Windows.Media.Brush
                         ?? System.Windows.Media.Brushes.Gainsboro,
        });
        var refreshBtn = new Wpf.Ui.Controls.Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 8, 10, 8),
            Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "刷新账号列表",
                FontSize = 12,
                Foreground = FindBrush("OasTextSecondaryBrush"),
            },
        };
        refreshBtn.Click += async (_, _) =>
        {
            AccountPopup.IsOpen = false;
            await GlobalAccountContext.Instance.LoadAccountsAsync();
        };
        AccountPopupPanel.Children.Add(refreshBtn);

        // 后台刷新在线角色：本次先用缓存，下次打开菜单即可显示「在线」标注
        _ = RefreshLiveRoleAsync();

        AccountPopup.PlacementTarget = AccountButton;
        // 延迟到输入队列之后打开：StaysOpen=False 的弹层会被"同一次左键抬起"
        // 当作外部点击而立刻关闭（表现为点了没反应）。
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                AccountPopup.IsOpen = true;
            }),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>刷新"当前在线角色"缓存（失败静默；仅用于菜单标注）。</summary>
    private async Task RefreshLiveRoleAsync()
    {
        var ctx = GlobalAccountContext.Instance;
        var config = ctx.Current?.Config;
        if (string.IsNullOrEmpty(config))
        {
            return;
        }
        var role = await OasRoleApi.GetLiveAsync(config);
        if (role is { Success: true })
        {
        }
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
