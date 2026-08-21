using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NarutoLauncher.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
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

        // 主题模式：同步下拉框
        ThemeCombo.SelectedIndex = s.ThemeMode switch
        {
            ThemeMode.Dark => 1,
            ThemeMode.Light => 2,
            _ => 0,
        };

        // 主题色：同步下拉框
        AccentCombo.SelectedIndex = s.AccentMode == AccentMode.Custom ? 1 : 0;
        ColorPalette.Visibility = s.AccentMode == AccentMode.Custom
            ? Visibility.Visible : Visibility.Collapsed;

        // 功能开关
        GameSpeedBox.IsChecked = s.GameSpeed;
        AntiDropBox.IsChecked = s.AntiDrop;
        FlashGpuBox.IsChecked = s.FlashHardwareAcceleration;
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
        // Flash 硬件加速：拦截点击（开关不随点击翻转），确认后才开启/关闭
        FlashGpuBox.PreviewMouseLeftButtonDown += OnFlashGpuPreviewMouseDown;
        FlashGpuBox.PreviewKeyDown += OnFlashGpuPreviewKeyDown;
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

    /// <summary>
    /// Flash 硬件加速：点击开关时不让开关直接翻转（避免取消/关闭后开关仍打开）。
    /// 鼠标按下即拦截，弹出确认框，用户确认后才开启/关闭。
    /// </summary>
    private async void OnFlashGpuPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_initializing) return;
        e.Handled = true;
        await ToggleFlashGpuAsync();
    }

    /// <summary>键盘（空格）切换时同样拦截，行为与鼠标一致。</summary>
    private async void OnFlashGpuPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_initializing || e.Key != Key.Space) return;
        e.Handled = true;
        await ToggleFlashGpuAsync();
    }

    private async Task ToggleFlashGpuAsync()
    {
        var target = FlashGpuBox.IsChecked != true;
        if (target)
        {
            var options = new SimpleContentDialogCreateOptions
            {
                Title = "Flash 硬件加速",
                Content =
                    "此开关控制 Flash 的 GPU 硬件加速（3D/Stage3D 渲染）。\n" +
                    "火影忍者OL 这类传统 Flash 页游画面主要由 CPU 渲染，\n" +
                    "开启硬件加速基本没有提升。\n\n" +
                    "开启后可能带来副作用：\n" +
                    "· 画面花屏、闪烁或黑屏\n" +
                    "· 游戏崩溃或加载异常\n" +
                    "· 占用更多内存和资源\n\n" +
                    "不建议开启。此设置需要重新进入游戏才会生效。\n\n" +
                    "确定要开启吗？",
                CloseButtonText = "关闭",
                PrimaryButtonText = "开启",
                SecondaryButtonText = "取消",
                DefaultButton = ContentDialogButton.Secondary,
            };
            var result = await App.CurrentApp.DialogService.ShowSimpleDialogAsync(options);
            if (result != ContentDialogResult.Primary)
                return;
        }
        // 确认开启，或直接关闭（关闭无需确认）
        FlashGpuBox.IsChecked = target;
        App.CurrentApp.Settings.FlashHardwareAcceleration = target;
    }

    // ---- 主题模式切换 ----
    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        var s = App.CurrentApp.Settings;
        s.ThemeMode = ThemeCombo.SelectedIndex switch
        {
            1 => ThemeMode.Dark,
            2 => ThemeMode.Light,
            _ => ThemeMode.System,
        };
        ApplyTheme();
    }

    // ---- 主题色模式切换 ----
    private void OnAccentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        var s = App.CurrentApp.Settings;
        s.AccentMode = AccentCombo.SelectedIndex == 1 ? AccentMode.Custom : AccentMode.System;
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
