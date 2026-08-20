using System.Windows;
using System.Windows.Interop;
using NarutoLauncher.Models;
using NarutoLauncher.Services;
using Wpf.Ui.Controls;

namespace NarutoLauncher.Views;

/// <summary>
/// 账号对应的无边框游戏窗口（带全屏/最小化/最大化/关闭）。
/// 窗口显示后自动拉起 CEF GameHost，并把游戏窗口内嵌到内容区。
/// </summary>
public partial class GameWindow : FluentWindow
{
    private readonly Account _account;
    private GameSession? _session;
    private bool _isFullScreen;
    private Rect _normalBounds;

    public GameWindow(Account account)
    {
        InitializeComponent();
        _account = account;
        Title = $"火影忍者OL - {account.DisplayName}";
        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnWindowLoaded;
        var session = App.CurrentApp.Games.StartGame(_account, new WindowInteropHelper(this).Handle);
        if (session == null)
        {
            PlaceholderText.Text = "启动失败，请确认 GameHost 已就位";
            return;
        }
        _session = session;

        // 等待 GameHost 窗口句柄
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
            return;
        }

        _account.Running = true;

        // 通过 HwndHost 内嵌
        GameHost.ChildWindowHandle = hwnd;
        Placeholder.Visibility = Visibility.Collapsed;
        GameHost.Visibility = Visibility.Visible;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Closed -= OnWindowClosed;
        if (_session != null)
        {
            App.CurrentApp.Games.StopGame(_account);
            _session = null;
        }
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