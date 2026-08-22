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

    /// <summary>LLT Store 风格（80px，竖排图标 + 小字）。</summary>
    Modern = 1,
}

/// <summary>
/// 应用设置：本地 JSON 持久化（设置页各项），属性变更时自动保存。
/// </summary>
public class SettingsService
{
    /// <summary>导航风格变更时触发（供 MainWindow 实时切换布局）。</summary>
    public event Action? NavigationStyleChanged;
    private const string SettingsFileName = "settings.json";

    private readonly string _path;
    private bool _gameSpeed = true;
    private bool _antiDrop = true;
    private bool _autoScript;
    private bool _autoTask;
    private bool _minimizeToTray = true;
    private bool _rememberPassword = true;
    private bool _flashHardwareAcceleration = true;
    private string _flashQuality = "low";
    private ThemeMode _themeMode = ThemeMode.System;
    private AccentMode _accentMode = AccentMode.System;
    private string _accentColor = "#E8482C";
    private AvatarType _avatarDisplay = AvatarType.NameChar;
    private NavigationStyle _navigationStyle = NavigationStyle.Classic;

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NarutoLauncher");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, SettingsFileName);
        Load();
    }

    public bool GameSpeed { get => _gameSpeed; set { if (_gameSpeed != value) { _gameSpeed = value; Save(); } } }
    public bool AntiDrop { get => _antiDrop; set { if (_antiDrop != value) { _antiDrop = value; Save(); } } }
    public bool AutoScript { get => _autoScript; set { if (_autoScript != value) { _autoScript = value; Save(); } } }
    public bool AutoTask { get => _autoTask; set { if (_autoTask != value) { _autoTask = value; Save(); } } }
    public bool MinimizeToTray { get => _minimizeToTray; set { if (_minimizeToTray != value) { _minimizeToTray = value; Save(); } } }
    public bool RememberPassword { get => _rememberPassword; set { if (_rememberPassword != value) { _rememberPassword = value; Save(); } } }

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
            }
        }
    }

    public void Save()
    {
        try
        {
            var dto = new SettingsData
            {
                GameSpeed = _gameSpeed,
                AntiDrop = _antiDrop,
                AutoScript = _autoScript,
                AutoTask = _autoTask,
                MinimizeToTray = _minimizeToTray,
                RememberPassword = _rememberPassword,
                FlashHardwareAcceleration = _flashHardwareAcceleration,
                FlashQuality = _flashQuality,
                ThemeMode = _themeMode,
                AccentMode = _accentMode,
                AccentColor = _accentColor,
                AvatarDisplay = _avatarDisplay,
                NavigationStyle = _navigationStyle,
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
            _gameSpeed = dto.GameSpeed;
            _antiDrop = dto.AntiDrop;
            _autoScript = dto.AutoScript;
            _autoTask = dto.AutoTask;
            _minimizeToTray = dto.MinimizeToTray;
            _rememberPassword = dto.RememberPassword;
            _flashHardwareAcceleration = dto.FlashHardwareAcceleration;
            _flashQuality = string.IsNullOrEmpty(dto.FlashQuality) ? "low" : dto.FlashQuality;
            _themeMode = dto.ThemeMode;
            _accentMode = dto.AccentMode;
            _accentColor = string.IsNullOrEmpty(dto.AccentColor) ? "#E8482C" : dto.AccentColor;
            _avatarDisplay = dto.AvatarDisplay;
            _navigationStyle = dto.NavigationStyle;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
        }
    }

    /// <summary>序列化 DTO（避免反序列化自身触发构造函数递归）。</summary>
    private class SettingsData
    {
        public bool GameSpeed { get; set; } = true;
        public bool AntiDrop { get; set; } = true;
        public bool AutoScript { get; set; }
        public bool AutoTask { get; set; }
        public bool MinimizeToTray { get; set; } = true;
        public bool RememberPassword { get; set; } = true;
        public bool FlashHardwareAcceleration { get; set; }
        public string FlashQuality { get; set; } = "low";
        public ThemeMode ThemeMode { get; set; } = ThemeMode.System;
        public AccentMode AccentMode { get; set; } = AccentMode.System;
        public string AccentColor { get; set; } = "#E8482C";
        public AvatarType AvatarDisplay { get; set; } = AvatarType.NameChar;
        public NavigationStyle NavigationStyle { get; set; } = NavigationStyle.Classic;
    }
}
