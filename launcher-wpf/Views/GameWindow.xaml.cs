using System.Windows;
using NarutoLauncher.Models;
using Wpf.Ui.Controls;

namespace NarutoLauncher.Views;

/// <summary>
/// 账号对应的无边框游戏窗口（带全屏/最小化/最大化/关闭）。
/// </summary>
public partial class GameWindow : FluentWindow
{
    private bool _isFullScreen;
    private Rect _normalBounds;

    public GameWindow(Account account)
    {
        InitializeComponent();
        Title = $"火影忍者OL - {account.DisplayName}";
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
            if (FullscreenBtn.Content is SymbolIcon icon)
                icon.Symbol = SymbolRegular.FullScreenMinimize20;
        }
        else
        {
            WindowState = WindowState.Normal;
            Left = _normalBounds.X;
            Top = _normalBounds.Y;
            Width = _normalBounds.Width;
            Height = _normalBounds.Height;
            _isFullScreen = false;
            if (FullscreenBtn.Content is SymbolIcon icon)
                icon.Symbol = SymbolRegular.FullScreenMaximize20;
        }
    }
}