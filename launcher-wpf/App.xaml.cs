using System.Text;
using System.Windows;
using NarutoLauncher.Services;

namespace NarutoLauncher;

public partial class App : Application
{
    /// <summary>全局服务容器（各窗口共享）。</summary>
    public static App CurrentApp { get; private set; } = null!;

    public AccountService Accounts { get; } = new();
    public SettingsService Settings { get; } = new();
    public GameProcessService Games { get; } = new();

    /// <summary>主窗口句柄（供 GameHost 嵌入）。</summary>
    public nint MainWindowHandle { get; set; }

    public App()
    {
        CurrentApp = this;
        // 注册 GB2312/GBK 编码（官网公告页面使用）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 应用主题（深色/浅色 + 强调色）
        var s = Settings;
        var accent = ParseColor(s.AccentColor);
        ThemeManager.Apply(s.ThemeMode, s.AccentMode == AccentMode.Custom, accent);

        var win = new MainWindow();
        MainWindow = win;
        // 主窗口句柄（WPF 窗口的 HWND，创建后获取）
        var helper = new System.Windows.Interop.WindowInteropHelper(win);
        win.SourceInitialized += (_, _) => MainWindowHandle = helper.Handle;
        win.Show();
    }

    /// <summary>解析 #RRGGBB 颜色。</summary>
    private static System.Windows.Media.Color ParseColor(string hex)
    {
        try
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return System.Windows.Media.Color.FromRgb(0xE8, 0x48, 0x2C);
        }
    }
}
