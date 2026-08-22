using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Controls = System.Windows.Controls;
using NarutoLauncher.Converters;
using NarutoLauncher.Models;
using NarutoLauncher.Services;
using Wpf.Ui.Controls;

namespace NarutoLauncher.Views;

/// <summary>
/// 多开游戏宿主窗口：顶部标签栏按账号备注列出所有运行中的游戏会话，
/// 点击标签切换显示对应游戏，标签右侧小 ✕ 关闭对应游戏窗口。
/// 全局共享单例（AccountsView 添加账号时复用本窗口，不再每账号开一个窗口）。
/// </summary>
public partial class GameWindow : FluentWindow
{
    private sealed class SessionTab
    {
        public required Account Account { get; init; }
        public required GameSession Session { get; init; }
        public required GameHostView Host { get; init; }
        public required TabViewItem TabItem { get; init; }
        public required Controls.Border AvatarHost { get; init; }
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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

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
    }

    private GameWindow()
    {
        InitializeComponent();
        Closed += OnWindowClosed;
        // 账号头像设置变化时，刷新所有标签头像
        App.CurrentApp.Settings.AvatarDisplayChanged += OnAvatarDisplayChanged;
    }

    private void OnAvatarDisplayChanged()
    {
        foreach (var tab in _tabs)
            RefreshAvatar(tab);
    }

    /// <summary>按当前头像设置重建标签头像内容。</summary>
    private static void RefreshAvatar(SessionTab tab)
    {
        tab.AvatarHost.Child = BuildAvatar(tab.Account);
    }

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

        var (tabItem, avatar) = BuildTabItem(account);
        var tab = new SessionTab
        {
            Account = account,
            Session = session,
            Host = host,
            TabItem = tabItem,
            AvatarHost = avatar,
        };
        TabBar.Items.Add(tabItem);
        _tabs.Add(tab);
        ActivateTab(tab);
    }

    /// <summary>构建账号头像小圆（复用账号管理的头像设置：备注首字/QQ首数字/QQ头像）。</summary>
    private static Controls.Border BuildAvatar(Account account)
    {
        var avatarType = App.CurrentApp.Settings.AvatarDisplay;
        var avatar = new Controls.Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = App.CurrentApp.Resources["AccentFillColorDefaultBrush"] as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0xE8, 0x48, 0x2C)),
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
            var avatarChar = new AvatarCharDisplayConverter().Convert(
                new object[] { account, avatarType }, typeof(string),
                null!, CultureInfo.InvariantCulture) as string ?? "?";
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

    private (TabViewItem Item, Controls.Border Avatar) BuildTabItem(Account account)
    {
        var avatar = BuildAvatar(account);

        var text = new Controls.TextBlock
        {
            Text = account.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        // 关闭按钮：圆形，hover 显示红色小圆圈
        var close = new Controls.Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Tag = account,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"关闭 {account.DisplayName} 的游戏窗口",
        };
        var closeText = new Controls.TextBlock
        {
            Text = "✕",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        close.Child = closeText;
        close.MouseEnter += (_, _) =>
            close.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0xE8, 0x48, 0x2C));
        close.MouseLeave += (_, _) => close.Background = Brushes.Transparent;
        close.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            CloseTab(account.Id);
        };

        var sp = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        sp.Children.Add(avatar);
        sp.Children.Add(text);
        sp.Children.Add(close);

        // 标题与关闭按钮颜色跟随标签选中状态（绑定 TabViewItem.Foreground）
        var fgBinding = new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,
                                                typeof(TabViewItem), 1),
        };
        text.SetBinding(Controls.TextBlock.ForegroundProperty, fgBinding);
        closeText.SetBinding(Controls.TextBlock.ForegroundProperty, fgBinding);

        var item = new TabViewItem
        {
            Header = sp,
            Content = null,
            Tag = account,
            Padding = new Thickness(8, 2, 8, 2),
        };
        return (item, avatar);
    }

    private void OnTabSelectionChanged(object sender, Controls.SelectionChangedEventArgs e)
    {
        if (TabBar.SelectedItem is TabViewItem { Tag: Account acc })
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
        Placeholder.Visibility = Visibility.Collapsed;
        if (tab.TabItem != null)
            TabBar.SelectedItem = tab.TabItem;
    }

    private void CloseTab(long accountId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Account.Id == accountId);
        if (tab == null)
            return;
        App.CurrentApp.Games.StopGame(tab.Account);
        TabBar.Items.Remove(tab.TabItem);
        GameHostContainer.Children.Remove(tab.Host);
        tab.Host.ChildWindowHandle = 0;
        _tabs.Remove(tab);

        if (_tabs.Count == 0)
        {
            _activeTab = null;
            PlaceholderText.Text = "尚未启动游戏";
            Placeholder.Visibility = Visibility.Visible;
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
        TabBar.Items.Clear();
        GameHostContainer.Children.Clear();
        Shared = null;
    }

    private void SendCommand(int cmd, nint wParam = 0)
    {
        var hwnd = _activeTab?.Session.ReadWindowHandle() ?? 0;
        if (hwnd == 0)
            return;
        PostMessage(hwnd, WM_APP + cmd, wParam, IntPtr.Zero);
    }

    private void OnRefreshGame(object sender, RoutedEventArgs e)
    {
        SendCommand(CmdRefresh);
    }

    private void OnToggleFullscreen(object sender, RoutedEventArgs e)
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
            FullscreenBtn.ToolTip = "退出全屏";
        }
        else
        {
            WindowState = WindowState.Normal;
            Left = _normalBounds.X;
            Top = _normalBounds.Y;
            Width = _normalBounds.Width;
            Height = _normalBounds.Height;
            _isFullScreen = false;
            FullscreenBtn.ToolTip = "全屏";
        }
    }
}