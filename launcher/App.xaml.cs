using Microsoft.UI.Xaml;
using NarutoLauncher.Services;

namespace NarutoLauncher;

public partial class App : Application
{
    private Window? _window;

    /// <summary>全局服务容器（各页面共享）。</summary>
    public static App CurrentApp { get; private set; } = null!;

    public AccountService Accounts { get; } = new();
    public SettingsService Settings { get; } = new();

    public App()
    {
        CurrentApp = this;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
