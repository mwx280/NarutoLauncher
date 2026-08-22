using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using Media = System.Windows.Media;
using NarutoLauncher.Services;
using Wpf.Ui;
using Ui = Wpf.Ui.Controls;

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

    private TrayIcon? _trayIcon;
    private Mutex? _instanceMutex;

    /// <summary>单实例互斥体名称（第二个实例启动时检测并激活现有实例）。</summary>
    private const string InstanceMutexName = "NarutoLauncher_SingleInstance";

    public App()
    {
        CurrentApp = this;
        // 注册 GB2312/GBK 编码（官网公告页面使用）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例：已有实例则激活其主窗口后退出
        _instanceMutex = new Mutex(true, InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }

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

    /// <summary>创建托盘图标（苦无 favicon）+ WPF 菜单：主窗口 / 开始游戏（二级账号）/ 退出。</summary>
    private void CreateTrayIcon()
    {
        _trayIcon = new TrayIcon
        {
            Text = "火影忍者OL",
            ContextMenu = BuildTrayMenu(),
            Visible = true,
        };
        _trayIcon.OnClick += (_, _) => ShowMainWindow();
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
    }

    /// <summary>构建托盘 WPF 菜单（WPF-UI MenuItem，动态主题：不透明背景 + 主题文字/图标色）。</summary>
    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();
        ApplyTheme(menu, ContextMenu.BackgroundProperty,
            "SolidBackgroundFillColorBaseBrush", 0xFF1E1E1E);
        ApplyTheme(menu, ContextMenu.BorderBrushProperty,
            "CardStrokeColorDefaultBrush", 0xFF3A3A3A);
        ApplyTheme(menu, ContextMenu.ForegroundProperty,
            "TextFillColorPrimaryBrush", 0xFFE8E8E8);

        var home = new Ui.MenuItem { Header = "主窗口", Icon = MakeIcon(Ui.SymbolRegular.Home24) };
        ApplyMenuItemTheme(home);
        home.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(home);

        var start = new Ui.MenuItem { Header = "开始游戏", Icon = MakeIcon(Ui.SymbolRegular.Play24) };
        ApplyMenuItemTheme(start);
        menu.Items.Add(start);
        menu.Opened += (_, _) =>
        {
            start.Items.Clear();
            foreach (var acc in Accounts.Accounts)
            {
                var accRef = acc;
                var item = new Ui.MenuItem
                {
                    Header = acc.DisplayName,
                    Icon = MakeIcon(Ui.SymbolRegular.Person20),
                };
                ApplyMenuItemTheme(item);
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
                start.Items.Add(item);
            }
        };

        var exit = new Ui.MenuItem { Header = "退出", Icon = MakeIcon(Ui.SymbolRegular.SignOut24) };
        ApplyMenuItemTheme(exit);
        exit.Click += (_, _) =>
        {
            IsExiting = true;
            Shutdown();
        };
        menu.Items.Add(exit);

        return menu;
    }

    /// <summary>菜单项前景跟随主题文字色（深色下白色，浅色下黑色）。</summary>
    private static void ApplyMenuItemTheme(Ui.MenuItem item)
    {
        ApplyTheme(item, Control.ForegroundProperty,
            "TextFillColorPrimaryBrush", 0xFFE8E8E8);
    }

    /// <summary>创建菜单图标（前景跟随主题文字色）。</summary>
    private static Ui.SymbolIcon MakeIcon(Ui.SymbolRegular symbol)
    {
        var icon = new Ui.SymbolIcon { Symbol = symbol, FontSize = 16 };
        ApplyTheme(icon, Control.ForegroundProperty,
            "TextFillColorPrimaryBrush", 0xFFE8E8E8);
        return icon;
    }

    /// <summary>动态绑定主题资源（主题切换自动更新）；键不存在时用不透明兜底色。</summary>
    private static void ApplyTheme(FrameworkElement el, DependencyProperty dp,
                                   string key, uint fallbackRgb)
    {
        if (CurrentApp.Resources[key] is Media.Brush)
        {
            el.SetResourceReference(dp, key);
        }
        else
        {
            el.SetValue(dp, new Media.SolidColorBrush(Media.Color.FromRgb(
                (byte)(fallbackRgb >> 16), (byte)(fallbackRgb >> 8),
                (byte)fallbackRgb)));
        }
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
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>激活已运行实例的主窗口（恢复并前置）。</summary>
    private static void ActivateExistingInstance()
    {
        var hwnd = FindWindow(null, "火影忍者OL 启动器");
        if (hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, 9);  // SW_RESTORE
            SetForegroundWindow(hwnd);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

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