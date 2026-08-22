using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NarutoLauncher.Converters;
using NarutoLauncher.Models;
using NarutoLauncher.Services;
using Path = System.Windows.Shapes.Path;

namespace NarutoLauncher.Views;

/// <summary>
/// 多开游戏宿主窗口：完全自绘界面（原生 WPF，不依赖 wpf-ui 控件库）。
/// 顶部 Chrome 式标签条（品牌标题 + 多开标签 + 窗口控制按钮），
/// 下方工具栏（图标 + 文字按钮），最下为内嵌游戏画面区域。
/// 无边框窗口：边缘缩放 + 标签条拖动（WM_NCHITTEST）由本地消息处理实现。
/// </summary>
public partial class GameWindow : Window
{
    private sealed class SessionTab
    {
        public required Account Account { get; init; }
        public required GameSession Session { get; init; }
        public required GameHostView Host { get; init; }
        public required Border TabItem { get; init; }
        public required Border AvatarHost { get; init; }
        public required TextBlock TitleText { get; init; }
        public required TextBlock CloseText { get; init; }
    }

    public static GameWindow? Shared { get; private set; }

    /// <summary>选区页：开始游戏先进选区页，用户点「开始游戏」进对应区（可换区）。</summary>
    private const string ServerSelectUrl = "https://huoying.qq.com/server/website/";

    private readonly List<SessionTab> _tabs = new();
    private SessionTab? _activeTab;
    private bool _isFullScreen;
    private Rect _normalBounds;

    private const int WM_APP = 0x8000;
    private const int CmdRefresh = 1;
    private const int CmdSelectServer = 3;
    private const int CmdToggleMute = 4;
    private const int CmdToggleSpeed = 5;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    // ---- 自绘界面使用的静态颜色与画笔 ----
    private static readonly Color ColorAccent = Color.FromRgb(0xE8, 0x48, 0x2C);
    private static readonly Brush BrushAccent = new SolidColorBrush(ColorAccent);
    private static readonly Brush BrushText = new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xED));
    private static readonly Brush BrushTextDim = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));
    private static readonly Brush BrushHover = new SolidColorBrush(Color.FromRgb(0x3C, 0x40, 0x43));
    private static readonly Brush BrushTabHover = new SolidColorBrush(Color.FromRgb(0x28, 0x2A, 0x2D));
    private static readonly Brush BrushCloseHover = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly Brush BrushWhite = Brushes.White;
    private static readonly Brush BrushTransparent = Brushes.Transparent;
    private static readonly Brush BrushLogo = new LinearGradientBrush(
        Color.FromRgb(0xFF, 0x6B, 0x3D), Color.FromRgb(0xC2, 0x2D, 0x18), 45);

    private const double TabStripHeight = 46;
    private const double ToolbarHeight = 45;

    // ---- SVG 风格图标路径（24x24 viewBox） ----
    private const string IconRefresh =
        "M17.65 6.35A7.95 7.95 0 0 0 12 4a8 8 0 1 0 7.9 9h-2.02A6 6 0 1 1 12 6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z";
    private const string IconMap =
        "M20.5 3l-.16.03L15 5.1 9 3 3.36 4.9c-.21.07-.36.25-.36.48V20.5c0 .28.22.5.5.5l.16-.03L9 18.9l6 2.1 5.64-1.9c.21-.07.36-.25.36-.48V3.5c0-.28-.22-.5-.5-.5zM15 19l-6-2.11V5l6 2.11V19z";
    private const string IconVolume =
        "M3 9v6h4l5 5V4L7 9H3zm13.5 3A4.5 4.5 0 0 0 14 7.97v8.05A4.47 4.47 0 0 0 16.5 12zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z";
    private const string IconVolumeOff =
        "M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z";
    private const string IconPlay =
        "M8 5v14l11-7z";
    private const string IconPerson =
        "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z";
    private const string IconFullscreen =
        "M7 14H5v5h5v-2H7v-3zm-2-4h2V7h3V5H5v5zm12 7h-3v2h5v-5h-2v3zM14 5v2h3v3h2V5h-5z";

    // 窗口控制按钮图标（12x12 viewBox）
    private const string IconMin = "M0,6 H12 V8 H0 Z";
    private const string IconMax = "M0,0 H12 V12 H0 Z";
    private const string IconRestore = "M0,5 H5 V0 H12 V7 H7 V12 H0 Z";
    private const string IconClose = "M0,0 L12,12 M12,0 L0,12";

    // ---- 无边框窗口消息常量 ----
    private const int WM_NCHITTEST = 0x84;
    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private HwndSource? _source;

    // ---- 自绘界面控件 ----
    private Grid _root = null!;
    private StackPanel _tabPanel = null!;
    private StackPanel _winBtns = null!;
    private StackPanel _placeholder = null!;
    private TextBlock _placeholderText = null!;
    private Grid _gameHostContainer = null!;
    private StackPanel _userListPanel = null!;
    private Button _minBtn = null!;
    private Button _maxBtn = null!;
    private Button _closeBtn = null!;
    private Button _muteBtn = null!;
    private Button _speedBtn = null!;
    private Button _userBtn = null!;
    private Path _muteIcon = null!;
    private Path _muteIconMuted = null!;
    private Path _maxIcon = null!;
    private Path _restoreIcon = null!;
    private Popup _userPopup = null!;
    private Popup _speedPopup = null!;
    private bool _muted;

    private readonly DispatcherTimer _popupTimer;

    public static GameWindow GetShared(Window? owner)
    {
        var win = Shared;
        if (win == null || !win.IsLoaded)
        {
            win = new GameWindow { Owner = owner };
            Shared = win;
            win.Show();
        }
        return win;
    }

    public static void OpenAccount(Account account, Window? owner)
    {
        GetShared(owner).AddAccount(account);
        // 启动游戏后主窗口最小化到托盘（设置开启时）
        if (App.CurrentApp.Settings.MinimizeOnGameStart &&
            App.CurrentApp.MainWindow is { } main &&
            main.IsVisible)
        {
            main.Hide();
        }
    }

    public GameWindow()
    {
        InitializeComponent();
        _root = Root;
        BuildUi();
        Closed += OnWindowClosed;
        StateChanged += OnWindowStateChanged;
        SourceInitialized += OnSourceInitialized;
        // 账号头像设置变化时，刷新所有标签头像
        App.CurrentApp.Settings.AvatarDisplayChanged += OnAvatarDisplayChanged;
        // hover 菜单：移出立即关闭；按钮移出允许短暂移入菜单（80ms）
        _popupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _popupTimer.Tick += (_, _) => CloseHoverPopups();
    }

    // ==================== 界面构建（完全用 C# 自绘） ====================

    private void BuildUi()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TabStripHeight) });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ToolbarHeight) });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var tabStrip = BuildTabStrip();
        Grid.SetRow(tabStrip, 0);
        _root.Children.Add(tabStrip);

        var toolbar = BuildToolbar();
        Grid.SetRow(toolbar, 1);
        _root.Children.Add(toolbar);

        var viewport = BuildViewport();
        Grid.SetRow(viewport, 2);
        _root.Children.Add(viewport);

        BuildPopups();
        _root.Children.Add(_userPopup);
        _root.Children.Add(_speedPopup);
    }

    /// <summary>顶部标签条：品牌标题 + 多开标签 + 窗口控制按钮。</summary>
    private Border BuildTabStrip()
    {
        var border = new Border { Background = new SolidColorBrush(Color.FromRgb(0x20, 0x21, 0x24)) };
        var dock = new DockPanel();

        // 品牌标题
        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(10, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        var logo = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(6),
            Background = BrushLogo,
        };
        logo.Child = new TextBlock
        {
            Text = "火",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = BrushWhite,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        brand.Children.Add(logo);
        brand.Children.Add(new TextBlock
        {
            Text = "火影忍者Online",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushText,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        DockPanel.SetDock(brand, Dock.Left);
        dock.Children.Add(brand);

        // 窗口控制按钮
        _winBtns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _minBtn = WinButton("最小化", FillIcon(IconMin, 12, BrushTextDim), BrushTabHover);
        _minBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        _winBtns.Children.Add(_minBtn);

        _maxBtn = WinButton("最大化", null, BrushTabHover);
        var maxGrid = new Grid { Width = 12, Height = 12 };
        _maxIcon = StrokeIcon(IconMax, 12, BrushTextDim);
        _restoreIcon = StrokeIcon(IconRestore, 12, BrushTextDim);
        _restoreIcon.Visibility = Visibility.Collapsed;
        maxGrid.Children.Add(_maxIcon);
        maxGrid.Children.Add(_restoreIcon);
        _maxBtn.Content = maxGrid;
        _maxBtn.Click += (_, _) => OnMaximizeClick();
        _winBtns.Children.Add(_maxBtn);

        _closeBtn = WinButton("关闭", StrokeIcon(IconClose, 12, BrushTextDim), BrushCloseHover);
        _closeBtn.Click += (_, _) => Close();
        _winBtns.Children.Add(_closeBtn);

        DockPanel.SetDock(_winBtns, Dock.Right);
        dock.Children.Add(_winBtns);

        // 多开标签容器
        _tabPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(6, 6, 6, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        dock.Children.Add(_tabPanel);

        border.Child = dock;
        return border;
    }

    /// <summary>工具栏：图标 + 文字按钮（刷新/选区/静音/倍速/账号/全屏）。</summary>
    private Border BuildToolbar()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x29, 0x2A, 0x2D)),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var refreshBtn = ToolButton("刷新", IconRefresh);
        refreshBtn.Click += (_, _) => SendCommand(CmdRefresh);
        refreshBtn.ToolTip = "重新加载当前选中的游戏页面";
        panel.Children.Add(refreshBtn);

        var selectBtn = ToolButton("选区", IconMap);
        selectBtn.Click += (_, _) => SendCommand(CmdSelectServer);
        selectBtn.ToolTip = "选区（回到官网选区页）";
        panel.Children.Add(selectBtn);

        // 静音按钮：两个图标切换
        _muteBtn = ToolButton("静音", IconVolume);
        _muteBtn.ToolTip = "静音";
        var muteGrid = new Grid();
        _muteIcon = FillIcon(IconVolume, 14, BrushTextDim);
        _muteIconMuted = FillIcon(IconVolumeOff, 14, BrushTextDim);
        _muteIconMuted.Visibility = Visibility.Collapsed;
        muteGrid.Children.Add(_muteIcon);
        muteGrid.Children.Add(_muteIconMuted);
        var muteSp = new StackPanel { Orientation = Orientation.Horizontal };
        muteSp.Children.Add(muteGrid);
        muteSp.Children.Add(new TextBlock
        {
            Text = "静音",
            FontSize = 12,
            Foreground = BrushTextDim,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        _muteBtn.Content = muteSp;
        _muteBtn.Click += (_, _) => OnToggleMute();
        panel.Children.Add(_muteBtn);

        _speedBtn = ToolButton("倍速", IconPlay);
        _speedBtn.ToolTip = "倍速";
        _speedBtn.MouseEnter += OnSpeedBtnMouseEnter;
        _speedBtn.MouseLeave += OnSpeedBtnMouseLeave;
        panel.Children.Add(_speedBtn);

        _userBtn = ToolButton("账号", IconPerson);
        _userBtn.ToolTip = "切换游戏账号";
        _userBtn.MouseEnter += OnUserBtnMouseEnter;
        _userBtn.MouseLeave += OnUserBtnMouseLeave;
        panel.Children.Add(_userBtn);

        var fullscreenBtn = ToolButton("全屏", IconFullscreen);
        fullscreenBtn.Click += (_, _) => OnToggleFullscreen();
        fullscreenBtn.ToolTip = "全屏";
        panel.Children.Add(fullscreenBtn);

        border.Child = panel;
        return border;
    }

    /// <summary>游戏画面区域。</summary>
    private Border BuildViewport()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            ClipToBounds = true,
        };
        var grid = new Grid();
        _placeholder = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _placeholderText = new TextBlock
        {
            Text = "尚未启动游戏",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _placeholder.Children.Add(_placeholderText);
        _placeholder.Children.Add(new TextBlock
        {
            Text = "在「账号管理」页点击账号的开始游戏按钮即可添加标签",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        });
        grid.Children.Add(_placeholder);
        _gameHostContainer = new Grid();
        grid.Children.Add(_gameHostContainer);
        border.Child = grid;
        return border;
    }

    /// <summary>hover 菜单：账号切换列表 + 倍速菜单。</summary>
    private void BuildPopups()
    {
        _userPopup = new Popup
        {
            Placement = PlacementMode.Bottom,
            AllowsTransparency = true,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade,
            IsOpen = false,
        };
        var userBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x26, 0x28, 0x2C)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x41)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 4, 0, 0),
            MaxHeight = 360,
        };
        userBorder.MouseEnter += OnUserMenuPopupEnter;
        userBorder.MouseLeave += OnUserMenuPopupLeave;
        _userListPanel = new StackPanel();
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.Content = _userListPanel;
        userBorder.Child = scroll;
        _userPopup.Child = userBorder;

        _speedPopup = new Popup
        {
            Placement = PlacementMode.Bottom,
            AllowsTransparency = true,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade,
            IsOpen = false,
        };
        var speedBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x26, 0x28, 0x2C)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x41)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 4, 0, 0),
            MinWidth = 140,
        };
        speedBorder.MouseEnter += OnSpeedMenuPopupEnter;
        speedBorder.MouseLeave += OnSpeedMenuPopupLeave;
        var speedPanel = new StackPanel();
        speedPanel.Children.Add(new TextBlock
        {
            Text = "倍速",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushTextDim,
            Margin = new Thickness(4, 0, 0, 6),
        });
        speedPanel.Children.Add(SpeedRadio("0.5 倍速", "5"));
        speedPanel.Children.Add(SpeedRadio("1 倍速", "10", true));
        speedPanel.Children.Add(SpeedRadio("2 倍速", "20"));
        speedPanel.Children.Add(SpeedRadio("4 倍速", "40"));
        speedBorder.Child = speedPanel;
        _speedPopup.Child = speedBorder;
    }

    private static RadioButton SpeedRadio(string text, string tag, bool isChecked = false)
    {
        var rb = new RadioButton
        {
            Content = text,
            Tag = tag,
            IsChecked = isChecked,
            GroupName = "SpeedGroup",
            FontSize = 13,
            Foreground = BrushText,
            Margin = new Thickness(4, 3, 4, 3),
        };
        rb.Checked += OnSpeedChanged;
        return rb;
    }

    /// <summary>工具栏按钮：图标 + 文字，hover 变背景。</summary>
    private static Button ToolButton(string text, string iconData)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(FillIcon(iconData, 14, BrushTextDim));
        sp.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = BrushTextDim,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var btn = new Button
        {
            Content = sp,
            Background = BrushTransparent,
            Height = 32,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 2, 0),
            Cursor = Cursors.Hand,
        };
        btn.MouseEnter += (_, _) => btn.Background = BrushHover;
        btn.MouseLeave += (_, _) => btn.Background = BrushTransparent;
        return btn;
    }

    /// <summary>窗口控制按钮（44x30），hover 变背景。</summary>
    private static Button WinButton(string tip, UIElement? content, Brush hover)
    {
        var btn = new Button
        {
            Content = content,
            ToolTip = tip,
            Width = 44,
            Height = 30,
            Background = BrushTransparent,
            Cursor = Cursors.Hand,
        };
        btn.MouseEnter += (_, _) => btn.Background = hover;
        btn.MouseLeave += (_, _) => btn.Background = BrushTransparent;
        return btn;
    }

    private static Path FillIcon(string data, double size, Brush fill)
    {
        return new Path
        {
            Data = Geometry.Parse(data),
            Fill = fill,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
        };
    }

    private static Path StrokeIcon(string data, double size, Brush stroke)
    {
        return new Path
        {
            Data = Geometry.Parse(data),
            Stroke = stroke,
            StrokeThickness = 1.2,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
        };
    }

    // ==================== 无边框窗口：缩放 + 拖动 ====================

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _source = (HwndSource)HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            handled = true;
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            return HitTestNCA(x, y);
        }
        return IntPtr.Zero;
    }

    private nint HitTestNCA(int x, int y)
    {
        var pt = PointFromScreen(new Point(x, y));
        const double border = 8;

        bool left = pt.X < border;
        bool right = pt.X > ActualWidth - border;
        bool top = pt.Y < border;
        bool bottom = pt.Y > ActualHeight - border;

        if (top && left) return HTTOPLEFT;
        if (top && right) return HTTOPRIGHT;
        if (bottom && left) return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;

        // 标签条区域：命中标签/窗口按钮则交给控件，否则作为标题栏拖动
        if (pt.Y >= 0 && pt.Y < TabStripHeight)
        {
            var hit = InputHitTest(pt) as DependencyObject;
            if (hit != null && (IsInside(hit, _tabPanel) || IsInside(hit, _winBtns)))
                return HTCLIENT;
            return HTCAPTION;
        }

        return HTCLIENT;
    }

    private static bool IsInside(DependencyObject child, DependencyObject ancestor)
    {
        while (child != null)
        {
            if (child == ancestor)
                return true;
            child = VisualTreeHelper.GetParent(child);
        }
        return false;
    }

    private void OnMaximizeClick()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        var maximized = WindowState == WindowState.Maximized;
        _maxIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        _restoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        _maxBtn.ToolTip = maximized ? "还原" : "最大化";
    }

    // ==================== 多开标签 ====================

    private void AddAccount(Account account)
    {
        var existing = _tabs.FirstOrDefault(t => t.Account.Id == account.Id);
        if (existing != null)
        {
            ActivateTab(existing);
            return;
        }
        _ = StartTabAsync(account);
    }

    private async Task StartTabAsync(Account account)
    {
        var session = App.CurrentApp.Games.StartGame(
            account, new WindowInteropHelper(this).Handle,
            urlOverride: App.CurrentApp.Settings.AutoEnterGame ? null : ServerSelectUrl);
        if (session == null)
        {
            _placeholderText.Text = "启动失败，请确认 GameHost 已就位";
            _placeholder.Visibility = Visibility.Visible;
            return;
        }

        nint hwnd = 0;
        for (int i = 0; i < 60; i++)
        {
            hwnd = session.ReadWindowHandle();
            if (hwnd != 0)
                break;
            if (session.Process.HasExited)
                break;
            await Task.Delay(200);
        }

        if (hwnd == 0)
        {
            _placeholderText.Text = "等待游戏窗口超时";
            _placeholder.Visibility = Visibility.Visible;
            return;
        }

        account.Running = true;
        var host = new GameHostView { ChildWindowHandle = hwnd };
        _gameHostContainer.Children.Add(host);

        var tabItem = BuildTabItem(account, out var avatar, out var titleText, out var closeText);
        var tab = new SessionTab
        {
            Account = account,
            Session = session,
            Host = host,
            TabItem = tabItem,
            AvatarHost = avatar,
            TitleText = titleText,
            CloseText = closeText,
        };
        _tabPanel.Children.Add(tabItem);
        _tabs.Add(tab);
        ActivateTab(tab);
    }

    /// <summary>构建账号头像小圆（复用账号管理的头像设置：备注首字/QQ首数字/QQ头像）。</summary>
    private static Border BuildAvatar(Account account)
    {
        var avatarType = App.CurrentApp.Settings.AvatarDisplay;
        var avatar = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = BrushAccent,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
        };
        if (avatarType == AvatarType.QqAvatar && !string.IsNullOrEmpty(account.QQ))
        {
            var img = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill,
                Clip = new EllipseGeometry(new Point(10, 10), 10, 10),
            };
            img.Source = new QqAvatarConverter().Convert(
                new object[] { account, avatarType }, typeof(ImageSource),
                null!, CultureInfo.InvariantCulture) as ImageSource;
            avatar.Child = img;
        }
        else
        {
            var avatarChar = new AvatarCharDisplayConverter().Convert(
                new object[] { account, avatarType }, typeof(string),
                null!, CultureInfo.InvariantCulture) as string ?? "?";
            avatar.Child = new TextBlock
            {
                Text = avatarChar,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BrushWhite,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        return avatar;
    }

    /// <summary>构建自绘标签：头像 + 显示名 + 关闭按钮。</summary>
    private Border BuildTabItem(Account account, out Border avatarHost, out TextBlock titleText, out TextBlock closeText)
    {
        var avatar = BuildAvatar(account);
        avatarHost = avatar;

        titleText = new TextBlock
        {
            Text = account.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = BrushTextDim,
            FontSize = 13,
        };

        closeText = new TextBlock
        {
            Text = "✕",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        var close = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = BrushTransparent,
            Tag = account,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"关闭 {account.DisplayName} 的游戏窗口",
        };
        close.Child = closeText;
        close.MouseEnter += (_, _) => close.Background = BrushAccent;
        close.MouseLeave += (_, _) => close.Background = BrushTransparent;
        close.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            CloseTab(account.Id);
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(avatar);
        sp.Children.Add(titleText);
        sp.Children.Add(close);

        var item = new Border
        {
            Background = BrushTransparent,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 6, 4, 0),
            Tag = account,
            Child = sp,
        };
        item.MouseLeftButtonDown += OnTabMouseDown;
        return item;
    }

    private void OnTabMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;
        if (sender is Border { Tag: Account acc })
        {
            var tab = _tabs.FirstOrDefault(t => t.Account.Id == acc.Id);
            if (tab != null)
                ActivateTab(tab);
        }
    }

    private void ActivateTab(SessionTab tab)
    {
        foreach (var t in _tabs)
            t.Host.Visibility = t == tab ? Visibility.Visible : Visibility.Collapsed;
        _activeTab = tab;
        _placeholder.Visibility = Visibility.Collapsed;
        UpdateTabVisual(tab);
    }

    private void UpdateTabVisual(SessionTab active)
    {
        foreach (var t in _tabs)
        {
            bool selected = t == active;
            t.TabItem.Background = selected ? BrushAccent : BrushTransparent;
            t.TitleText.Foreground = selected ? BrushWhite : BrushTextDim;
            t.CloseText.Foreground = selected ? BrushWhite : BrushTextDim;
        }
    }

    private void CloseTab(long accountId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Account.Id == accountId);
        if (tab == null)
            return;
        App.CurrentApp.Games.StopGame(tab.Account);
        _tabPanel.Children.Remove(tab.TabItem);
        _gameHostContainer.Children.Remove(tab.Host);
        tab.Host.ChildWindowHandle = 0;
        _tabs.Remove(tab);

        if (_tabs.Count == 0)
        {
            _activeTab = null;
            _placeholderText.Text = "尚未启动游戏";
            _placeholder.Visibility = Visibility.Visible;
            return;
        }
        ActivateTab(_tabs[0]);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Closed -= OnWindowClosed;
        foreach (var t in _tabs.ToList())
        {
            App.CurrentApp.Games.StopGame(t.Account);
            t.Host.ChildWindowHandle = 0;
        }
        _tabs.Clear();
        _tabPanel.Children.Clear();
        _gameHostContainer.Children.Clear();
        Shared = null;
    }

    private void OnAvatarDisplayChanged()
    {
        foreach (var tab in _tabs)
        {
            tab.AvatarHost.Child = BuildAvatar(tab.Account);
        }
    }

    // ==================== 命令发送 ====================

    private void SendCommand(int cmd, nint wParam = 0)
    {
        var hwnd = _activeTab?.Session.ReadWindowHandle() ?? 0;
        if (hwnd == 0)
            return;
        PostMessage(hwnd, WM_APP + cmd, wParam, IntPtr.Zero);
    }

    private void OnToggleMute()
    {
        _muted = !_muted;
        UpdateMuteButton();
        SendCommand(CmdToggleMute);
    }

    /// <summary>静音按钮图标随状态切换。</summary>
    private void UpdateMuteButton()
    {
        _muteIcon.Visibility = _muted ? Visibility.Collapsed : Visibility.Visible;
        _muteIconMuted.Visibility = _muted ? Visibility.Visible : Visibility.Collapsed;
        _muteBtn.ToolTip = _muted ? "已静音" : "静音";
    }

    // ==================== 倍速 hover 菜单 ====================

    private void OnSpeedBtnMouseEnter(object sender, MouseEventArgs e)
    {
        _popupTimer.Stop();
        _speedPopup.PlacementTarget = _speedBtn;
        _speedPopup.IsOpen = true;
    }

    private void OnSpeedBtnMouseLeave(object sender, MouseEventArgs e)
    {
        StartPopupCloseTimer();
    }

    private void OnSpeedMenuPopupEnter(object sender, MouseEventArgs e)
    {
        _popupTimer.Stop();
    }

    private void OnSpeedMenuPopupLeave(object sender, MouseEventArgs e)
    {
        ClosePopupNow();
    }

    /// <summary>选择倍速：发送到 GameHost（wParam = 倍速×10）。</summary>
    private static void OnSpeedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true } rb &&
            rb.Tag is string s &&
            int.TryParse(s, out var speed10))
        {
            // 通过当前 GameWindow 实例发送（静态事件需转发）
            (Application.Current.Windows.OfType<GameWindow>().FirstOrDefault())
                ?.SendCommand(CmdToggleSpeed, (nint)speed10);
        }
    }

    // ==================== 账号切换 hover 菜单 ====================

    private void OnUserBtnMouseEnter(object sender, MouseEventArgs e)
    {
        _popupTimer.Stop();
        _userListPanel.Children.Clear();
        foreach (var acc in App.CurrentApp.Accounts.Accounts)
        {
            var b = new Button
            {
                Content = acc.DisplayName,
                Tag = acc,
                Background = BrushTransparent,
                Foreground = BrushText,
                Padding = new Thickness(10, 6, 10, 6),
                MinWidth = 170,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
            };
            b.MouseEnter += (_, _) => b.Background = BrushHover;
            b.MouseLeave += (_, _) => b.Background = BrushTransparent;
            b.Click += OnUserAccountClick;
            _userListPanel.Children.Add(b);
        }
        _userPopup.PlacementTarget = _userBtn;
        _userPopup.IsOpen = _userListPanel.Children.Count > 0;
    }

    private void OnUserBtnMouseLeave(object sender, MouseEventArgs e)
    {
        StartPopupCloseTimer();
    }

    private void OnUserMenuPopupEnter(object sender, MouseEventArgs e)
    {
        _popupTimer.Stop();
    }

    private void OnUserMenuPopupLeave(object sender, MouseEventArgs e)
    {
        ClosePopupNow();
    }

    /// <summary>点击账号：已启动则切换标签，未启动则启动该账号游戏。</summary>
    private void OnUserAccountClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Account acc })
        {
            var tab = _tabs.FirstOrDefault(t => t.Account.Id == acc.Id);
            if (tab != null)
                ActivateTab(tab);
            else
                AddAccount(acc);
            _userPopup.IsOpen = false;
        }
    }

    // ==================== 全屏 ====================

    private void OnToggleFullscreen()
    {
        if (!_isFullScreen)
        {
            _normalBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
            WindowState = WindowState.Normal;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            _isFullScreen = true;
        }
        else
        {
            WindowState = WindowState.Normal;
            Left = _normalBounds.X;
            Top = _normalBounds.Y;
            Width = _normalBounds.Width;
            Height = _normalBounds.Height;
            _isFullScreen = false;
        }
    }

    // ==================== hover 菜单关闭定时器 ====================

    private void StartPopupCloseTimer()
    {
        _popupTimer.Stop();
        _popupTimer.Start();
    }

    private void CloseHoverPopups()
    {
        _popupTimer.Stop();
        _userPopup.IsOpen = false;
        _speedPopup.IsOpen = false;
    }

    private void ClosePopupNow()
    {
        _popupTimer.Stop();
        _userPopup.IsOpen = false;
        _speedPopup.IsOpen = false;
    }
}