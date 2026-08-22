using System.Windows;
using System.Windows.Controls;
using NarutoLauncher.Services;
using NarutoLauncher.Views;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NarutoLauncher;

public partial class MainWindow : FluentWindow
{
    private readonly HomeView _home = new();
    private readonly GamesView _games = new();
    private readonly AccountsView _accounts = new();
    private readonly SettingsView _settings = new();
    private readonly AboutView _about = new();

    public MainWindow()
    {
        InitializeComponent();
        App.CurrentApp.DialogService.SetDialogHost(ContentDialogHost);
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyNavigationStyle();
    }

    /// <summary>根据设置切换导航栏风格。</summary>
    public void ApplyNavigationStyle()
    {
        var style = App.CurrentApp.Settings.NavigationStyle;
        if (style == NavigationStyle.Modern)
        {
            ClassicLayout.Visibility = Visibility.Collapsed;
            NavView.Visibility = Visibility.Visible;
            NavView.Navigate(typeof(NarutoLauncher.Views.HomeView));
        }
        else
        {
            NavView.Visibility = Visibility.Collapsed;
            ClassicLayout.Visibility = Visibility.Visible;
            App.CurrentApp.DialogService.SetDialogHost(ClassicDialogHost);
            // 经典模式默认选中首页
            if (NavList.SelectedIndex < 0)
                NavList.SelectedIndex = 0;
            ClassicContentHost.Content = _home;
        }
    }

    // ---- 经典导航（ListBox） ----
    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClassicContentHost == null) return;
        var tag = (NavList.SelectedItem as ListBoxItem)?.Tag as string;
        ClassicContentHost.Content = tag switch
        {
            "home" => _home,
            "games" => _games,
            "accounts" => _accounts,
            "settings" => _settings,
            _ => _home,
        };
    }

    // ---- LLT 导航（NavigationView） ----
    private void OnNavSelectionChanged(object sender, RoutedEventArgs e)
    {
    }
}
