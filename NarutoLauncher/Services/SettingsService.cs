using System.Text.Json;
using NarutoLauncher.Models;

namespace NarutoLauncher.Services;

/// <summary>界面主题模式。</summary>
public enum ThemeMode
{
    /// <summary>跟随系统。</summary>
    System = 0,

    /// <summary>深色。</summary>
    Dark = 1,

    /// <summary>浅色。</summary>
    Light = 2,
}

/// <summary>强调色模式。</summary>
public enum AccentMode
{
    /// <summary>跟随系统。</summary>
    System = 0,

    /// <summary>自定义。</summary>
    Custom = 1,
}

/// <summary>导航栏风格。</summary>
public enum NavigationStyle
{
    /// <summary>经典侧栏（200px，文字 + 图标，宽导航）。</summary>
    Classic = 0,

    /// <summary>简约 Store 风格（80px，竖排图标 + 小字）。</summary>
    Modern = 1,
}

/// <summary>分辨率模式（DPR）。</summary>
public enum DprMode
{
    /// <summary>性能优先：强制 DPR=1，低画质降质明显，大屏可能偏小。</summary>
    Performance = 0,

    /// <summary>画质优先：跟随系统 DPI，画面清晰，低画质降质不明显。</summary>
    Quality = 1,
}

/// <summary>
/// 应用设置：本地 JSON 持久化（设置页各项），属性变更时自动保存。
/// </summary>
public class SettingsService
{
    /// <summary>导航风格变更时触发（供 MainWindow 实时切换布局）。</summary>
    public event Action? NavigationStyleChanged;

    /// <summary>导航风格变更后触发（供 SettingsView 刷新下拉框）。</summary>
    public static event Action? NavigationStyleChangedExternally;

    /// <summary>账号头像显示类型变更时触发（游戏窗口 Tab 头像跟随刷新）。</summary>
    public event Action? AvatarDisplayChanged;
    private const string SettingsFileName = "settings.json";

    private readonly string _path;
    private bool _minimizeToTray = true;
    private bool _minimizeOnGameStart = true;
    private bool _showMainOnGameClose = true;
    private bool _rememberPassword = true;
    private bool _autoEnterGame;
    private bool _flashHardwareAcceleration = true;
    private string _flashQuality = "low";
    private DprMode _dprMode = DprMode.Performance;
    private ThemeMode _themeMode = ThemeMode.System;
    private AccentMode _accentMode = AccentMode.System;
    private string _accentColor = "#E8482C";
    private AvatarType _avatarDisplay = AvatarType.QqAvatar;
    private NavigationStyle _navigationStyle = NavigationStyle.Classic;
    private bool _enableDebugPort;
    private int _debugPort = 9222;

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NarutoLauncher");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, SettingsFileName);
        Load();
    }

    public bool MinimizeToTray { get => _minimizeToTray; set { if (_minimizeToTray != value) { _minimizeToTray = value; Save(); } } }
    public bool RememberPassword { get => _rememberPassword; set { if (_rememberPassword != value) { _rememberPassword = value; Save(); } } }

    /// <summary>启动游戏后，主窗口自动最小化到托盘。</summary>
    public bool MinimizeOnGameStart
    {
        get => _minimizeOnGameStart;
        set { if (_minimizeOnGameStart != value) { _minimizeOnGameStart = value; Save(); } }
    }

    /// <summary>开始游戏是否自动进入游戏（关闭则先进选区页，手动点开始）。</summary>
    public bool AutoEnterGame { get => _autoEnterGame; set { if (_autoEnterGame != value) { _autoEnterGame = value; Save(); } } }

    /// <summary>关闭游戏窗口后，自动显示启动器主界面。</summary>
    public bool ShowMainOnGameClose
    {
        get => _showMainOnGameClose;
        set { if (_showMainOnGameClose != value) { _showMainOnGameClose = value; Save(); } }
    }

    /// <summary>Flash 硬件加速（默认开启：GPU 合成显著提升流畅度，实测优于纯 CPU 合成）。</summary>
    public bool FlashHardwareAcceleration
    {
        get => _flashHardwareAcceleration;
        set
        {
            if (_flashHardwareAcceleration != value)
            {
                _flashHardwareAcceleration = value;
                Save();
            }
        }
    }

    /// <summary>Flash 渲染质量（low/medium/high，默认 low 流畅优先）。</summary>
    public string FlashQuality
    {
        get => _flashQuality;
        set
        {
            var v = value ?? "low";
            if (v != "medium" && v != "high")
                v = "low";
            if (_flashQuality != v)
            {
                _flashQuality = v;
                Save();
            }
        }
    }

    /// <summary>分辨率模式（性能优先=强制DPR1，画质优先=跟随系统DPI）。</summary>
    public DprMode DprMode
    {
        get => _dprMode;
        set
        {
            if (_dprMode != value)
            {
                _dprMode = value;
                Save();
            }
        }
    }

    /// <summary>界面主题模式。</summary>
    public ThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            if (_themeMode != value)
            {
                _themeMode = value;
                Save();
            }
        }
    }

    /// <summary>强调色模式。</summary>
    public AccentMode AccentMode
    {
        get => _accentMode;
        set
        {
            if (_accentMode != value)
            {
                _accentMode = value;
                Save();
            }
        }
    }

    /// <summary>自定义强调色（#RRGGBB）。</summary>
    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (_accentColor != value)
            {
                _accentColor = value;
                Save();
            }
        }
    }

    /// <summary>账号头像显示类型（账号管理页全局设置）。</summary>
    public AvatarType AvatarDisplay
    {
        get => _avatarDisplay;
        set
        {
            if (_avatarDisplay != value)
            {
                _avatarDisplay = value;
                Save();
                AvatarDisplayChanged?.Invoke();
            }
        }
    }

    /// <summary>导航栏风格（经典侧栏 / LLT Store 风格）。</summary>
    public NavigationStyle NavigationStyle
    {
        get => _navigationStyle;
        set
        {
            if (_navigationStyle != value)
            {
                _navigationStyle = value;
                Save();
                NavigationStyleChanged?.Invoke();
                NavigationStyleChangedExternally?.Invoke();
            }
        }
    }

    /// <summary>开启远程调试端口：启动游戏时给 GameHost 传 --debug-port，便于调试/联调。</summary>
    public bool EnableDebugPort
    {
        get => _enableDebugPort;
        set { if (_enableDebugPort != value) { _enableDebugPort = value; Save(); } }
    }

    /// <summary>远程调试端口（CEF CDP 端口）。</summary>
    public int DebugPort
    {
        get => _debugPort;
        set
        {
            var v = value < 1 || value > 65535 ? 9222 : value;
            if (_debugPort != v) { _debugPort = v; Save(); }
        }
    }

    public void Save()
    {
        try
        {
            var dto = new SettingsData
            {
                MinimizeToTray = _minimizeToTray,
                RememberPassword = _rememberPassword,
                MinimizeOnGameStart = _minimizeOnGameStart,
                ShowMainOnGameClose = _showMainOnGameClose,
                AutoEnterGame = _autoEnterGame,
                FlashHardwareAcceleration = _flashHardwareAcceleration,
                FlashQuality = _flashQuality,
                DprMode = _dprMode,
                ThemeMode = _themeMode,
                AccentMode = _accentMode,
                AccentColor = _accentColor,
                AvatarDisplay = _avatarDisplay,
                NavigationStyle = _navigationStyle,
                EnableDebugPort = _enableDebugPort,
                DebugPort = _debugPort,
            };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            var dto = JsonSerializer.Deserialize<SettingsData>(json);
            if (dto == null) return;
            _minimizeToTray = dto.MinimizeToTray;
            _rememberPassword = dto.RememberPassword;
            _minimizeOnGameStart = dto.MinimizeOnGameStart;
            _showMainOnGameClose = dto.ShowMainOnGameClose;
            _autoEnterGame = false;  // 自动登录已停用（忽略旧配置，强制关闭）
            _flashHardwareAcceleration = dto.FlashHardwareAcceleration;
            _flashQuality = string.IsNullOrEmpty(dto.FlashQuality) ? "low" : dto.FlashQuality;
            _dprMode = dto.DprMode;
            _themeMode = dto.ThemeMode;
            _accentMode = dto.AccentMode;
            _accentColor = string.IsNullOrEmpty(dto.AccentColor) ? "#E8482C" : dto.AccentColor;
            _avatarDisplay = dto.AvatarDisplay;
            _navigationStyle = dto.NavigationStyle;
            _enableDebugPort = dto.EnableDebugPort;
            _debugPort = dto.DebugPort < 1 || dto.DebugPort > 65535 ? 9222 : dto.DebugPort;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
        }
    }

    /// <summary>序列化 DTO（避免反序列化自身触发构造函数递归）。</summary>
    private class SettingsData
    {
        public bool MinimizeToTray { get; set; } = true;
        public bool RememberPassword { get; set; } = true;
        public bool MinimizeOnGameStart { get; set; } = true;
        public bool ShowMainOnGameClose { get; set; } = true;
        public bool AutoEnterGame { get; set; }
        public bool FlashHardwareAcceleration { get; set; }
        public string FlashQuality { get; set; } = "low";
        public DprMode DprMode { get; set; } = DprMode.Performance;
        public ThemeMode ThemeMode { get; set; } = ThemeMode.System;
        public AccentMode AccentMode { get; set; } = AccentMode.System;
        public string AccentColor { get; set; } = "#E8482C";
        public AvatarType AvatarDisplay { get; set; } = AvatarType.NameChar;
        public NavigationStyle NavigationStyle { get; set; } = NavigationStyle.Classic;
        public bool EnableDebugPort { get; set; }
        public int DebugPort { get; set; } = 9222;
    }
}
