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
        var win = new MainWindow();
        MainWindow = win;
        // 主窗口句柄（WPF 窗口的 HWND，创建后获取）
        var helper = new System.Windows.Interop.WindowInteropHelper(win);
        win.SourceInitialized += (_, _) => MainWindowHandle = helper.Handle;
        win.Show();
    }
}
