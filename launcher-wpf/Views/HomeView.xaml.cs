using System.Windows;
using System.Windows.Controls;

namespace NarutoLauncher.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        AccountList.ItemsSource = App.CurrentApp.Accounts.Accounts;
        GameSpeedBox.IsChecked = App.CurrentApp.Settings.GameSpeed;
        AntiDropBox.IsChecked = App.CurrentApp.Settings.AntiDrop;
        AutoScriptBox.IsChecked = App.CurrentApp.Settings.AutoScript;
        AutoTaskBox.IsChecked = App.CurrentApp.Settings.AutoTask;
        TrayBox.IsChecked = App.CurrentApp.Settings.MinimizeToTray;
    }

    private void OnStartGame(object sender, RoutedEventArgs e)
    {
        // 跳转到游戏页并自动开始
        var win = Window.GetWindow(this) as MainWindow;
        win?.NavigateTo(1);  // 游戏页索引
    }
}
