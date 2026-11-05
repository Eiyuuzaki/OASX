using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace OASX.Services;

/// <summary>
/// 后端自动拉起（设置项）：
/// 启动 OASX 时若后端不可达，则按配置的 OAS 目录执行 toolkit/python.exe server.py，
/// 并等待其就绪（最多 15 秒）。目录与 python 路径可由设置页配置。
/// </summary>
public static class OasBackendLauncher
{
    private const string DefaultWorkDir = @"D:\OAS-Kirin\OAS";

    public static bool Enabled =>
        OasSettings.Get("autoStartBackend", "true") == "true";

    /// <summary>OAS 工作目录（含 server.py 与 toolkit/python.exe）。</summary>
    public static string WorkDir =>
        OasSettings.Get("oasWorkDir", DefaultWorkDir);

    /// <summary>确保后端在运行：不可达则启动并等待就绪。返回是否可用。</summary>
    public static async Task<bool> EnsureRunningAsync()
    {
        if (!Enabled) return false;
        if (await PingAsync()) return true;

        try
        {
            var python = Path.Combine(WorkDir, "toolkit", "python.exe");
            var server = Path.Combine(WorkDir, "server.py");
            if (!File.Exists(python) || !File.Exists(server))
            {
                return false;   // 目录配置错误
            }
            var psi = new ProcessStartInfo(python, $"\"{server}\"")
            {
                WorkingDirectory = WorkDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(psi);

            // 等待后端就绪（uvicorn 启动约 5-9 秒）
            for (var i = 0; i < 15; i++)
            {
                await Task.Delay(1000);
                if (await PingAsync()) return true;
            }
        }
        catch
        {
            // 启动失败不打断应用
        }
        return false;
    }

    private static async Task<bool> PingAsync()
    {
        try
        {
            using var http = new HttpClient
            {
                BaseAddress = new Uri(OasSettings.BackendAddress),
                Timeout = TimeSpan.FromSeconds(2),
            };
            using var res = await http.GetAsync("/config_list");
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
