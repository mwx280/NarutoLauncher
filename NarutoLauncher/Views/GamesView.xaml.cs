using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Models;
using NarutoLauncher.Services;

namespace NarutoLauncher.Views;

public partial class GamesView : UserControl
{
    private GameSession? _session;

    public GamesView()
    {
        InitializeComponent();
        LoadAccounts();
    }

    private void LoadAccounts()
    {
        var accounts = App.CurrentApp.Accounts.Accounts;
        AccountSelector.ItemsSource = accounts;
        AccountSelector.DisplayMemberPath = "DisplayName";
        if (accounts.Count > 0)
            AccountSelector.SelectedIndex = 0;
    }

    private Account? SelectedAccount => AccountSelector.SelectedItem as Account;

    private async void OnStartGame(object sender, RoutedEventArgs e)
    {
        var acc = SelectedAccount;
        if (acc == null)
        {
            MessageBox.Show("请先选择账号", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_session != null && !_session.Process.HasExited)
        {
            MessageBox.Show("已有游戏运行中，请先停止", "提示");
            return;
        }

        var session = App.CurrentApp.Games.StartGame(acc);
        if (session == null)
        {
            MessageBox.Show("启动失败，请确认 GameHost 已就位", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("等待游戏窗口超时", "错误");
            return;
        }

        acc.Running = true;

        // 通过 HwndHost 内嵌
        GameHost.ChildWindowHandle = hwnd;
        Placeholder.Visibility = Visibility.Collapsed;
        GameHost.Visibility = Visibility.Visible;
    }

    private void OnStopGame(object sender, RoutedEventArgs e)
    {
        if (_session != null && SelectedAccount != null)
        {
            App.CurrentApp.Games.StopGame(SelectedAccount);
        }
        _session = null;
        GameHost.ChildWindowHandle = 0;
        GameHost.Visibility = Visibility.Collapsed;
        Placeholder.Visibility = Visibility.Visible;
    }
}
