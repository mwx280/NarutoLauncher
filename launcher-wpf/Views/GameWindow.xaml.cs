using System.Windows;
using NarutoLauncher.Models;
using Wpf.Ui.Controls;

namespace NarutoLauncher.Views;

/// <summary>
/// 账号对应的空白游戏窗口（带置顶/最小化/最大化/关闭）。
/// </summary>
public partial class GameWindow : FluentWindow
{
    private readonly Account _account;

    public GameWindow(Account account)
    {
        InitializeComponent();
        _account = account;
        Title = $"火影忍者OL - {account.DisplayName}";
        var tb = FindName("TitleBarHost") as TitleBar;
        if (tb != null)
            tb.Title = Title;
    }

    private void OnToggleTopmost(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinBtn.Appearance = Topmost ? ControlAppearance.Primary : ControlAppearance.Transparent;
    }
}