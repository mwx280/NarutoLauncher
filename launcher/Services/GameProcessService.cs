using System.Diagnostics;
using System.IO;
using NarutoLauncher.Models;

namespace NarutoLauncher.Services;

/// <summary>
/// 游戏进程管理：拉起/管理 GameHost 实例（每账号一个进程，多开）。
/// </summary>
public class GameProcessService
{
    /// <summary>GameHost 可执行文件名（相对启动器目录）。</summary>
    private const string GameHostExe = "GameHost/huoyin_launcher.exe";

    /// <summary>启动器目录下的游戏 URL 约定。</summary>
    private const string DefaultGameUrl = "https://game.huoying.qq.com/main.html";

    private readonly Dictionary<long, Process> _processes = new();

    /// <summary>GameHost exe 绝对路径（不存在则返回 null）。</summary>
    public string? GameHostPath
    {
        get
        {
            var exeDir = AppContext.BaseDirectory;
            var path = Path.Combine(exeDir, GameHostExe);
            return File.Exists(path) ? path : null;
        }
    }

    /// <summary>是否有运行中的游戏进程。</summary>
    public bool IsRunning(long accountId) =>
        _processes.TryGetValue(accountId, out var p) && !p.HasExited;

    /// <summary>
    /// 启动一个账号的游戏窗口。
    /// </summary>
    public Process? StartGame(Account account)
    {
        var exe = GameHostPath;
        if (exe == null)
            return null;

        // 每账号独立缓存目录（多开 cookie 隔离）
        var userdata = Path.Combine(
            Path.GetDirectoryName(exe)!,
            "userdata",
            account.QQ);
        Directory.CreateDirectory(userdata);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add($"--url={DefaultGameUrl}");
        psi.ArgumentList.Add($"--userdata={userdata}");
        psi.ArgumentList.Add($"--title=火影忍者OL - {account.DisplayName}");

        try
        {
            var proc = Process.Start(psi);
            if (proc == null) return null;
            _processes[account.Id] = proc;
            account.Running = true;
            return proc;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动游戏失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>停止一个账号的游戏进程。</summary>
    public void StopGame(Account account)
    {
        if (_processes.TryGetValue(account.Id, out var proc) && !proc.HasExited)
        {
            proc.Kill();
            proc.Dispose();
            _processes.Remove(account.Id);
        }
        account.Running = false;
    }

    /// <summary>停止全部游戏进程。</summary>
    public void StopAll()
    {
        foreach (var (id, proc) in _processes.ToList())
        {
            if (!proc.HasExited)
            {
                proc.Kill();
                proc.Dispose();
            }
            var acc = App.CurrentApp.Accounts.Accounts.FirstOrDefault(a => a.Id == id);
            if (acc != null)
                acc.Running = false;
        }
        _processes.Clear();
    }
}
