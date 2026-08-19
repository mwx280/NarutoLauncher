using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using NarutoLauncher.Views;

namespace NarutoLauncher;

public sealed partial class MainWindow : Window
{
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        Title = "火影忍者OL 启动器";
        _initialized = true;
        ContentFrame.Navigate(typeof(HomePage), null, new EntranceNavigationTransitionInfo());
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_initialized)
            return;

        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "games":
                    ContentFrame.Navigate(typeof(GamesPage));
                    break;
                case "accounts":
                    ContentFrame.Navigate(typeof(AccountsPage));
                    break;
                default:
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
            }
        }
    }
}
