using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Models;

namespace NarutoLauncher.Views;

/// <summary>
/// 游戏标签页数据：关联账号、GameHostView 和会话。
/// </summary>
public class GameTab
{
    public Account Account { get; set; } = null!;
    public GameHostView? HostView { get; set; }
    public StackPanel? Panel { get; set; }
    public StackPanel? Placeholder { get; set; }
    public SessionInfo? Session { get; set; }

    public bool IsRunning => Session != null && !Session.Process.HasExited;
}

/// <summary>
/// 封装一个游戏进程会话。
/// </summary>
public class SessionInfo
{
    public required Process Process { get; init; }
    public required string UserdataDir { get; init; }
}
