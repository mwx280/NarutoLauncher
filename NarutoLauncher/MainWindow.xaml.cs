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
    private Type? _currentPageType;

    public MainWindow()
    {
        InitializeComponent();
        App.CurrentApp.DialogService.SetDialogHost(ContentDialogHost);
        App.CurrentApp.Settings.NavigationStyleChanged += OnNavigationStyleChanged;
        Loaded += MainWindow_Loaded;
    }

    private void OnNavigationStyleChanged()
    {
        if (IsLoaded)
            ApplyNavigationStyle();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _currentPageType = typeof(HomeView);
        ApplyNavigationStyle();
    }

    /// <summary>根据设置切换导航栏风格，保持当前页面。</summary>
    public void ApplyNavigationStyle()
    {
        var style = App.CurrentApp.Settings.NavigationStyle;
        var pageType = _currentPageType ?? typeof(HomeView);

        if (style == NavigationStyle.Modern)
        {
            ClassicLayout.Visibility = Visibility.Collapsed;
            NavView.Visibility = Visibility.Visible;
            NavView.Navigate(pageType);
        }
        else
        {
            NavView.Visibility = Visibility.Collapsed;
            ClassicLayout.Visibility = Visibility.Visible;
            ClassicContentHost.Content = CreatePage(pageType);
            SyncClassicNavSelection(pageType);
        }
    }

    private object CreatePage(Type type)
    {
        if (type == typeof(HomeView)) return _home;
        if (type == typeof(GamesView)) return _games;
        if (type == typeof(AccountsView)) return _accounts;
        if (type == typeof(SettingsView)) return _settings;
        if (type == typeof(AboutView)) return _about;
        return _home;
    }

    private void SyncClassicNavSelection(Type pageType)
    {
        var tag = pageType switch
        {
            _ when pageType == typeof(HomeView) => "home",
            _ when pageType == typeof(GamesView) => "games",
            _ when pageType == typeof(AccountsView) => "accounts",
            _ when pageType == typeof(SettingsView) => "settings",
            _ => "home",
        };
        for (int i = 0; i < NavList.Items.Count; i++)
        {
            if (NavList.Items[i] is ListBoxItem item && item.Tag as string == tag)
            {
                NavList.SelectedIndex = i;
                return;
            }
        }
    }

    // ---- 经典导航（ListBox） ----
    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClassicContentHost == null) return;
        var tag = (NavList.SelectedItem as ListBoxItem)?.Tag as string;
        var (page, pageType) = tag switch
        {
            "home" => ((object)_home, typeof(HomeView)),
            "games" => ((object)_games, typeof(GamesView)),
            "accounts" => ((object)_accounts, typeof(AccountsView)),
            "settings" => ((object)_settings, typeof(SettingsView)),
            _ => ((object)_home, typeof(HomeView)),
        };
        ClassicContentHost.Content = page;
        _currentPageType = pageType;
    }

    // ---- 简约导航（NavigationView） ----
    private void OnNavSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (NavView.SelectedItem is Wpf.Ui.Controls.NavigationViewItem item && item.TargetPageType is not null)
            _currentPageType = item.TargetPageType;
    }
}
