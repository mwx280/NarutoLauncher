using Microsoft.UI.Xaml.Controls;
using NarutoLauncher.ViewModels;

namespace NarutoLauncher.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
    }
}
