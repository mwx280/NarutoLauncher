using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;
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

    /// <summary>共享 WebView2 环境（预创建，避免每次打开公告详情重新初始化）。</summary>
    public static CoreWebView2Environment? WebViewEnv { get; private set; }

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

        // 后台预创建 WebView2 环境（减少公告详情首次打开延迟）
        _ = CreateWebViewEnvAsync();

        var win = new MainWindow();
        MainWindow = win;
        // 主窗口句柄（WPF 窗口的 HWND，创建后获取）
        var helper = new System.Windows.Interop.WindowInteropHelper(win);
        win.SourceInitialized += (_, _) => MainWindowHandle = helper.Handle;
        win.Show();
    }

    private static async Task CreateWebViewEnvAsync()
    {
        try
        {
            WebViewEnv ??= await CoreWebView2Environment.CreateAsync();
        }
        catch
        {
            // 环境创建失败时忽略（窗口内会再次尝试默认环境）
        }
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
