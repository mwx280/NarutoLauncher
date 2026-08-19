using System.Windows;
using NarutoLauncher.Models;
using Wpf.Ui.Controls;

namespace NarutoLauncher.Views;

/// <summary>
/// 账号对应的空白游戏窗口（带最小化/最大化/关闭）。
/// </summary>
public partial class GameWindow : FluentWindow
{
    public GameWindow(Account account)
    {
        InitializeComponent();
        Title = $"火影忍者OL - {account.DisplayName}";
        var tb = FindName("TitleBarHost") as TitleBar;
        if (tb != null)
            tb.Title = Title;
    }
}