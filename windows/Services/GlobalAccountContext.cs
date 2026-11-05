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

    /// <summary>后端可能仍在启动：配置列表为空/异常时重试（3 次 × 2s）。</summary>
    private static async Task<List<string>> LoadConfigsWithRetryAsync()
    {
        var configs = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            try
            {
                configs = await OasApi.GetConfigListAsync();
            }
            catch
            {
                configs = [];
            }
            if (configs.Count > 0)
            {
                return configs;
            }
            await Task.Delay(2000);
        }
        return configs;
    }

    /// <summary>枚举全部调度器的角色，扁平化为账号列表。
    /// 单个调度器失败不影响其他（该调度器的账号缺席并记入 Error）。</summary>
    public async Task LoadAccountsAsync()
    {
        IsLoading = true;
        Error = null;
        var configs = await LoadConfigsWithRetryAsync();
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
                        // 规范化到该角色的主条目（数据最新、优先主调度器），
                        // 避免恢复出 oas1 的旧快照而仪表盘显示过期等级/区服
                        var canonical = BuildOptions()
                            .FirstOrDefault(o => IdentityKey(o.Entry) == IdentityKey(found))
                            ?.Entry;
                        Current = canonical ?? found;
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
                LogPaths.File("account_restore.log"),
                $"{DateTime.Now:HH:mm:ss} {msg}\n");
        }
        catch
        {
            // 日志失败不影响功能
        }
    }

    /// <summary>切换当前账号（同账号不重复触发事件）。</summary>
    /// <summary>账号选择项（按角色身份去重；附带该角色可用的调度器）。</summary>
    public sealed class AccountOption
    {
        public required AccountEntry Entry { get; init; }
        public required string Label { get; init; }

        /// <summary>同一角色出现的所有调度器（配置名）。</summary>
        public required IReadOnlyList<string> Schedulers { get; init; }

        public string SchedulerText => Schedulers.Count > 1
            ? $"{Schedulers[0]} 等 {Schedulers.Count} 个"
            : Schedulers.FirstOrDefault() ?? "";
    }

    /// <summary>角色身份键：优先 short_id + 区服，退回 名称 + 区服。
    /// 同一个号在不同配置下的 role_key 格式可能不同（{{id@server}} vs 其他），
    /// 不能用 role_key 去重。</summary>
    private static string IdentityKey(AccountEntry a) =>
        !string.IsNullOrEmpty(a.ShortId)
            ? $"{a.ShortId}@{a.ServerName}"
            : $"{a.Name}@{a.ServerName}";

    /// <summary>构造选项：**按角色身份去重**——同一个号跨多个调度器只出现一次，
    /// 取数据最新的一份作为当前条目（执行用该调度器）。</summary>
    public IReadOnlyList<AccountOption> BuildOptions()
    {
        var options = _accounts
            .GroupBy(IdentityKey)
            .Select(g =>
            {
                // 数据最新的那份（Dates 已按倒序，取首日最大者）
                var canonical = g
                    .OrderByDescending(a => a.Dates.FirstOrDefault() ?? "")
                    .First();
                var schedulers = g.Select(a => a.Config)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();
                return new AccountOption
                {
                    Entry = canonical,
                    Label = canonical.DisplayName,
                    Schedulers = schedulers,
                };
            })
            .OrderBy(o => o.Label, StringComparer.Ordinal)
            .ToList();
        DumpOptions(options);
        return options;
    }

    /// <summary>诊断：把去重结果打进 account_restore.log（排障用）。</summary>
    private void DumpOptions(IReadOnlyList<AccountOption> options)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(DateTime.Now.ToString("HH:mm:ss"))
              .Append(" options raw=").Append(_accounts.Count)
              .Append(" deduped=").Append(options.Count)
              .AppendLine();
            foreach (var o in options)
            {
                sb.Append("    ").Append(o.Label)
                  .Append(" | schedulers ").Append(string.Join(",", o.Schedulers))
                  .Append(" | use ").Append(o.Entry.Config).Append("/")
                  .AppendLine(o.Entry.RoleKey);
            }
            File.AppendAllText(
                LogPaths.File("account_restore.log"),
                sb.ToString());
        }
        catch
        {
            // 日志失败不影响功能
        }
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
                    LogPaths.File("account_persist.log"),
                    $"{DateTime.Now:HH:mm:ss} save {entry.Config}|{entry.RoleKey}\n");
            }
            catch { }
        }
        CurrentChanged?.Invoke(entry);
    }

    /// <summary>按 (config, roleKey) 定位账号条目。</summary>
    public AccountEntry? Find(string config, string roleKey)
    {
        var exact = _accounts.FirstOrDefault(
            a => a.Config == config && a.RoleKey == roleKey);
        if (exact is not null)
        {
            return exact;
        }
        // 回退：同一角色在不同配置下 role_key 格式可能不同——
        // 按身份键找到该账号，取数据最新的一份
        return _accounts
            .Where(a => IdentityKey(a) == roleKey || a.RoleKey == roleKey)
            .OrderByDescending(a => a.Dates.FirstOrDefault() ?? "")
            .FirstOrDefault();
    }
}
