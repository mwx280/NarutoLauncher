using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Views;
using Wpf.Ui.Controls;

namespace NarutoLauncher;

public partial class MainWindow : FluentWindow
{
    private readonly HomeView _home = new();
    private readonly GamesView _games = new();
    private readonly AccountsView _accounts = new();
    private readonly SettingsView _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        // 绑定全局对话框服务的宿主（UI 风格提示框）
        App.CurrentApp.DialogService.SetDialogHost(ContentDialogHost);
        // 默认选中首页
        NavList.SelectedIndex = 0;
        ContentHost.Content = _home;
    }

    /// <summary>按索引切换页面（0 首页，1 游戏，2 账号，3 设置）。</summary>
    public void NavigateTo(int index)
    {
        ContentHost.Content = index switch
        {
            0 => _home,
            1 => _games,
            2 => _accounts,
            3 => _settings,
            _ => _home,
        };
        if (NavList.Items.Count > index)
            NavList.SelectedIndex = index;
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContentHost == null)
            return;
        var idx = NavList.SelectedIndex;
        ContentHost.Content = idx switch
        {
            0 => _home,
            1 => _games,
            2 => _accounts,
            3 => _settings,
            _ => _home,
        };
    }
}
