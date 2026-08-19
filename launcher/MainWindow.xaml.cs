using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using NarutoLauncher.Views;

namespace NarutoLauncher;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "火影忍者OL 启动器";
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage), null, new EntranceNavigationTransitionInfo());
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "games":
                case "accounts":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                case "settings":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                default:
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
            }
        }
        else if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(HomePage));
        }
    }
}
