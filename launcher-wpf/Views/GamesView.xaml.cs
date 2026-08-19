using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Models;
using NarutoLauncher.Services;

namespace NarutoLauncher.Views;

public partial class GamesView : UserControl
{
    private readonly ObservableCollection<GameTab> _tabs = new();
    private readonly Dictionary<long, GameTab> _tabByAccount = new();

    public GamesView()
    {
        InitializeComponent();
        LoadAccounts();
        TabControl.ItemsSource = _tabs;
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
    private GameTab? ActiveTab => TabControl.SelectedItem as GameTab;

    private GameTab CreateTab(Account account)
    {
        var panel = new StackPanel();
        var host = new GameHostView { Visibility = Visibility.Collapsed };
        var placeholder = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        placeholder.Children.Add(new TextBlock
        {
            Text = "游戏窗口将显示在这里",
            FontSize = 15,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 153, 153, 153)),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        panel.Children.Add(host);
        panel.Children.Add(placeholder);

        var tab = new GameTab
        {
            Account = account,
            HostView = host,
            Panel = panel,
            Placeholder = placeholder,
        };

        _tabs.Add(tab);
        _tabByAccount[account.Id] = tab;
        TabControl.SelectedIndex = _tabs.Count - 1;
        UpdateRunningIndicators();
        return tab;
    }

    /// <summary>当前选中标签页的占位符（供外部引用）。</summary>
    internal StackPanel? Placeholder => ActiveTab?.Placeholder;

    private void CloseTab(long accountId)
    {
        if (!_tabByAccount.TryGetValue(accountId, out var tab)) return;
        StopTabSession(tab);
        _tabs.Remove(tab);
        _tabByAccount.Remove(accountId);
        if (_tabs.Count > 0)
        {
            int idx = Math.Min(TabControl.SelectedIndex, _tabs.Count - 1);
            if (idx < 0) idx = 0;
            TabControl.SelectedIndex = idx;
        }
        else
        {
            TabControl.SelectedIndex = -1;
        }
        UpdateRunningIndicators();
    }

    private async void OnStartGame(object sender, RoutedEventArgs e)
    {
        var acc = SelectedAccount;
        if (acc == null)
        {
            MessageBox.Show("请先选择账号", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_tabByAccount.TryGetValue(acc.Id, out var existing) && existing.Session != null && !existing.Session.Process.HasExited)
        {
            MessageBox.Show("该账号游戏已在运行中，请先停止", "提示");
            return;
        }

        var session = App.CurrentApp.Games.StartGame(acc);
        if (session == null)
        {
            MessageBox.Show("启动失败，请确认 GameHost 已就位", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        nint hwnd = 0;
        for (int i = 0; i < 60; i++)
        {
            hwnd = session.ReadWindowHandle();
            if (hwnd != 0) break;
            if (session.Process.HasExited) break;
            await Task.Delay(200);
        }

        if (hwnd == 0)
        {
            MessageBox.Show("等待游戏窗口超时", "错误");
            return;
        }

        acc.Running = true;

        GameTab tab;
        if (_tabByAccount.TryGetValue(acc.Id, out existing))
        {
            tab = existing;
        }
        else
        {
            tab = CreateTab(acc);
        }

        tab.Session = new SessionInfo
        {
            Process = session.Process,
            UserdataDir = session.UserdataDir,
        };
        if (tab.HostView != null)
            tab.HostView.ChildWindowHandle = hwnd;
        if (tab.Placeholder != null)
            tab.Placeholder.Visibility = Visibility.Collapsed;

        UpdateRunningIndicators();
    }

    private void OnStopGame(object sender, RoutedEventArgs e)
    {
        var tab = ActiveTab;
        if (tab == null) return;
        StopTabSession(tab);
        UpdateRunningIndicators();
    }

    private void StopTabSession(GameTab tab)
    {
        if (tab.Session != null && !tab.Session.Process.HasExited)
            App.CurrentApp.Games.StopGame(tab.Account);
        tab.Session = null;
        if (tab.HostView != null)
            tab.HostView.ChildWindowHandle = 0;
        tab.Account.Running = false;
        if (tab.Placeholder != null)
            tab.Placeholder.Visibility = Visibility.Visible;
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        // ContentArea.Content is managed per-tab via Panel reference
    }

    private void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn) return;
        // 在模板内，需向上找父级 TabItem 的 Tag
        var tab = btn.TemplatedParent as TabItem;
        if (tab == null) return;
        if (tab.Tag is long id)
            CloseTab(id);
    }

    private void UpdateRunningIndicators()
    {
        foreach (var tab in _tabs)
            tab.Account.Running = tab.IsRunning;
    }
}
