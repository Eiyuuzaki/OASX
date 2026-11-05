using System.IO;

namespace OASX.Services;

/// <summary>
/// 日志目录：统一写到 %LOCALAPPDATA%\OASX\logs。
/// 早期各模块直接写 AppContext.BaseDirectory（exe 旁），发布包解压后
/// 目录里会散落 nav.log / theme.log / share.log / settings_path.log，
/// 既脏又随每次更新丢失。
/// </summary>
public static class LogPaths
{
    private static string? _dir;

    /// <summary>日志目录（必要时创建；不可写则回退 exe 目录）。</summary>
    public static string Dir => _dir ??= ResolveDir();

    /// <summary>日志文件完整路径。</summary>
    public static string File(string name) => Path.Combine(Dir, name);

    /// <summary>追加一行日志（失败静默——日志本身不能打断功能）。</summary>
    public static void Append(string name, string line)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Dir);
            System.IO.File.AppendAllText(File(name), line + Environment.NewLine);
        }
        catch
        {
            // 日志失败不影响功能
        }
    }

    private static string ResolveDir()
    {
        foreach (var baseDir in new[]
                 {
                     Environment.GetEnvironmentVariable("LOCALAPPDATA"),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 })
        {
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                continue;
            }
            try
            {
                var dir = Path.Combine(baseDir, "OASX", "logs");
                System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
            catch
            {
                // 换下一个候选
            }
        }
        // 兜底：exe 目录（便携/受限环境）
        return AppContext.BaseDirectory;
    }
}