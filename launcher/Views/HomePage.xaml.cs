using Microsoft.UI.Xaml.Controls;
using NarutoLauncher.Models;
using NarutoLauncher.ViewModels;

namespace NarutoLauncher.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; } = new();

    public HomePage()
    {
        InitializeComponent();
    }

    private void OnAccountClick(object sender, ItemClickEventArgs e)
    {
        // 账号卡片点击（后续：切换/管理账号）
    }
}
