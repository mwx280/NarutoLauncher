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
        // 立即显示本地缓存（无网络延迟）
        NewsList.ItemsSource = _news.GetItems();
        // 后台刷新最新公告（有新公告时更新列表）
        Loaded += async (_, _) => await RefreshBackgroundAsync();
    }

    /// <summary>后台刷新（不阻塞 UI，静默更新）。</summary>
    private async Task RefreshBackgroundAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            await _news.RefreshAsync();
            RefreshList();
        }
        catch
        {
            // 后台刷新失败静默（保留缓存显示）
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>前台刷新（按钮触发，带加载指示）。</summary>
    private async Task RefreshForegroundAsync()
    {
        if (_loading) return;
        _loading = true;
        RefreshBtn.IsEnabled = false;
        LoadingBar.Visibility = Visibility.Visible;
        try
        {
            var hasNew = await _news.RefreshAsync();
            RefreshList();
            if (hasNew)
                MessageBox.Show("公告已更新", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刷新公告失败：{ex.Message}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _loading = false;
            RefreshBtn.IsEnabled = true;
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshList()
    {
        NewsList.ItemsSource = _news.GetItems();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
        => await RefreshForegroundAsync();

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
