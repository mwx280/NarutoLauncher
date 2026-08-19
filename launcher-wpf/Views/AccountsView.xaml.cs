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
        var dlg = new AddAccountWindow
        {
            Owner = Window.GetWindow(this),
        };
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            LoadAccounts();
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
