using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
    }

    // 监听第二实例发来的"显示主窗口"消息，走 managed 的 ShowMainWindow，
    // 保证托盘 Hide 后重新启动时 WPF 可见性/渲染状态正确，避免白屏。
    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == App.WmShowMain)
        {
            ShowMainWindow();
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>显示并激活主窗口（供单实例唤醒调用）。</summary>
    private void ShowMainWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
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

    // 关闭窗口时按设置最小化到托盘（从托盘「退出」才真正退出）
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (App.CurrentApp.Settings.MinimizeToTray && !App.CurrentApp.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
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
            _ when pageType == typeof(AboutView) => "about",
            _ => "home",
        };
        // 先清除两个列表的选中
        NavList.SelectedIndex = -1;
        FooterList.SelectedIndex = -1;
        // 在对应列表中选中
        if (tag is "home" or "games" or "accounts")
            SelectNavItem(NavList, tag);
        else
            SelectNavItem(FooterList, tag);
    }

    private static void SelectItemFromTag(ListBox list, string tag)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i] is ListBoxItem item && item.Tag as string == tag)
            {
                list.SelectedIndex = i;
                return;
            }
        }
    }

    // ---- 经典导航（ListBox） ----
    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClassicContentHost == null || NavList.SelectedIndex < 0) return;
        // 切换主菜单时取消底部选中
        FooterList.SelectionChanged -= OnFooterNavChanged;
        FooterList.SelectedIndex = -1;
        FooterList.SelectionChanged += OnFooterNavChanged;

        var tag = (NavList.SelectedItem as ListBoxItem)?.Tag as string;
        NavigateClassic(tag);
    }

    private void OnFooterNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClassicContentHost == null || FooterList.SelectedIndex < 0) return;
        // 切换底部时取消主菜单选中
        NavList.SelectionChanged -= OnNavChanged;
        NavList.SelectedIndex = -1;
        NavList.SelectionChanged += OnNavChanged;

        var tag = (FooterList.SelectedItem as ListBoxItem)?.Tag as string;
        NavigateClassic(tag);
    }

    private void NavigateClassic(string? tag)
    {
        var (page, pageType) = tag switch
        {
            "home" => ((object)_home, typeof(HomeView)),
            "games" => ((object)_games, typeof(GamesView)),
            "accounts" => ((object)_accounts, typeof(AccountsView)),
            "settings" => ((object)_settings, typeof(SettingsView)),
            "about" => ((object)_about, typeof(AboutView)),
            _ => ((object)_home, typeof(HomeView)),
        };
        ClassicContentHost.Content = page;
        _currentPageType = pageType;
    }

    private void SelectNavItem(ListBox list, string tag)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i] is ListBoxItem item && item.Tag as string == tag)
            {
                list.SelectedIndex = i;
                return;
            }
        }
    }

    // ---- 简约导航（NavigationView） ----
    private void OnNavSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (NavView.SelectedItem is Wpf.Ui.Controls.NavigationViewItem item && item.TargetPageType is not null)
            _currentPageType = item.TargetPageType;
    }
}
