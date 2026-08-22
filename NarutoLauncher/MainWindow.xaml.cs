using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NarutoLauncher;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        // 全局对话框宿主（供 UI 风格 ContentDialog 提示框使用）
        App.CurrentApp.DialogService.SetDialogHost(ContentDialogHost);
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 默认导航到首页
        NavView.Navigate(typeof(NarutoLauncher.Views.HomeView));
    }

    /// <summary>导航选中变化（页面由 TargetPageType 自动承载）。</summary>
    private void OnNavSelectionChanged(object sender, RoutedEventArgs e)
    {
        // 页面内容由 NavigationView 根据 TargetPageType 自动导航，无需手动切换。
    }
}