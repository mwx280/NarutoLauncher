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
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
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
            var loaded = JsonSerializer.Deserialize<SettingsService>(json);
            if (loaded == null) return;
            _gameSpeed = loaded._gameSpeed;
            _antiDrop = loaded._antiDrop;
            _autoScript = loaded._autoScript;
            _autoTask = loaded._autoTask;
            _darkTheme = loaded._darkTheme;
            _minimizeToTray = loaded._minimizeToTray;
            _rememberPassword = loaded._rememberPassword;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
        }
    }
}
