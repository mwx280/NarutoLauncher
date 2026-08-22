using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Models;
using NarutoLauncher.Services;

namespace NarutoLauncher.Views;

public partial class AccountsView : UserControl
{
    // 全局头像显示类型（依赖属性，支持 DataTemplate 内绑定）
    public static readonly DependencyProperty AvatarDisplayProperty =
        DependencyProperty.Register(nameof(AvatarDisplay), typeof(AvatarType),
            typeof(AccountsView), new PropertyMetadata(AvatarType.NameChar));

    public AvatarType AvatarDisplay
    {
        get => (AvatarType)GetValue(AvatarDisplayProperty);
        set => SetValue(AvatarDisplayProperty, value);
    }

    public AccountsView()
    {
        InitializeComponent();
        // 从设置初始化显示类型
        AvatarDisplay = App.CurrentApp.Settings.AvatarDisplay;
        if (AvatarDisplayBox != null)
        {
            AvatarDisplayBox.SelectedIndex = AvatarDisplay switch
            {
                AvatarType.QqFirstDigit => 1,
                AvatarType.QqAvatar => 2,
                _ => 0,
            };
        }
        LoadAccounts();
    }

    private void LoadAccounts()
    {
        var accounts = App.CurrentApp.Accounts.Accounts;
        // 退订旧账号的勾选监听，再订阅当前账号
        foreach (var a in _subscribed)
            a.PropertyChanged -= OnAccountPropertyChanged;
        _subscribed.Clear();
        foreach (var a in accounts)
        {
            a.PropertyChanged += OnAccountPropertyChanged;
            _subscribed.Add(a);
        }
        AccountList.ItemsSource = accounts;
        CountText.Text = $"共 {accounts.Count} 个账号";
        UpdateBatchButtonState();
        _ = RefreshServerInfoAsync(accounts);
    }

    private readonly List<Account> _subscribed = new();

    /// <summary>勾选状态变化：刷新批量启动按钮并持久化勾选。</summary>
    private void OnAccountPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Account.IsSelected))
        {
            UpdateBatchButtonState();
            App.CurrentApp.Accounts.Save();
        }
    }

    /// <summary>批量启动按钮可用性跟随勾选状态。</summary>
    private void UpdateBatchButtonState()
    {
        if (BatchStartBtn == null)
            return;
        BatchStartBtn.IsEnabled = App.CurrentApp.Accounts.Accounts.Any(a => a.IsSelected);
    }

    /// <summary>批量启动：启动所有勾选的账号游戏窗口。</summary>
    private void OnBatchStart(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        foreach (var acc in App.CurrentApp.Accounts.Accounts.Where(a => a.IsSelected))
        {
            GameWindow.OpenAccount(acc, owner);
        }
    }

    /// <summary>账号对应的 GameHost userdata 目录（不存在返回 null）。</summary>
    private static string? ResolveUserDataDir(Account acc)
    {
        var gameHostDir = Path.GetDirectoryName(App.CurrentApp.Games.GameHostPath);
        if (gameHostDir == null)
            return null;
        var ud = !string.IsNullOrEmpty(acc.ScanUserDataDir)
            ? acc.ScanUserDataDir
            : Path.Combine(gameHostDir, "userdata", acc.QQ);
        return Directory.Exists(ud) ? ud : null;
    }

    /// <summary>从各账号登录 cookie 解析区服信息，异步刷新到账号列表。</summary>
    private async Task RefreshServerInfoAsync(System.Collections.ObjectModel.ObservableCollection<Account> accounts)
    {
        foreach (var acc in accounts)
        {
            var ud = ResolveUserDataDir(acc);
            if (ud == null)
                continue;

            var info = await Task.Run(() => CookieParser.ReadServerInfo(ud));
            if (info == null)
                continue;
            acc.HasLoginData = info.HasLogin;
            acc.ServerId = int.TryParse(info.ServerId, out var sid) ? sid : 0;
            if (!string.IsNullOrEmpty(info.ServerName))
            {
                acc.Server = info.ServerName;
            }
            else if (int.TryParse(info.ServerId, out sid))
            {
                // sServerName 缺失时用区服 ID 查区名（如 725 只有 zonelist=8856）
                var name = await ServerCatalog.GetServerNameAsync(sid);
                acc.Server = name ?? info.ServerId;
            }
        }
    }

    private void OnAvatarDisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AvatarDisplayBox == null) return;
        var t = AvatarDisplayBox.SelectedIndex switch
        {
            1 => AvatarType.QqFirstDigit,
            2 => AvatarType.QqAvatar,
            _ => AvatarType.NameChar,
        };
        AvatarDisplay = t;
        App.CurrentApp.Settings.AvatarDisplay = t;
    }

    private void OnAddAccount(object sender, RoutedEventArgs e)
    {
        var dlg = new AddAccountWindow
        {
            Owner = Window.GetWindow(this),
        };
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            LoadAccounts();
        }
    }

    private void OnStartGame(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not long id) return;
        var acc = App.CurrentApp.Accounts.Accounts.FirstOrDefault(a => a.Id == id);
        if (acc == null) return;
        // 复用共享的多开游戏窗口（顶部标签栏），在该窗口打开/切换账号标签
        GameWindow.OpenAccount(acc, Window.GetWindow(this));
    }

    private void OnEditAccount(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is long id)
        {
            var acc = App.CurrentApp.Accounts.Accounts.FirstOrDefault(a => a.Id == id);
            if (acc == null) return;
            var dlg = new AddAccountWindow
            {
                Owner = Window.GetWindow(this),
                EditTarget = acc,
            };
            if (dlg.ShowDialog() == true)
            {
                LoadAccounts();
            }
        }
    }

    private void OnDeleteAccount(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is long id)
        {
            App.CurrentApp.Accounts.RemoveAccount(id);
            LoadAccounts();
        }
    }
}
