using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows;
using NarutoLauncher.Services;
using Wpf.Ui;
using WinForms = System.Windows.Forms;

namespace NarutoLauncher;

public partial class App : Application
{
    /// <summary>全局服务容器（各窗口共享）。</summary>
    public static App CurrentApp { get; private set; } = null!;

    public AccountService Accounts { get; } = new();
    public SettingsService Settings { get; } = new();
    public GameProcessService Games { get; } = new();

    /// <summary>对话框服务（UI 风格 ContentDialog 提示框宿主）。</summary>
    public ContentDialogService DialogService { get; } = new();

    /// <summary>主窗口句柄（供 GameHost 嵌入）。</summary>
    public nint MainWindowHandle { get; set; }

    /// <summary>是否正在退出应用（放行窗口关闭，避免托盘拦截）。</summary>
    public bool IsExiting { get; private set; }

    private WinForms.NotifyIcon? _trayIcon;

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

        CreateTrayIcon();
    }

    /// <summary>创建托盘图标（苦无 favicon）+ 菜单：主窗口 / 开始游戏（二级账号）/ 退出。</summary>
    private void CreateTrayIcon()
    {
        _trayIcon = new WinForms.NotifyIcon { Visible = true, Text = "火影忍者OL" };
        try
        {
            using var stream = Application.GetResourceStream(
                new Uri("pack://application:,,,/assets/favicon.png", UriKind.Absolute))?.Stream;
            if (stream != null)
            {
                using var bmp = new Bitmap(stream);
                _trayIcon.Icon = Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch
        {
            // 图标加载失败不阻塞托盘功能
        }

        var menu = new WinForms.ContextMenuStrip();

        var home = new WinForms.ToolStripMenuItem("主窗口");
        home.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(home);

        var start = new WinForms.ToolStripMenuItem("开始游戏");
        menu.Items.Add(start);
        menu.Opening += (_, _) =>
        {
            start.DropDownItems.Clear();
            foreach (var acc in Accounts.Accounts)
            {
                var accRef = acc;
                var item = new WinForms.ToolStripMenuItem(acc.DisplayName);
                item.Click += (_, _) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (MainWindow != null)
                        {
                            MainWindow.Show();
                            if (MainWindow.WindowState == WindowState.Minimized)
                                MainWindow.WindowState = WindowState.Normal;
                        }
                        Views.GameWindow.OpenAccount(accRef, MainWindow);
                    });
                };
                start.DropDownItems.Add(item);
            }
        };

        var exit = new WinForms.ToolStripMenuItem("退出");
        exit.Click += (_, _) =>
        {
            IsExiting = true;
            Shutdown();
        };
        menu.Items.Add(exit);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    /// <summary>显示并激活主窗口（最小化则恢复）。</summary>
    public void ShowMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            var win = MainWindow;
            if (win == null)
                return;
            win.Show();
            if (win.WindowState == WindowState.Minimized)
                win.WindowState = WindowState.Normal;
            win.Activate();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        base.OnExit(e);
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