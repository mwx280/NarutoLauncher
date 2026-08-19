using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NarutoLauncher.Models;
using NarutoLauncher.ViewModels;

namespace NarutoLauncher.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; } = new();

    public HomePage()
    {
        InitializeComponent();
    }

    private void OnStartGame(object sender, RoutedEventArgs e)
    {
        var accounts = ViewModel.Accounts.Where(a => a.LoggedIn).ToList();
        if (accounts.Count == 0)
        {
            ShowTip("暂无可登录账号，请先在账号管理中登录", InfoBarSeverity.Warning);
            return;
        }
        var started = 0;
        foreach (var acc in accounts)
        {
            if (!acc.Running && App.CurrentApp.Games.StartGame(acc) != null)
                started++;
        }
        ShowTip(started > 0
            ? $"已启动 {started} 个游戏窗口"
            : "启动失败，请确认 GameHost 已就位", InfoBarSeverity.Success);
    }

    private void OnToggleRun(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is long id)
        {
            var acc = App.CurrentApp.Accounts.Accounts.FirstOrDefault(a => a.Id == id);
            if (acc == null) return;
            if (acc.Running)
            {
                App.CurrentApp.Games.StopGame(acc);
            }
            else
            {
                var proc = App.CurrentApp.Games.StartGame(acc);
                if (proc == null)
                    ShowTip("启动失败，请确认 GameHost 已就位", InfoBarSeverity.Error);
            }
        }
    }

    private void OnAccountClick(object sender, ItemClickEventArgs e)
    {
        // 账号卡片点击（预留）
    }

    private void ShowTip(string message, InfoBarSeverity severity)
    {
        if (FindName("TipInfoBar") is InfoBar bar)
        {
            bar.Severity = severity;
            bar.Message = message;
            bar.IsOpen = true;
        }
    }
}
