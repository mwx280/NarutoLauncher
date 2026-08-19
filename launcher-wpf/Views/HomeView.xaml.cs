using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NarutoLauncher.Services;

namespace NarutoLauncher.Views;

public partial class HomeView : UserControl
{
    private readonly NewsService _news = new();
    private bool _loading;

    public HomeView()
    {
        InitializeComponent();
        // 启动时显示加载界面，网络抓取完成后再显示公告
        Loaded += async (_, _) => await LoadNewsAsync();
    }

    private async Task LoadNewsAsync()
    {
        if (_loading) return;
        _loading = true;
        RefreshBtn.IsEnabled = false;
        LoadingBar.Visibility = Visibility.Visible;
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            var items = await _news.FetchListAsync();
            NewsList.ItemsSource = items;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载公告失败：{ex.Message}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _loading = false;
            RefreshBtn.IsEnabled = true;
            LoadingBar.Visibility = Visibility.Collapsed;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
        => await LoadNewsAsync();

    private async void OnNewsClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not NewsItem item)
            return;
        try
        {
            var detail = await _news.FetchDetailAsync(item.Url);
            NewsDetailWindow.Show(detail.Title, detail.HtmlBody, Window.GetWindow(this));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载公告内容失败：{ex.Message}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
