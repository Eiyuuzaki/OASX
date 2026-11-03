using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

public sealed record AssetVm(string Name, string CountText, string DeltaText, Brush DeltaBrush);

/// <summary>
/// Dashboard（规范 4.2）：当前账号概况为主视觉，资产快照为主内容，
/// 调度器状态为次级系统信息。账号来源 = 全局账号上下文，本页不自持选择状态。
/// </summary>
public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = RefreshFromContextAsync();
    }

    /// <summary>按全局当前账号刷新概况（无账号 → 空态）。</summary>
    private async Task RefreshFromContextAsync()
    {
        var ctx = GlobalAccountContext.Instance;
        if (ctx.Accounts.Count == 0 && !ctx.IsLoading)
        {
            await ctx.LoadAccountsAsync();
        }

        var entry = ctx.Current;
        if (entry is null)
        {
            AccountNameText.Text = "未选择账号";
            AccountDetailText.Text = ctx.Accounts.Count > 0
                ? "在资产页选择账号后展示概况"
                : (ctx.IsLoading ? "账号加载中…" : "未发现任何账号（后端无角色数据）");
            EmptyText.Visibility = Visibility.Visible;
            AssetList.ItemsSource = null;
            SchedulerText.Text = "—";
            return;
        }

        // 1. 账号（主）
        AccountNameText.Text = entry.Name;
        AccountDetailText.Text = $"Lv{entry.Level} · {entry.ServerName} · 调度器 {entry.Config}";

        // 2. 资产快照（该账号 = (config, roleKey) 的最新一天）
        await LoadAssetSnapshotAsync(entry);

        // 3. 调度器状态（次级）
        await RefreshSchedulerAsync(entry.Config);
    }

    private async Task LoadAssetSnapshotAsync(AccountEntry entry)
    {
        try
        {
            if (entry.Dates.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                AssetList.ItemsSource = null;
                UpdatedAtText.Text = "";
                return;
            }
            var date = entry.Dates[0];   // 最新一天
            var day = await OasResourceApi.GetResourceDayAsync(entry.Config, date, entry.RoleKey);
            if (day is null || day.Items.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                AssetList.ItemsSource = null;
                UpdatedAtText.Text = "";
                return;
            }
            EmptyText.Visibility = Visibility.Collapsed;
            UpdatedAtText.Text = $"更新于 {day.UpdatedAt}";

            var whitelist = CurrencyIconRegistry.Get();
            var visible = day.Items
                .Where(i => whitelist.Contains(CurrencyIconRegistry.BaseName(i.Icon)) && i.Count > 0)
                .GroupBy(i => CurrencyIconRegistry.BaseName(i.Icon))
                .Select(g =>
                {
                    var agg = g.OrderByDescending(x => x.Id).First();
                    var delta = g.Sum(x => x.Delta ?? 0);
                    return new AssetVm(
                        agg.Name,
                        FormatCount(agg.Count),
                        delta == 0 ? "" : FormatDelta(delta),
                        new SolidColorBrush(delta >= 0
                            ? Color.FromRgb(0x4C, 0xAF, 0x50)
                            : Color.FromRgb(0xE5, 0x53, 0x53)));
                })
                .Take(7)
                .ToList();
            AssetList.ItemsSource = visible;
        }
        catch (Exception ex)
        {
            EmptyText.Visibility = Visibility.Visible;
            AssetList.ItemsSource = null;
            UpdatedAtText.Text = $"加载失败：{ex.Message}";
        }
    }

    /// <summary>调度器状态（次级信息，不阻塞账号数据）。</summary>
    private async Task RefreshSchedulerAsync(string config)
    {
        SchedulerText.Text = "查询中…";
        var state = await OasApi.GetScriptStateAsync(config);
        SchedulerText.Text = $"{config} · {(state is null ? "状态未知" : state.State)}";
    }

    private static string FormatCount(long n) => n switch
    {
        >= 100_000_000 => $"{n / 100_000_000.0:0.0}亿",
        >= 10_000 => $"{n / 10_000.0:0.0}万",
        _ => n.ToString("N0"),
    };

    private static string FormatDelta(long d)
    {
        var sign = d > 0 ? "+" : "";
        var v = Math.Abs(d) switch
        {
            >= 100_000_000 => $"{Math.Abs(d) / 100_000_000.0:0.0}亿",
            >= 10_000 => $"{Math.Abs(d) / 10_000.0:0.0}万",
            _ => Math.Abs(d).ToString("N0"),
        };
        return $"{sign}{v}";
    }
}
