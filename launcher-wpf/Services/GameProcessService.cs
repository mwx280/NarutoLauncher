using System.Diagnostics;
using System.IO;
using NarutoLauncher.Models;

namespace NarutoLauncher.Services;

/// <summary>
/// 一个账号对应的游戏会话（进程 + 隔离目录）。
/// </summary>
public class GameSession
{
    public required Process Process { get; init; }
    public required string UserdataDir { get; init; }

    public string WindowHandleFile => Path.Combine(UserdataDir, "window_hwnd.txt");

    /// <summary>读取 GameHost 写入的窗口句柄（返回 0 表示尚未就绪）。</summary>
    public nint ReadWindowHandle()
    {
        try
        {
            if (!File.Exists(WindowHandleFile))
                return 0;
            var text = File.ReadAllText(WindowHandleFile).Trim();
            return long.TryParse(text, out var v) ? new nint(v) : 0;
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// 游戏进程管理：拉起/管理 GameHost 实例（每账号一个进程，多开，可内嵌）。
/// </summary>
public class GameProcessService
{
    /// <summary>GameHost 可执行文件名（相对启动器目录）。</summary>
    private const string GameHostExe = "GameHost/huoyin_launcher.exe";

    /// <summary>启动器目录下的游戏 URL 约定。</summary>
    private const string DefaultGameUrl = "https://game.huoying.qq.com/main.html";

    private readonly Dictionary<long, GameSession> _sessions = new();

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
        _sessions.TryGetValue(accountId, out var s) && !s.Process.HasExited;

    /// <summary>获取账号对应的会话（无则 null）。</summary>
    public GameSession? GetSession(long accountId) =>
        _sessions.TryGetValue(accountId, out var s) ? s : null;

    /// <summary>
    /// 启动一个账号的游戏窗口（内嵌模式）。
    /// </summary>
    public GameSession? StartGame(Account account)
    {
        var exe = GameHostPath;
        if (exe == null)
            return null;

        // 每账号独立缓存目录（多开 cookie 隔离）；扫码登录账号复用扫码时的 userdata
        var userdata = !string.IsNullOrEmpty(account.ScanUserDataDir)
            ? account.ScanUserDataDir
            : Path.Combine(
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
        psi.ArgumentList.Add("--embed");
        // 把启动器主窗口句柄传给 GameHost（作为嵌入父窗口）
        if (App.CurrentApp.MainWindowHandle != 0)
            psi.ArgumentList.Add($"--parent={App.CurrentApp.MainWindowHandle}");

        try
        {
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var session = new GameSession { Process = proc, UserdataDir = userdata };
            _sessions[account.Id] = session;
            account.Running = true;
            return session;
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
        if (_sessions.TryGetValue(account.Id, out var session) && !session.Process.HasExited)
        {
            session.Process.Kill();
            session.Process.Dispose();
            _sessions.Remove(account.Id);
        }
        account.Running = false;
    }

    /// <summary>停止全部游戏进程。</summary>
    public void StopAll()
    {
        foreach (var (id, session) in _sessions.ToList())
        {
            if (!session.Process.HasExited)
            {
                session.Process.Kill();
                session.Process.Dispose();
            }
            var acc = App.CurrentApp.Accounts.Accounts.FirstOrDefault(a => a.Id == id);
            if (acc != null)
                acc.Running = false;
        }
        _sessions.Clear();
    }

    /// <summary>
    /// 启动扫码登录会话（GameHost 加载官网首页，弹出 QQ 登录二维码）。
    /// 登录成功后 userdata/login_result.txt 会写入 QQ 号。
    /// </summary>
    public GameSession? StartScanLogin(nint parentHwnd)
    {
        var exe = GameHostPath;
        if (exe == null)
            return null;

        // 独立扫码 userdata（登录 cookie 持久化，添加账号时绑定）
        var userdata = Path.Combine(
            Path.GetDirectoryName(exe)!,
            "userdata",
            "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(userdata);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("--login");
        psi.ArgumentList.Add($"--userdata={userdata}");
        psi.ArgumentList.Add("--title=QQ 扫码登录");
        psi.ArgumentList.Add("--embed");
        if (parentHwnd != 0)
            psi.ArgumentList.Add($"--parent={parentHwnd}");

        try
        {
            var proc = Process.Start(psi);
            if (proc == null) return null;
            return new GameSession { Process = proc, UserdataDir = userdata };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动扫码登录失败: {ex.Message}");
            return null;
        }
    }
}
