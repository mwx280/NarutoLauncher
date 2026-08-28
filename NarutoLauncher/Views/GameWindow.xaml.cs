using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.ComponentModel;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NarutoLauncher.Converters;
using NarutoLauncher.Models;
using NarutoLauncher.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Controls = System.Windows.Controls;
using System.Windows.Media;

namespace NarutoLauncher.Views;

/// <summary>
/// 多开游戏宿主窗口：顶部标签条 + 工具栏由 WebView2 渲染，
/// 游戏画面区由 HwndHost 内嵌。顶部 UI 通过 postMessage 与 C# 通信。
/// 支持同账号多开（多区），每个标签唯一 Key；"+" 号新建标签（登录后自动添加账号）。
/// </summary>
public partial class GameWindow : FluentWindow
{
    private sealed class SessionTab
    {
        // 唯一标签标识（沿用会话 Key，同账号多开时彼此区分）
        public required string Key { get; init; }
        public required Account Account { get; init; }
        public required GameSession Session { get; init; }
        public required GameHostView Host { get; init; }
    }

    public static GameWindow? Shared { get; private set; }

    /// <summary>是否已打开游戏标签（用于退出判定）。</summary>
    public bool HasTabs => _tabs.Count > 0;

    /// <summary>选区页：先进选区页让用户选区（可换区）。</summary>
    private const string ServerSelectUrl = "https://huoying.qq.com/server/website/";

    private readonly List<SessionTab> _tabs = new();
    // 各标签倍速（标签 Key → 倍速×10），默认 1x
    private readonly Dictionary<string, int> _speedByTab = new();
    private SessionTab? _activeTab;
    private bool _isFullScreen;
    private Rect _normalBounds;
    private bool _webUiReady;

    private const int WM_APP = 0x8000;
    private const int CmdRefresh = 1;
    private const int CmdSelectServer = 3;
    private const int CmdToggleMute = 4;
    private const int CmdToggleSpeed = 5;
    private const int CmdSetMute = 6;

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 2;

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    public static GameWindow GetShared(Window? owner)
    {
        var win = Shared;
        if (win == null)
        {
            win = new GameWindow();
            Shared = win;
            win.Show();
        }
        return win;
    }

    public static void OpenAccount(Account account, Window? owner)
    {
        var win = GetShared(owner);
        if (!win.IsVisible)
            win.Show();
        if (win.WindowState != WindowState.Maximized)
            win.WindowState = WindowState.Maximized;
        win.AddAccount(account);
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
        Closed += OnWindowClosed;
        StateChanged += OnWindowStateChanged;
        Loaded += OnLoaded;
        // 账号头像设置变化时，刷新所有标签头像
        App.CurrentApp.Settings.AvatarDisplayChanged += OnAvatarDisplayChanged;
    }

    // ==================== WebView2 顶部 UI ====================

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            WebUi.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, 0x20, 0x21, 0x24);
            await WebUi.EnsureCoreWebView2Async();
            var core = WebUi.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsBuiltInErrorPageEnabled = false;
            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigateToString(LoadUiHtml());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 初始化失败: {ex}");
        }
    }

    private static string LoadUiHtml()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream("NarutoMai.GameWindowUi.index.html")
                     ?? throw new InvalidOperationException("缺少顶部 UI 资源");
        using var r = new StreamReader(s);
        var html = r.ReadToEnd();
        html = html.Replace("__LOGO__", BuildLogoDataUri());
        return html;
    }

    private static string BuildLogoDataUri()
    {
        var sri = Application.GetResourceStream(new Uri("pack://application:,,,/assets/favicon.png"));
        if (sri == null)
            return string.Empty;
        using var ms = new MemoryStream();
        sri.Stream.CopyTo(ms);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    private void Eval(string js)
    {
        try
        {
            if (WebUi.CoreWebView2 != null)
                _ = WebUi.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch
        {
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var cmd = root.GetProperty("cmd").GetString();
            switch (cmd)
            {
                case "ui-ready":
                    _webUiReady = true;
                    SyncAll();
                    break;
                case "tab-select":
                    var selKey = root.GetProperty("id").GetString();
                    if (selKey != null)
                        SelectTabByKey(selKey);
                    break;
                case "tab-close":
                    var closeKey = root.GetProperty("id").GetString();
                    if (closeKey != null)
                        CloseTab(closeKey);
                    break;
                case "add-account":
                    OnAddAccount();
                    break;
                case "win-min":
                    WindowState = WindowState.Minimized;
                    break;
                case "win-max":
                    OnMaximizeClick();
                    break;
                case "win-close":
                    Close();
                    break;
                case "win-drag":
                    BeginWindowDrag();
                    break;
                case "cmd-refresh":
                    SendCommand(CmdRefresh);
                    break;
                case "cmd-server":
                    SendCommand(CmdSelectServer);
                    break;
                case "cmd-mute":
                    OnToggleMute();
                    break;
                case "cmd-speed":
                    SendCommand(CmdToggleSpeed, (nint)root.GetProperty("v").GetInt32());
                    break;
                case "cmd-fullscreen":
                    OnToggleFullscreen();
                    break;
                case "menu-speed":
                    OpenSpeedMenu(root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble());
                    break;
                case "menu-users":
                    OpenUserMenu(root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble());
                    break;
                case "user-select":
                    OnUserSelect(root.GetProperty("id").GetInt64());
                    break;
            }
        }
        catch
        {
            // 忽略异常消息
        }
    }

    private void SyncAll()
    {
        SyncTabs();
        SyncMuteState();
        Eval($"__setMaximized({(WindowState == WindowState.Maximized ? "true" : "false")})");
    }

    /// <summary>把当前标签同步到 HTML（每个标签用唯一 Key 作为 id）。</summary>
    private void SyncTabs()
    {
        var arr = _tabs.Select(t => new
        {
            id = t.Key,
            name = t.Account.DisplayName,
            avatar = AvatarChar(t.Account),
            avatarUrl = BuildAvatarUrl(t.Account),
            active = t == _activeTab,
        }).ToList();
        Eval($"__setTabs({JsonSerializer.Serialize(arr)})");
    }

    private static string AvatarChar(Account account)
    {
        var avatarType = App.CurrentApp.Settings.AvatarDisplay;
        var c = new AvatarCharDisplayConverter().Convert(
            new object[] { account, avatarType }, typeof(string),
            null!, CultureInfo.InvariantCulture) as string ?? "?";
        return c.Length > 1 ? c[..1] : c;
    }

    private static string? BuildAvatarUrl(Account account)
    {
        if (App.CurrentApp.Settings.AvatarDisplay == AvatarType.QqAvatar &&
            !string.IsNullOrEmpty(account.QQ))
            return $"https://q.qlogo.cn/g?b=qq&nk={account.QQ}&s=100";
        return null;
    }

    private void BeginWindowDrag()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        ReleaseCapture();
        SendMessage(hwnd, WM_NCLBUTTONDOWN, (nint)HTCAPTION, 0);
    }

    private void OnMaximizeClick()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private bool _wasMinimized;

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_webUiReady || WebUi.CoreWebView2 == null)
            return;
        Eval($"__setMaximized({(WindowState == WindowState.Maximized ? "true" : "false")})");
        // 从最小化还原：重新导航页面，避免渲染白屏
        var isMin = WindowState == WindowState.Minimized;
        if (_wasMinimized && !isMin)
            WebUi.CoreWebView2.NavigateToString(LoadUiHtml());
        _wasMinimized = isMin;
    }

    // ==================== 弹出菜单（账号 / 倍速） ====================

    private void OpenUserMenu(double x, double y)
    {
        var list = App.CurrentApp.Accounts.Accounts.ToList();
        if (list.Count == 0)
            return;
        var style = (Style)FindResource("UserItemButtonStyle");
        UserListPanel.Children.Clear();
        foreach (var acc in list)
        {
            var tab = _tabs.FirstOrDefault(t => t.Account.Id == acc.Id);
            var active = _activeTab?.Account.Id == acc.Id;
            var avatar = BuildAvatar(acc);

            var nameText = new Controls.TextBlock
            {
                Text = acc.DisplayName,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = active
                    ? new SolidColorBrush(Color.FromRgb(0x6C, 0xB5, 0xFF))
                    : new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xED)),
            };
            var statusText = new Controls.TextBlock
            {
                Text = tab != null ? "运行中" : "未运行",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = tab != null
                    ? new SolidColorBrush(Color.FromRgb(0x5F, 0xD0, 0x68))
                    : new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6)),
            };
            var qqText = new Controls.TextBlock
            {
                Text = string.IsNullOrEmpty(acc.QQ) ? "" : $" · QQ {acc.QQ}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB4, 0xBA)),
            };
            var subRow = new Controls.StackPanel
            {
                Orientation = Controls.Orientation.Horizontal,
                Margin = new Thickness(0, 1, 0, 0),
            };
            subRow.Children.Add(statusText);
            subRow.Children.Add(qqText);

            var info = new Controls.StackPanel
            {
                Orientation = Controls.Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
            };
            info.Children.Add(nameText);
            info.Children.Add(subRow);

            var sp = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
            sp.Children.Add(avatar);
            sp.Children.Add(info);

            var btn = new Controls.Button
            {
                Content = sp,
                Tag = acc,
                Style = style,
                Margin = new Thickness(0, 1, 0, 1),
            };
            btn.Click += OnUserAccountClick;
            UserListPanel.Children.Add(btn);
        }
        UserMenuPopup.HorizontalOffset = x;
        UserMenuPopup.VerticalOffset = y;
        UserMenuPopup.IsOpen = true;
    }

    private static Controls.Border BuildAvatar(Account account)
    {
        var avatarType = App.CurrentApp.Settings.AvatarDisplay;
        var avatar = new Controls.Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x6C, 0xBD)),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
        };
        if (avatarType == AvatarType.QqAvatar && !string.IsNullOrEmpty(account.QQ))
        {
            var img = new Controls.Image
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
            var avatarChar = AvatarChar(account);
            avatar.Child = new Controls.TextBlock
            {
                Text = avatarChar,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        return avatar;
    }

    /// <summary>打开倍速菜单（点击「倍速」按钮）：按当前标签设置选中项。</summary>
    private void OpenSpeedMenu(double x, double y)
    {
        var speed10 = GetCurrentSpeed10();
        foreach (var rb in new[] { Speed05, Speed1, Speed2, Speed4 })
        {
            rb.IsChecked = rb.Tag is string s && int.TryParse(s, out var v) && v == speed10;
        }
        // 预设项都不匹配当前倍速时，选中「自定义」并回填数值
        var matched = new[] { Speed05, Speed1, Speed2, Speed4 }.Any(
            rb => rb.Tag is string s && int.TryParse(s, out var v) && v == speed10);
        if (!matched)
        {
            SpeedCustom.IsChecked = true;
            CustomSpeedBox.Text = (speed10 / 10.0).ToString("0.#", CultureInfo.InvariantCulture);
        }
        SpeedMenuPopup.HorizontalOffset = x;
        SpeedMenuPopup.VerticalOffset = y;
        SpeedMenuPopup.IsOpen = true;
    }

    /// <summary>当前激活标签的倍速（×10），默认 1x。</summary>
    private int GetCurrentSpeed10()
    {
        var key = _activeTab?.Key ?? "";
        return _speedByTab.TryGetValue(key, out var s) ? s : 10;
    }

    private void SyncSpeedText()
    {
        var speed10 = GetCurrentSpeed10();
        double v = speed10 / 10.0;
        var text = v == Math.Floor(v) ? $"{(int)v}x" : $"{v.ToString("0.#", CultureInfo.InvariantCulture)}x";
        Eval($"__setSpeedText('{text}')");
    }

    private void OnUserAccountClick(object sender, RoutedEventArgs e)
    {
        if (sender is Controls.Button { Tag: Account acc })
        {
            // 同账号可多开：点击账号菜单始终新开一个标签
            AddAccount(acc);
            UserMenuPopup.IsOpen = false;
        }
    }

    /// <summary>选择倍速：记录当前标签倍速，发送到 GameHost（wParam = 倍速×10）。</summary>
    private void OnSpeedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Controls.RadioButton { IsChecked: true } rb)
        {
            if (rb.Tag is string s && s == "custom")
            {
                SendCustomSpeed();
                return;
            }
            if (rb.Tag is string tag && int.TryParse(tag, out var speed10))
            {
                SetSpeed(speed10);
            }
        }
    }

    /// <summary>自定义倍速输入框回车：立即发送。</summary>
    private void OnCustomSpeedKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        SendCustomSpeed();
        e.Handled = true;
    }

    /// <summary>自定义倍速框按下：保持焦点，防止 Popup 因点击选中倍速而关闭输入。</summary>
    private void OnCustomSpeedBoxPreviewMouseDown(object sender, RoutedEventArgs e)
    {
        CustomSpeedBox.Focus();
        e.Handled = true;
    }

    /// <summary>倍速菜单移出鼠标但输入框有焦点时保持打开，否则关闭。</summary>
    private void OnSpeedMenuMouseLeave(object sender, RoutedEventArgs e)
    {
        // 若输入框正获焦，延迟关闭，避免失去焦点时输入被中断
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!CustomSpeedBox.IsKeyboardFocused)
                SpeedMenuPopup.IsOpen = false;
        });
    }

    /// <summary>解析自定义倍速并发送（×10，关闭菜单）。</summary>
    private void SendCustomSpeed()
    {
        var text = CustomSpeedBox.Text.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var v) && v is >= 0.1 and <= 100)
        {
            SetSpeed((int)Math.Round(v * 10));
        }
        else
        {
            CustomSpeedBox.Text = "";
        }
    }

    private void SetSpeed(int speed10)
    {
        if (_activeTab != null)
            _speedByTab[_activeTab.Key] = speed10;
        SendCommand(CmdToggleSpeed, (nint)speed10);
        SpeedMenuPopup.IsOpen = false;
        SyncSpeedText();
    }

    // ==================== 多开标签 ====================

    private void AddAccount(Account account)
    {
        // 同账号可开多个标签（多区多开）：每次调用都新开一个游戏会话
        _ = StartTabAsync(account);
    }

    private async Task StartTabAsync(Account account)
    {
        var session = App.CurrentApp.Games.StartGame(
            account, new WindowInteropHelper(this).Handle,
            urlOverride: App.CurrentApp.Settings.AutoEnterGame ? null : ServerSelectUrl);
        if (session == null)
        {
            PlaceholderText.Text = "启动失败，请确认 GameHost 已就位";
            Placeholder.Visibility = Visibility.Visible;
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
            PlaceholderText.Text = "等待游戏窗口超时";
            Placeholder.Visibility = Visibility.Visible;
            return;
        }

        account.Running = true;
        var host = new GameHostView { ChildWindowHandle = hwnd };
        GameHostContainer.Children.Add(host);

        var tab = new SessionTab
        {
            Key = session.Key,
            Account = account,
            Session = session,
            Host = host,
        };
        _tabs.Add(tab);
        if (account.IsMuted)
        {
            var gh = session.ReadWindowHandle();
            if (gh != 0)
                PostMessage(gh, WM_APP + CmdSetMute, (nint)1, IntPtr.Zero);
        }
        ActivateTab(tab);
    }

    private void SelectTabByKey(string key)
    {
        var tab = _tabs.FirstOrDefault(t => t.Key == key);
        if (tab != null)
            ActivateTab(tab);
    }

    private void ActivateTab(SessionTab tab)
    {
        foreach (var t in _tabs)
            t.Host.Visibility = t == tab ? Visibility.Visible : Visibility.Collapsed;
        _activeTab = tab;
        Placeholder.Visibility = Visibility.Collapsed;
        SyncTabs();
        SyncSpeedText();
        SyncMuteState();
    }

    private void CloseTab(string key)
    {
        var tab = _tabs.FirstOrDefault(t => t.Key == key);
        if (tab == null)
            return;
        // 只关闭这个会话（同账号多开时不影响其它区）
        App.CurrentApp.Games.StopSessionByKey(key);
        _speedByTab.Remove(key);
        GameHostContainer.Children.Remove(tab.Host);
        tab.Host.ChildWindowHandle = 0;
        _tabs.Remove(tab);

        if (_tabs.Count == 0)
        {
            _activeTab = null;
            PlaceholderText.Text = "尚未启动游戏";
            Placeholder.Visibility = Visibility.Visible;
        }
        else
        {
            ActivateTab(_tabs[0]);
        }
        SyncTabs();
    }

    /// <summary>新建标签（「+」号）：打开添加账号窗口，登录后自动添加并开标签。</summary>
    private void OnAddAccount()
    {
        var win = new AddAccountWindow
        {
            Owner = null,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = true,
        };
        if (win.ShowDialog() == true && win.Result is { } account)
        {
            AddAccount(account);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Closed -= OnWindowClosed;
        try
        {
            foreach (var t in _tabs.ToList())
            {
                App.CurrentApp.Games.StopSessionByKey(t.Key);
                t.Host.ChildWindowHandle = 0;
            }
            _tabs.Clear();
            GameHostContainer.Children.Clear();
            Shared = null;
        }
        catch
        {
        }
        // 关闭游戏窗口后，按设置决定是否显示启动器主界面
        if (App.CurrentApp.Settings.ShowMainOnGameClose &&
            App.CurrentApp.MainWindow is { } main)
        {
            try
            {
                if (!main.IsVisible)
                    main.Show();
                main.WindowState = WindowState.Normal;
                main.Activate();
            }
            catch
            {
            }
        }
        App.CurrentApp.TryExitWhenNoGame();
    }

    private void OnAvatarDisplayChanged()
    {
        SyncTabs();
    }

    private void OnUserSelect(long accountId)
    {
        var account = App.CurrentApp.Accounts.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (account == null)
            return;
        // 同账号可多开：点击账号选择始终新开一个标签
        AddAccount(account);
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
        if (_activeTab == null)
            return;
        var acc = _activeTab.Account;
        acc.IsMuted = !acc.IsMuted;
        SendCommand(CmdSetMute, acc.IsMuted ? 1 : 0);
        SyncMuteState();
    }

    private void SyncMuteState()
    {
        var muted = _activeTab?.Account.IsMuted ?? false;
        Eval($"__setMuted({(muted ? "true" : "false")})");
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
}
