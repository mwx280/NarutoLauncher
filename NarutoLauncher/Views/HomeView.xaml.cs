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
        // 每次进入首页时自动抓取最新公告（加载界面 → 完成后显示）
        IsVisibleChanged += OnVisibilityChanged;
    }

    private async void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await LoadNewsAsync();
    }

    private async Task LoadNewsAsync()
    {
        if (_loading) return;
        _loading = true;
        RefreshBtn.IsEnabled = false;
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
            var win = new NewsDetailWindow(detail.Title, detail.HtmlBody)
            {
                Owner = Window.GetWindow(this),
            };
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载公告内容失败：{ex.Message}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
