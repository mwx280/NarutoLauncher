using System.Windows;
using System.Windows.Controls;
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
    private bool _qualityInitDone;

    // 与 GameHost 主窗口通信的自定义消息（WM_APP + n，n=1 刷新，n=2 画质调节）
    private const int WM_APP = 0x8000;
    private const int CmdRefresh = 1;
    private const int CmdQuality = 2;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

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

        // 画质下拉同步当前设置（低/中/高），并标记初始化完成
        QualitySelector.SelectedIndex = App.CurrentApp.Settings.FlashQuality switch
        {
            "high" => 2,
            "medium" => 1,
            _ => 0,
        };
        _qualityInitDone = true;
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

    /// <summary>发送自定义命令消息给 GameHost 主窗口（无会话则忽略）。</summary>
    private void SendCommand(int cmd, nint wParam = 0)
    {
        var hwnd = _session?.ReadWindowHandle() ?? 0;
        if (hwnd == 0)
            return;
        PostMessage(hwnd, WM_APP + cmd, wParam, IntPtr.Zero);
    }

    private void OnRefreshGame(object sender, RoutedEventArgs e)
    {
        SendCommand(CmdRefresh);
    }

    private void OnQualityChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_qualityInitDone)
            return;  // 窗口加载初始化的默认选中不触发重启
        if (QualitySelector.SelectedItem is not ComboBoxItem item ||
            !int.TryParse(item.Tag?.ToString(), out var level))
            return;
        // 画质只在新 GameHost 进程（Flash 实例创建）时生效，切换需重启游戏。
        _ = RestartGameAsync(level);
    }

    /// <summary>以指定画质档位重启游戏（停止旧进程，用新 --flash-quality 重新拉起）。</summary>
    private async Task RestartGameAsync(int level)
    {
        var quality = level switch
        {
            2 => "high",
            1 => "medium",
            _ => "low",
        };
        App.CurrentApp.Settings.FlashQuality = quality;

        // 停止当前会话
        if (_session != null)
        {
            App.CurrentApp.Games.StopGame(_account);
            _session = null;
        }

        // 清空内嵌区域，显示加载占位
        GameHost.ChildWindowHandle = 0;
        GameHost.Visibility = Visibility.Collapsed;
        Placeholder.Visibility = Visibility.Visible;
        PlaceholderText.Text = "正在切换画质…";

        // 重新启动（传新 quality）
        var session = App.CurrentApp.Games.StartGame(_account,
            new WindowInteropHelper(this).Handle, quality);
        if (session == null)
        {
            PlaceholderText.Text = "启动失败，请确认 GameHost 已就位";
            return;
        }
        _session = session;

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

        GameHost.ChildWindowHandle = hwnd;
        Placeholder.Visibility = Visibility.Collapsed;
        GameHost.Visibility = Visibility.Visible;
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