using System.Windows;
using System.Windows.Controls;

namespace NarutoLauncher.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        GameSpeedBox.IsChecked = App.CurrentApp.Settings.GameSpeed;
        AntiDropBox.IsChecked = App.CurrentApp.Settings.AntiDrop;
        AutoScriptBox.IsChecked = App.CurrentApp.Settings.AutoScript;
        AutoTaskBox.IsChecked = App.CurrentApp.Settings.AutoTask;
        TrayBox.IsChecked = App.CurrentApp.Settings.MinimizeToTray;
        RememberPwdBox.IsChecked = App.CurrentApp.Settings.RememberPassword;

        GameSpeedBox.Checked += Save;
        GameSpeedBox.Unchecked += Save;
        AntiDropBox.Checked += Save;
        AntiDropBox.Unchecked += Save;
        AutoScriptBox.Checked += Save;
        AutoScriptBox.Unchecked += Save;
        AutoTaskBox.Checked += Save;
        AutoTaskBox.Unchecked += Save;
        TrayBox.Checked += Save;
        TrayBox.Unchecked += Save;
        RememberPwdBox.Checked += Save;
        RememberPwdBox.Unchecked += Save;
    }

    private void Save(object? sender, RoutedEventArgs e)
    {
        var s = App.CurrentApp.Settings;
        s.GameSpeed = GameSpeedBox.IsChecked == true;
        s.AntiDrop = AntiDropBox.IsChecked == true;
        s.AutoScript = AutoScriptBox.IsChecked == true;
        s.AutoTask = AutoTaskBox.IsChecked == true;
        s.MinimizeToTray = TrayBox.IsChecked == true;
        s.RememberPassword = RememberPwdBox.IsChecked == true;
    }
}
