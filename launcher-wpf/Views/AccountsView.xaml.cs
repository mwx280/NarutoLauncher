using System.Windows;
using System.Windows.Controls;

namespace NarutoLauncher.Views;

public partial class AccountsView : UserControl
{
    public AccountsView()
    {
        InitializeComponent();
        LoadAccounts();
    }

    private void LoadAccounts()
    {
        AccountList.ItemsSource = App.CurrentApp.Accounts.Accounts;
        CountText.Text = $"共 {App.CurrentApp.Accounts.Accounts.Count} 个账号";
    }

    private void OnAddAccount(object sender, RoutedEventArgs e)
    {
        var qqBox = new TextBox { Width = 200 };
        var nameBox = new TextBox { Width = 200 };
        var pwdBox = new PasswordBox { Width = 200 };
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "QQ 号" });
        panel.Children.Add(qqBox);
        panel.Children.Add(new TextBlock { Text = "备注（可选）" });
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock { Text = "密码（可选，留空则扫码登录）" });
        panel.Children.Add(pwdBox);

        var dlg = new Window
        {
            Title = "添加账号",
            Content = panel,
            Width = 320,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
        };
        var okBtn = new Button { Content = "添加", IsDefault = true, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(16, 4, 16, 4) };
        okBtn.Click += (_, _) => dlg.DialogResult = true;
        panel.Children.Add(okBtn);

        if (dlg.ShowDialog() != true)
            return;

        var qq = qqBox.Text.Trim();
        if (string.IsNullOrEmpty(qq))
            return;
        var pwd = pwdBox.Password;
        App.CurrentApp.Accounts.AddAccount(qq, nameBox.Text.Trim(), pwd,
            scanLogin: string.IsNullOrEmpty(pwd));
        LoadAccounts();
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
