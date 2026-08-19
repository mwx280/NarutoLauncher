using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NarutoLauncher.Models;

/// <summary>头像显示类型。</summary>
public enum AvatarType
{
    /// <summary>备注首字（无备注用 QQ 首字）。</summary>
    NameChar = 0,

    /// <summary>账号第一个数字。</summary>
    QqFirstDigit = 1,

    /// <summary>QQ 头像（网络加载）。</summary>
    QqAvatar = 2,
}

/// <summary>
/// 启动器账号（多开维度）。
/// </summary>
public class Account : INotifyPropertyChanged
{
    private string _name = "";
    private string _qq = "";
    private string _pwd = "";
    private string _server = "";
    private int _level;
    private string _power = "";
    private bool _running;
    private bool _loggedIn;
    private bool _scanLogin;
    private string _scanUserDataDir = "";
    private string _cookies = "";
    private AvatarType _avatarType = AvatarType.NameChar;

    public long Id { get; set; }

    /// <summary>QQ 号。</summary>
    public string QQ { get => _qq; set { _qq = value; OnChanged(); } }

    /// <summary>昵称/备注。</summary>
    public string Name { get => _name; set { _name = value; OnChanged(); } }

    /// <summary>密码（仅内存持有，序列化时加密）。</summary>
    public string Password { get => _pwd; set { _pwd = value; OnChanged(); } }

    /// <summary>所在区服。</summary>
    public string Server { get => _server; set { _server = value; OnChanged(); } }

    /// <summary>角色等级。</summary>
    public int Level { get => _level; set { _level = value; OnChanged(); } }

    /// <summary>战力文本。</summary>
    public string Power { get => _power; set { _power = value; OnChanged(); } }

    /// <summary>是否已登录（角色信息是否同步到）。</summary>
    public bool LoggedIn { get => _loggedIn; set { _loggedIn = value; OnChanged(); } }

    /// <summary>是否运行中（对应 GameHost 进程）。</summary>
    public bool Running { get => _running; set { _running = value; OnChanged(); } }

    /// <summary>是否扫码登录（无密码）。</summary>
    public bool ScanLogin { get => _scanLogin; set { _scanLogin = value; OnChanged(); } }

    /// <summary>扫码登录的 userdata 目录（cookie 持久化，游戏启动复用）。</summary>
    public string ScanUserDataDir { get => _scanUserDataDir; set { _scanUserDataDir = value; OnChanged(); } }

    /// <summary>登录 cookie 文本（key=value; ...，游戏启动时注入 GameHost）。</summary>
    public string Cookies { get => _cookies; set { _cookies = value; OnChanged(); } }

    /// <summary>头像显示类型。</summary>
    public AvatarType AvatarType { get => _avatarType; set { _avatarType = value; OnChanged(); } }

    /// <summary>头像配色种子。</summary>
    public int Seed { get; set; }

    /// <summary>头像字符（备注首字或账号首数字，由 AvatarType 决定）。</summary>
    public string AvatarChar =>
        AvatarType == AvatarType.QqFirstDigit
            ? (!string.IsNullOrEmpty(QQ) ? QQ[..1] : "?")
            : (!string.IsNullOrEmpty(Name) ? Name[..1]
               : !string.IsNullOrEmpty(QQ) ? QQ[..1] : "?");

    /// <summary>是否使用 QQ 网络头像。</summary>
    public bool UseQqAvatar => AvatarType == AvatarType.QqAvatar && !string.IsNullOrEmpty(QQ);

    /// <summary>显示名。</summary>
    public string DisplayName => string.IsNullOrEmpty(Name) ? QQ : Name;

    /// <summary>信息摘要（区服/等级/战力）。</summary>
    public string InfoText =>
        LoggedIn ? $"{Server} · Lv.{Level} · {Power}"
                 : "未获取（登录后同步）";

    /// <summary>登录类型文本。</summary>
    public string LoginTypeText => ScanLogin ? "扫码登录" : "账号密码";

    /// <summary>QQ 头像 URL（公开接口，仅需 QQ 号）。</summary>
    public string AvatarUrl => string.IsNullOrEmpty(QQ)
        ? ""
        : $"https://q1.qlogo.cn/g?b=qq&nk={QQ}&s=100";

    /// <summary>运行状态文本。</summary>
    public string RunText => Running ? "● 游戏中" : "未运行";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(Name) or nameof(QQ))
            OnChanged(nameof(AvatarChar));
        if (name == nameof(AvatarType))
        {
            OnChanged(nameof(AvatarChar));
            OnChanged(nameof(UseQqAvatar));
        }
        if (name is nameof(Name) or nameof(QQ))
            OnChanged(nameof(DisplayName));
        if (name is nameof(Server) or nameof(Level) or nameof(Power) or nameof(LoggedIn))
            OnChanged(nameof(InfoText));
        if (name == nameof(ScanLogin))
            OnChanged(nameof(LoginTypeText));
        if (name == nameof(Running))
            OnChanged(nameof(RunText));
    }
}
