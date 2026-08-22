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
    private object? _lastContent;
    private Type? _lastPageType;

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
        _lastContent = _home;
        _lastPageType = typeof(Views.HomeView);
        ApplyNavigationStyle();
    }

    /// <summary>根据设置切换导航栏风格。</summary>
    public void ApplyNavigationStyle()
    {
        // 记住当前页面
        _lastContent ??= ClassicContentHost?.Content;

        var style = App.CurrentApp.Settings.NavigationStyle;
        if (style == NavigationStyle.Modern)
        {
            ClassicLayout.Visibility = Visibility.Collapsed;
            NavView.Visibility = Visibility.Visible;
            // 恢复页面或默认首页
            var pageType = _lastPageType ?? typeof(Views.HomeView);
            NavView.Navigate(pageType);
        }
        else
        {
            NavView.Visibility = Visibility.Collapsed;
            ClassicLayout.Visibility = Visibility.Visible;
            // 恢复页面或默认首页
            ClassicContentHost.Content = _lastContent ?? _home;
            // 同步 ListBox 选中项
            SyncClassicNavSelection();
        }
    }

    private void SyncClassicNavSelection()
    {
        var tag = _lastContent switch
        {
            Views.HomeView => "home",
            Views.GamesView => "games",
            Views.AccountsView => "accounts",
            Views.SettingsView => "settings",
            _ => "home",
        };
        for (int i = 0; i < NavList.Items.Count; i++)
        {
            if (NavList.Items[i] is ListBoxItem item && item.Tag as string == tag)
            {
                NavList.SelectedIndex = i;
                break;
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
            "home" => ((object)_home, typeof(Views.HomeView)),
            "games" => ((object)_games, typeof(Views.GamesView)),
            "accounts" => ((object)_accounts, typeof(Views.AccountsView)),
            "settings" => ((object)_settings, typeof(Views.SettingsView)),
            _ => ((object)_home, typeof(Views.HomeView)),
        };
        ClassicContentHost.Content = page;
        _lastContent = page;
        _lastPageType = pageType;
    }

    // ---- LLT 导航（NavigationView） ----
    private void OnNavSelectionChanged(object sender, RoutedEventArgs e)
    {
        // 记录当前页面类型供切换风格时恢复
        if (NavView.SelectedItem is Wpf.Ui.Controls.NavigationViewItem item && item.TargetPageType is not null)
            _lastPageType = item.TargetPageType;
    }
}
