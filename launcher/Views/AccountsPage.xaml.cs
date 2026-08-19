using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NarutoLauncher.Models;
using NarutoLauncher.ViewModels;

namespace NarutoLauncher.Views;

public sealed partial class AccountsPage : Page
{
    public AccountsViewModel ViewModel { get; } = new();

    public AccountsPage()
    {
        InitializeComponent();
    }

    public string AccountCountText => $"共 {ViewModel.Accounts.Count} 个账号";

    private async void OnAddAccount(object sender, RoutedEventArgs e)
    {
        var qqBox = new TextBox { Header = "QQ 号", PlaceholderText = "输入 QQ 号" };
        var nameBox = new TextBox { Header = "备注（可选）", PlaceholderText = "如：大号·鸣人" };
        var pwdBox = new PasswordBox { Header = "密码（可选，本地加密存储）", PlaceholderText = "留空则使用扫码登录" };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(qqBox);
        panel.Children.Add(nameBox);
        panel.Children.Add(pwdBox);

        var dialog = new ContentDialog
        {
            Title = "添加账号",
            Content = panel,
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var qq = qqBox.Text.Trim();
        if (string.IsNullOrEmpty(qq))
        {
            ShowTip("请输入 QQ 号", InfoBarSeverity.Warning);
            return;
        }
        var pwd = pwdBox.Password;
        App.CurrentApp.Accounts.AddAccount(
            qq, nameBox.Text.Trim(), pwd,
            scanLogin: string.IsNullOrEmpty(pwd));
        ShowTip($"账号 {qq} 已添加", InfoBarSeverity.Success);
    }

    private void OnScanAdd(object sender, RoutedEventArgs e)
    {
        // TODO: 扫码登录（GameHost 代理 ptqrlogin）——后续接入
        ShowTip("扫码登录即将支持", InfoBarSeverity.Informational);
    }

    private void OnEditAccount(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is long id)
        {
            var acc = App.CurrentApp.Accounts.Accounts.FirstOrDefault(a => a.Id == id);
            if (acc == null) return;
            ShowTip($"编辑账号：{acc.DisplayName}（待实现）", InfoBarSeverity.Informational);
        }
    }

    private void OnDeleteAccount(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is long id)
        {
            App.CurrentApp.Accounts.RemoveAccount(id);
            ShowTip("账号已删除", InfoBarSeverity.Success);
        }
    }

    private void ShowTip(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        TipInfoBar.Severity = severity;
        TipInfoBar.Message = message;
        TipInfoBar.IsOpen = true;
    }
}
