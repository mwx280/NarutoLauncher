using System.Text.Json;

namespace NarutoLauncher.Services;

/// <summary>
/// 应用设置：本地 JSON 持久化（设置页各项），属性变更时自动保存。
/// </summary>
public class SettingsService
{
    private const string SettingsFileName = "settings.json";

    private readonly string _path;
    private bool _gameSpeed = true;
    private bool _antiDrop = true;
    private bool _autoScript;
    private bool _autoTask;
    private bool _darkTheme;
    private bool _minimizeToTray = true;
    private bool _rememberPassword = true;

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
    public bool DarkTheme { get => _darkTheme; set { if (_darkTheme != value) { _darkTheme = value; Save(); } } }
    public bool MinimizeToTray { get => _minimizeToTray; set { if (_minimizeToTray != value) { _minimizeToTray = value; Save(); } } }
    public bool RememberPassword { get => _rememberPassword; set { if (_rememberPassword != value) { _rememberPassword = value; Save(); } } }

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
                DarkTheme = _darkTheme,
                MinimizeToTray = _minimizeToTray,
                RememberPassword = _rememberPassword,
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
            _darkTheme = dto.DarkTheme;
            _minimizeToTray = dto.MinimizeToTray;
            _rememberPassword = dto.RememberPassword;
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
        public bool DarkTheme { get; set; }
        public bool MinimizeToTray { get; set; } = true;
        public bool RememberPassword { get; set; } = true;
    }
}
