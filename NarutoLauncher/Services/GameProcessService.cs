using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NarutoLauncher.Models;

namespace NarutoLauncher.Services;

/// <summary>
/// 一个账号对应的游戏会话（进程 + 隔离目录）。
/// 同一账号可开多个会话（多区多开），会话以唯一 Key 区分、可单独停止。
/// </summary>
public class GameSession
{
    /// <summary>会话唯一标识（同账号多开时区分）。</summary>
    public required string Key { get; init; }

    /// <summary>所属账号 Id（用于按账号查询/停止）。</summary>
    public required long AccountId { get; init; }

    /// <summary>进程。</summary>
    public required Process Process { get; init; }

    /// <summary>隔离目录。</summary>
    public required string UserdataDir { get; init; }

    public string WindowHandleFile => Path.Combine(UserdataDir, "window_hwnd.txt");

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    /// <summary>读取 GameHost 写入的窗口句柄（返回 0 表示尚未就绪）。</summary>
    public nint ReadWindowHandle()
    {
        try
        {
            if (!File.Exists(WindowHandleFile))
                return 0;
            var text = File.ReadAllText(WindowHandleFile).Trim();
            if (!long.TryParse(text, out var v) || v == 0)
                return 0;
            var hwnd = new nint(v);
            // 校验句柄确实对应一个现存窗口（避免读到上次运行遗留的过期句柄）
            return IsWindow(hwnd) ? hwnd : 0;
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
    private const string GameHostExe = "GameHost/cef_flash_game_host.exe";

    /// <summary>启动器目录下的游戏 URL 约定。</summary>
    private const string DefaultGameUrl = "https://game.huoying.qq.com/main.html";

    /// <summary>会话集合（按唯一 Key 索引，同账号可多个会话）。</summary>
    private readonly Dictionary<string, GameSession> _sessions = new();

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

    /// <summary>账号是否有运行中的游戏进程（任一会话）。</summary>
    public bool IsRunning(long accountId) =>
        GetSessions(accountId).Any(s => !s.Process.HasExited);

    /// <summary>获取账号的全部会话（无则空列表）。</summary>
    public List<GameSession> GetSessions(long accountId) =>
        _sessions.Values.Where(s => s.AccountId == accountId).ToList();

    /// <summary>按会话唯一 Key 获取会话（无则 null）。</summary>
    public GameSession? GetSession(string key) =>
        _sessions.TryGetValue(key, out var s) ? s : null;

    /// <summary>构建账号的游戏 userdata 目录：同账号多开时每个会话唯一，避免 CEF 锁库。</summary>
    private string BuildUserdataDir(Account account, string exe)
    {
        var baseDir = Path.GetDirectoryName(exe)!;
        var sourceUserdata = account.ScanUserDataDir;

        // 该账号当前是否已有会话在运行（多开判断）
        var running = GetSessions(account.Id).Where(s => !s.Process.HasExited).ToList();

        // 首次启动：扫码账号复用扫码目录（含登录态），账号密码账号用主目录
        if (running.Count == 0)
        {
            if (!string.IsNullOrEmpty(account.ScanUserDataDir))
                return account.ScanUserDataDir;
            return Path.Combine(baseDir, "userdata", account.QQ);
        }

        // 多开：每个会话用唯一序号目录，并把首会话的登录态（cookie）复制过去，
        // 使新区共享登录；既避免 CEF 锁库白屏，又保留登录 cookie。
        var unique = Path.Combine(baseDir, "userdata",
            $"{account.QQ}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        Directory.CreateDirectory(unique);

        // 复制登录态文件（Cookies、Local State、LocalPrefs.json 等）自登录源目录
        var loginSrc = !string.IsNullOrEmpty(sourceUserdata)
            ? sourceUserdata
            : Path.Combine(baseDir, "userdata", account.QQ);
        CopyLoginState(loginSrc, unique);
        return unique;
    }

    /// <summary>复制 CEF 登录态文件（Cookies / Local State / LocalPrefs.json 等）到目标目录。</summary>
    private static void CopyLoginState(string src, string dest)
    {
        try
        {
            if (string.IsNullOrEmpty(src) || !Directory.Exists(src))
                return;
            foreach (var name in new[] { "Cookies", "Local State", "LocalPrefs.json", "Network" })
            {
                var s = Path.Combine(src, name);
                if (File.Exists(s))
                    File.Copy(s, Path.Combine(dest, name), overwrite: true);
                else if (Directory.Exists(s))
                    CopyDirectory(s, Path.Combine(dest, name));
            }
        }
        catch
        {
            // 复制失败不阻断启动（新区可能无登录态）
        }
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), overwrite: true);
        foreach (var d in Directory.GetDirectories(src))
            CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
    }

    /// <summary>
    /// 启动一个账号的游戏窗口（内嵌模式）。同账号可多次调用（多区多开），
    /// 各会话共用账号 cookie，仅以唯一 Key 区分，可分别停止。
    /// </summary>
    public GameSession? StartGame(Account account, nint parentHwnd = 0,
                                  string? flashQuality = null,
                                  string? urlOverride = null)
    {
        var exe = GameHostPath;
        if (exe == null)
            return null;

        // 清理上次运行残留的孤儿 GameHost 进程（CEF 子进程），避免争用 userdata 目录
        KillOrphanedGameHosts();

        // 每账号独立缓存目录。同账号多开（多区）时，首个会话用账号主目录（扫码账号复用
        // 扫码目录，含登录态 cookie）；后续会话用唯一序号目录，避免多个 CEF 进程争用同一
        // userdata（锁库）导致新标签白屏。登录态通过 --cookie 注入共享。
        var userdata = BuildUserdataDir(account, exe);
        Directory.CreateDirectory(userdata);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add($"--url={urlOverride ?? DefaultGameUrl}");
        psi.ArgumentList.Add($"--userdata={userdata}");
        psi.ArgumentList.Add($"--title=火影忍者Online - {account.DisplayName}");
        psi.ArgumentList.Add("--embed");
        // Flash 硬件加速开关（默认关闭；开启需重新进入游戏才生效）
        psi.ArgumentList.Add(App.CurrentApp.Settings.FlashHardwareAcceleration
            ? "--flash-gpu=1" : "--flash-gpu=0");
        // Flash 渲染质量（低/中/高，经 Flash hook 在实例创建时生效）
        var quality = flashQuality ?? App.CurrentApp.Settings.FlashQuality;
        psi.ArgumentList.Add($"--flash-quality={quality}");
        // 分辨率模式（性能优先=强制DPR1，画质优先=跟随系统DPI）
        psi.ArgumentList.Add(App.CurrentApp.Settings.DprMode == Services.DprMode.Performance
            ? "--force-dpr=1" : "--force-dpr=0");
        // 账号有保存的 cookie 时注入（域分组的 JSON，base64 编码后传给 GameHost）；
        // 多开会话的登录态由 BuildUserdataDir 复制自首会话 userdata 获得。
        if (!string.IsNullOrEmpty(account.Cookies))
        {
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(account.Cookies));
            psi.ArgumentList.Add($"--cookie={b64}");
        }
        // 账号密码账号（无扫码 cookie 目录）：传 QQ 号/密码，GameHost 检测未登录时自动填表登录
        if (!account.ScanLogin && !string.IsNullOrEmpty(account.Password))
        {
            psi.ArgumentList.Add($"--user={Convert.ToBase64String(Encoding.UTF8.GetBytes(account.QQ))}");
            psi.ArgumentList.Add($"--pass={Convert.ToBase64String(Encoding.UTF8.GetBytes(account.Password))}");
        }
        // 把内嵌父窗口句柄传给 GameHost（缺省用主窗口）
        var parent = parentHwnd != 0 ? parentHwnd : App.CurrentApp.MainWindowHandle;
        if (parent != 0)
            psi.ArgumentList.Add($"--parent={parent}");
        // 调试分析：设置环境变量 HUOYIN_DEBUG_PORT 时开启 CEF DevTools 远程调试（不设则关闭）
        var debugPort = Environment.GetEnvironmentVariable("HUOYIN_DEBUG_PORT");
        if (!string.IsNullOrEmpty(debugPort))
            psi.ArgumentList.Add($"--debug-port={debugPort}");

        try
        {
            var proc = Process.Start(psi);
            if (proc == null) return null;
            // 提高游戏进程优先级，加快启动与画面加载
            try { proc.PriorityClass = ProcessPriorityClass.AboveNormal; } catch { }
            var key = $"{account.Id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var session = new GameSession
            {
                Key = key,
                AccountId = account.Id,
                Process = proc,
                UserdataDir = userdata,
            };
            _sessions[key] = session;
            account.Running = true;
            // 登录后同步区服/角色名（供账号 UI 与封禁上报，无需进游戏）
            _ = RefreshAccountInfoAsync(account);
            return session;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动游戏失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>登录后异步同步账号区服/角色名（失败静默）。</summary>
    private static async Task RefreshAccountInfoAsync(Account account)
    {
        try
        {
            await new AccountInfoService().RefreshAsync(account);
        }
        catch
        {
        }
    }

    /// <summary>停止一个账号的所有游戏进程（异步，不阻塞 UI）。</summary>
    public void StopGame(Account account)
    {
        foreach (var s in GetSessions(account.Id))
            _ = StopSession(s);
        foreach (var key in _sessions.Keys.Where(k => _sessions[k].AccountId == account.Id).ToList())
            _sessions.Remove(key);
        account.Running = false;
    }

    /// <summary>按会话唯一 Key 停止一个游戏进程。</summary>
    public void StopSessionByKey(string key)
    {
        if (_sessions.TryGetValue(key, out var session))
        {
            _ = StopSession(session);
            _sessions.Remove(key);
        }
    }

    /// <summary>
    /// 优雅停止进程：发送 WM_CLOSE 让 GameHost 正常退出（CEF 刷盘 cookie），
    /// 等待/强杀全部在后台线程执行，UI 立即返回。
    /// </summary>
    public Task StopSession(GameSession session, int timeoutMs = 3000)
    {
        if (session == null || session.Process.HasExited)
            return Task.CompletedTask;
        // 同步快速发送 WM_CLOSE（不耗时），让 GameHost 开始优雅退出
        try
        {
            var hwnd = session.ReadWindowHandle();
            if (hwnd != 0)
                PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
        // 等待退出与超时强杀放到后台线程，避免阻塞 UI
        return Task.Run(() => StopSessionCore(session, timeoutMs));
    }

    /// <summary>后台执行等待退出 / 超时强杀整个进程树 / 清理孤儿进程。</summary>
    private static void StopSessionCore(GameSession session, int timeoutMs)
    {
        try
        {
            if (session.Process.WaitForExit(timeoutMs))
            {
                session.Process.Dispose();
                return;
            }
        }
        catch { }
        // 超时未退出：用 taskkill 强杀整个进程树
        try
        {
            var psi = new ProcessStartInfo("taskkill.exe",
                $"/PID {session.Process.Id} /T /F")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var tk = Process.Start(psi);
            tk?.WaitForExit(3000);
        }
        catch { }
        try { if (!session.Process.HasExited) session.Process.Kill(); } catch { }
        session.Process.Dispose();
        KillOrphanedGameHosts();
    }

    /// <summary>清理已变成孤儿的 GameHost 进程（主进程已死但 CEF 子进程残留）。</summary>
    private static void KillOrphanedGameHosts()
    {
        try
        {
            var procs = Process.GetProcessesByName("cef_flash_game_host");
            if (procs.Length == 0) return;
            // 所有存活进程 PID 集合：父进程不在其中即视为孤儿
            var liveIds = new HashSet<int>(Process.GetProcesses().Select(p => p.Id));
            foreach (var p in procs)
            {
                try
                {
                    var ppid = GetParentProcessId(p.Id);
                    if (ppid == 0 || !liveIds.Contains(ppid))
                        p.Kill();
                }
                catch { }
            }
        }
        catch { }
    }

    private const int WM_CLOSE = 0x0010;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32First(nint hSnapshot, ref ProcessEntry32 lppe);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32Next(nint hSnapshot, ref ProcessEntry32 lppe);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint hObject);

    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    /// <summary>获取进程的父进程 PID（失败返回 0）。</summary>
    private static int GetParentProcessId(int pid)
    {
        var snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == nint.Zero) return 0;
        try
        {
            var entry = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32First(snap, ref entry)) return 0;
            do
            {
                if (entry.th32ProcessID == (uint)pid)
                    return (int)entry.th32ParentProcessID;
            } while (Process32Next(snap, ref entry));
        }
        finally
        {
            CloseHandle(snap);
        }
        return 0;
    }

    /// <summary>停止全部游戏进程。</summary>
    public void StopAll()
    {
        foreach (var session in _sessions.Values.ToList())
        {
            _ = StopSession(session);
            var acc = App.CurrentApp.Accounts.Accounts.FirstOrDefault(a => a.Id == session.AccountId);
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
        // Flash 硬件加速开关（默认关闭；开启需重新进入游戏才生效）
        psi.ArgumentList.Add(App.CurrentApp.Settings.FlashHardwareAcceleration
            ? "--flash-gpu=1" : "--flash-gpu=0");
        // 分辨率模式
        psi.ArgumentList.Add(App.CurrentApp.Settings.DprMode == Services.DprMode.Performance
            ? "--force-dpr=1" : "--force-dpr=0");
        if (parentHwnd != 0)
            psi.ArgumentList.Add($"--parent={parentHwnd}");

        try
        {
            var proc = Process.Start(psi);
            if (proc == null) return null;
            var key = "scan-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new GameSession
            {
                Key = key,
                AccountId = -1,  // 扫码登录会话暂不属于具体账号
                Process = proc,
                UserdataDir = userdata,
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动扫码登录失败: {ex.Message}");
            return null;
        }
    }
}
