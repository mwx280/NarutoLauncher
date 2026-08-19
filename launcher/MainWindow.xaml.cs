using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using NarutoLauncher.Views;

namespace NarutoLauncher;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "火影忍者OL 启动器";
        NavHome.IsChecked = true;
        ContentFrame.Navigate(typeof(HomePage), null, new EntranceNavigationTransitionInfo());
    }

    private void OnNavClicked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton b)
        {
            SetNavChecked(b);
            var tag = b.Tag?.ToString();
            switch (tag)
            {
                case "home":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                case "games":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                case "accounts":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                case "settings":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
            }
        }
    }

    private void SetNavChecked(ToggleButton selected)
    {
        NavHome.IsChecked = ReferenceEquals(NavHome, selected);
        NavGames.IsChecked = ReferenceEquals(NavGames, selected);
        NavAccounts.IsChecked = ReferenceEquals(NavAccounts, selected);
        NavSettings.IsChecked = ReferenceEquals(NavSettings, selected);
    }

    private void OnFrameNavigating(object sender, Microsoft.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
    {
        // 导航事件钩子（预留）
    }
}
