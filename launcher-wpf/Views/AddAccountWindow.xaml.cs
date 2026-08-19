using System.Windows;
using NarutoLauncher.Models;
using NarutoLauncher.Services;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace NarutoLauncher.Views;

public partial class AddAccountWindow : FluentWindow
{
    /// <summary>添加成功后的账号（未成功为 null）。</summary>
    public Account? Result { get; private set; }

    public AddAccountWindow()
    {
        InitializeComponent();
    }

    // ---- 账号密码登录 ----
    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var qq = QqBox.Text.Trim();
        if (string.IsNullOrEmpty(qq))
        {
            MessageBox.Show("请输入 QQ 号", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var pwd = PwdBox.Password;
        Result = App.CurrentApp.Accounts.AddAccount(
            qq, NameBox.Text.Trim(), pwd,
            scanLogin: false);
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    // ---- 扫码登录（TODO: 接入 GameHost ptqrlogin 轮询） ----
    private void OnRefreshQr(object sender, RoutedEventArgs e)
    {
        // TODO: 通过 GameHost 代理 ptqrshow 获取二维码图片 + qrsig cookie，
        //       轮询 ptqrlogin 完成登录后保存 cookie 并添加账号。
        // 当前暂用 WebView2 加载二维码占位（需在 XAML 引入 WebView2 或改 Image 加载）。
        MessageBox.Show("扫码登录即将支持（需接入 GameHost 登录代理）", "提示",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
