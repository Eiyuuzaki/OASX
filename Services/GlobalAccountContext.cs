using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OASX.Services;

/// <summary>账号条目：资源数据的归属地址 = (调度器, role_key) 对。
/// ⚠️ 数据模型实测（probe/account_model_audit.py）：同一 role_key 可出现在
/// 多个调度器下，一个调度器可有多个角色——多对多。因此账号的全局标识
/// 是 (Config, RoleKey) 二元组，role_key 单独不构成数据地址。</summary>
public sealed record AccountEntry(
    string Config, string RoleKey, string Name, int Level,
    string ServerName, string ShortId, List<string> Dates)
{
    /// <summary>展示名：角色 Lv 等级 · 区服（调度器作次要信息由 UI 决定是否带出）。</summary>
    public string DisplayName => $"{Name} Lv{Level} · {ServerName}";
}

/// <summary>
/// 全局账号上下文（规范 3.1）：统一枚举所有调度器下的账号、持有当前选中、
/// 切换事件、加载/错误状态。各账号数据页面订阅本上下文，不再各自维护选择状态。
/// </summary>
public sealed class GlobalAccountContext
{
    public static GlobalAccountContext Instance { get; } = new();

    /// <summary>当前账号持久化键：'config|roleKey'。</summary>
    private const string CurrentAccountKey = "currentAccount";

    /// <summary>全部账号（跨调度器扁平化）。</summary>
    public IReadOnlyList<AccountEntry> Accounts => _accounts;
    private List<AccountEntry> _accounts = [];

    /// <summary>当前账号。</summary>
    public AccountEntry? Current { get; private set; }

    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    /// <summary>账号列表加载完成（成功或失败后触发一次）。</summary>
    public event Action? AccountsLoaded;

    /// <summary>当前账号变化（含切到 null）。</summary>
    public event Action<AccountEntry?>? CurrentChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>枚举全部调度器的角色，扁平化为账号列表。
    /// 单个调度器失败不影响其他（该调度器的账号缺席并记入 Error）。</summary>
    public async Task LoadAccountsAsync()
    {
        IsLoading = true;
        Error = null;
        var configs = await OasApi.GetConfigListAsync();
        var all = new List<AccountEntry>();
        var errors = new List<string>();

        // 并发拉取各配置的角色：串行时 4 个配置要 20~30 秒，
        // 期间界面只能显示"账号加载中"，体验很差。
        var results = await Task.WhenAll(configs.Select(async config =>
        {
            try
            {
                var roles = await OasResourceApi.GetRolesAsync(config);
                return (Config: config, Roles: roles, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Config: config,
                        Roles: (IReadOnlyList<OasResourceApi.RoleInfo>)[],
                        Error: (string?)$"{config}: {ex.Message}");
            }
        }));
        foreach (var r in results)
        {
            if (r.Error is not null)
            {
                errors.Add(r.Error);
                continue;
            }
            all.AddRange(r.Roles.Select(x => new AccountEntry(
                r.Config, x.RoleKey, x.Name, x.Level,
                x.ServerName, x.ShortId, x.Dates ?? [])));
        }
        _accounts = all;
        IsLoading = false;
        Error = errors.Count > 0 ? string.Join("; ", errors) : null;

        // 恢复上次选中的账号（跨重启保留）
        if (Current is null)
        {
            var saved = OasSettings.Get(CurrentAccountKey, "");
            Diag($"restore start saved='{saved}' accounts={_accounts.Count}");
            if (saved.Length > 0)
            {
                var parts = saved.Split('|');
                if (parts.Length == 2)
                {
                    var found = Find(parts[0], parts[1]);
                    Diag($"restore find config='{parts[0]}' role='{parts[1]}' -> " +
                         (found is null ? "NOT FOUND" : found.DisplayName));
                    if (found is not null)
                    {
                        Current = found;
                    }
                }
                else
                {
                    Diag($"restore format invalid parts={parts.Length}");
                }
            }
        }
        AccountsLoaded?.Invoke();
    }

    /// <summary>账号上下文诊断（exe 目录 account_restore.log）。</summary>
    private static void Diag(string msg)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "account_restore.log"),
                $"{DateTime.Now:HH:mm:ss} {msg}\n");
        }
        catch
        {
            // 日志失败不影响功能
        }
    }

    /// <summary>切换当前账号（同账号不重复触发事件）。</summary>
    /// <summary>账号选择项（同名账号带调度器消歧）。
    /// 标题栏与资产页共用同一份构造逻辑，避免两处标签不一致。</summary>
    public sealed class AccountOption
    {
        public required AccountEntry Entry { get; init; }
        public required string Label { get; init; }
    }

    /// <summary>构造下拉选项：同名账号追加 (调度器) 消歧。</summary>
    public IReadOnlyList<AccountOption> BuildOptions()
    {
        var dupNames = _accounts
            .GroupBy(a => a.DisplayName)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();
        return _accounts.Select(a => new AccountOption
        {
            Entry = a,
            Label = dupNames.Contains(a.DisplayName)
                ? $"{a.DisplayName}（{a.Config}）"
                : a.DisplayName,
        }).ToList();
    }

    public void SetCurrent(AccountEntry? entry)
    {
        if (ReferenceEquals(Current, entry)) return;
        Current = entry;
        OasSettings.Set(CurrentAccountKey,
            entry is null ? "" : $"{entry.Config}|{entry.RoleKey}");
        Diag($"SetCurrent -> {(entry is null ? "null" : entry.Config + "|" + entry.RoleKey)}");
        if (entry is not null)
        {
            try
            {
                File.AppendAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "account_persist.log"),
                    $"{DateTime.Now:HH:mm:ss} save {entry.Config}|{entry.RoleKey}\n");
            }
            catch { }
        }
        CurrentChanged?.Invoke(entry);
    }

    /// <summary>按 (config, roleKey) 定位账号条目。</summary>
    public AccountEntry? Find(string config, string roleKey) =>
        _accounts.FirstOrDefault(a => a.Config == config && a.RoleKey == roleKey);
}
