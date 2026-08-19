using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Views;

namespace NarutoLauncher;

public partial class MainWindow : HandyControl.Controls.Window
{
    private readonly HomeView _home = new();
    private readonly GamesView _games = new();
    private readonly AccountsView _accounts = new();
    private readonly SettingsView _settings = new();

    public MainWindow()
    {
        InitializeComponent();
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
