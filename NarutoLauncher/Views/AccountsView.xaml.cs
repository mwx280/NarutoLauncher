using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Models;

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
        AccountList.ItemsSource = App.CurrentApp.Accounts.Accounts;
        CountText.Text = $"共 {App.CurrentApp.Accounts.Accounts.Count} 个账号";
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
        var win = new GameWindow(acc)
        {
            Owner = Window.GetWindow(this),
        };
        win.Show();
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
