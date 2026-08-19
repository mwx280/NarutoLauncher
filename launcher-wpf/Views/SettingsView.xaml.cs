using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NarutoLauncher.Services;
using ThemeMode = NarutoLauncher.Services.ThemeMode;
using AccentMode = NarutoLauncher.Services.AccentMode;

namespace NarutoLauncher.Views;

public partial class SettingsView : UserControl
{
    private bool _initializing;

    public SettingsView()
    {
        InitializeComponent();
        LoadSettings();
        _initializing = false;
    }

    private void LoadSettings()
    {
        _initializing = true;
        var s = App.CurrentApp.Settings;

        // 深色模式
        ThemeSystemRadio.IsChecked = s.ThemeMode == ThemeMode.System;
        ThemeDarkRadio.IsChecked = s.ThemeMode == ThemeMode.Dark;
        ThemeLightRadio.IsChecked = s.ThemeMode == ThemeMode.Light;

        // 主题色
        AccentSystemRadio.IsChecked = s.AccentMode == AccentMode.System;
        AccentCustomRadio.IsChecked = s.AccentMode == AccentMode.Custom;
        ColorPalette.Visibility = s.AccentMode == AccentMode.Custom
            ? Visibility.Visible : Visibility.Collapsed;

        // 功能开关
        GameSpeedBox.IsChecked = s.GameSpeed;
        AntiDropBox.IsChecked = s.AntiDrop;
        AutoScriptBox.IsChecked = s.AutoScript;
        AutoTaskBox.IsChecked = s.AutoTask;
        TrayBox.IsChecked = s.MinimizeToTray;
        RememberPwdBox.IsChecked = s.RememberPassword;

        // 开关变化保存
        GameSpeedBox.Checked += SaveSwitches;
        GameSpeedBox.Unchecked += SaveSwitches;
        AntiDropBox.Checked += SaveSwitches;
        AntiDropBox.Unchecked += SaveSwitches;
        AutoScriptBox.Checked += SaveSwitches;
        AutoScriptBox.Unchecked += SaveSwitches;
        AutoTaskBox.Checked += SaveSwitches;
        AutoTaskBox.Unchecked += SaveSwitches;
        TrayBox.Checked += SaveSwitches;
        TrayBox.Unchecked += SaveSwitches;
        RememberPwdBox.Checked += SaveSwitches;
        RememberPwdBox.Unchecked += SaveSwitches;
    }

    private void SaveSwitches(object? sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        var s = App.CurrentApp.Settings;
        s.GameSpeed = GameSpeedBox.IsChecked == true;
        s.AntiDrop = AntiDropBox.IsChecked == true;
        s.AutoScript = AutoScriptBox.IsChecked == true;
        s.AutoTask = AutoTaskBox.IsChecked == true;
        s.MinimizeToTray = TrayBox.IsChecked == true;
        s.RememberPassword = RememberPwdBox.IsChecked == true;
    }

    // ---- 深色模式切换 ----
    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing || !(sender as RadioButton)?.IsChecked == true) return;
        var s = App.CurrentApp.Settings;
        s.ThemeMode = ThemeDarkRadio.IsChecked == true ? ThemeMode.Dark
                     : ThemeLightRadio.IsChecked == true ? ThemeMode.Light
                     : ThemeMode.System;
        ApplyTheme();
    }

    // ---- 主题色切换 ----
    private void OnAccentChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing || !(sender as RadioButton)?.IsChecked == true) return;
        var s = App.CurrentApp.Settings;
        s.AccentMode = AccentCustomRadio.IsChecked == true ? AccentMode.Custom : AccentMode.System;
        ColorPalette.Visibility = s.AccentMode == AccentMode.Custom
            ? Visibility.Visible : Visibility.Collapsed;
        ApplyTheme();
    }

    private void OnColorClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is string hex)
        {
            var s = App.CurrentApp.Settings;
            s.AccentColor = hex;
            ApplyTheme();
        }
    }

    /// <summary>应用主题（从设置读取）。</summary>
    private void ApplyTheme()
    {
        var s = App.CurrentApp.Settings;
        var accent = ParseColor(s.AccentColor);
        ThemeManager.Apply(s.ThemeMode, s.AccentMode == AccentMode.Custom, accent);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(0xE8, 0x48, 0x2C);
        }
    }
}
