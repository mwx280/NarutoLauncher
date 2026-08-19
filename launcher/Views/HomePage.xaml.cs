using Microsoft.UI.Xaml.Controls;
using NarutoLauncher.ViewModels;

namespace NarutoLauncher.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; } = new();

    public HomePage()
    {
        InitializeComponent();
    }
}
