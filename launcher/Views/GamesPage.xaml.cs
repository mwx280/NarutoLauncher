using Microsoft.UI.Xaml.Controls;
using NarutoLauncher.ViewModels;

namespace NarutoLauncher.Views;

public sealed partial class GamesPage : Page
{
    public GamesViewModel ViewModel { get; } = new();

    public GamesPage()
    {
        InitializeComponent();
    }

    public string RunningCountText => $"运行中：{ViewModel.RunningCount} 个游戏窗口";
}
