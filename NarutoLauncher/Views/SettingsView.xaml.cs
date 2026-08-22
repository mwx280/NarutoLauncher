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
        SettingsService.NavigationStyleChangedExternally += RefreshNavStyleCombo;
    }

    private void RefreshNavStyleCombo()
    {
        _initializing = true;
        NavStyleCombo.SelectedIndex = App.CurrentApp.Settings.NavigationStyle == Services.NavigationStyle.Modern ? 1 : 0;
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
        NavStyleCombo.SelectedIndex = s.NavigationStyle == Services.NavigationStyle.Modern ? 1 : 0;
        DprModeCombo.SelectedIndex = s.DprMode == Services.DprMode.Quality ? 1 : 0;
        FlashQualityCombo.SelectedIndex = s.FlashQuality switch
        {
            "high" => 2,
            "medium" => 1,
            _ => 0,
        };

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
        if (!target)
        {
            // 关闭硬件加速时弹出确认提示
            var options = new SimpleContentDialogCreateOptions
            {
                Title = "Flash 硬件加速",
                Content =
                    "实测关闭硬件加速后游戏流畅度会明显下降，\n" +
                    "GPU 合成默认开启，画面更流畅、延迟更低。\n\n" +
                    "关闭后可能表现：\n" +
                    "· 游戏帧率降低、操作卡顿\n" +
                    "· 大规模战斗场景掉帧\n" +
                    "· 画面响应变慢\n\n" +
                    "不建议关闭。此设置需要重新进入游戏才会生效。\n\n" +
                    "确定要关闭吗？",
                CloseButtonText = "取消",
                PrimaryButtonText = "关闭",
                DefaultButton = ContentDialogButton.Primary,
            };
            var result = await App.CurrentApp.DialogService.ShowSimpleDialogAsync(options);
            if (result != ContentDialogResult.Primary)
                return;
        }
        // 确认关闭，或直接开启（开启无需确认）
        FlashGpuBox.IsChecked = target;
        App.CurrentApp.Settings.FlashHardwareAcceleration = target;
    }

    // ---- 导航风格切换 ----
    private void OnNavStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        App.CurrentApp.Settings.NavigationStyle = NavStyleCombo.SelectedIndex == 1
            ? Services.NavigationStyle.Modern
            : Services.NavigationStyle.Classic;
    }

    private async void OnDprModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (DprModeCombo.SelectedIndex != 1)
        {
            // 切到性能优先无需确认
            App.CurrentApp.Settings.DprMode = Services.DprMode.Performance;
            return;
        }
        // 切到画质优先弹出确认提示
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "分辨率模式",
            Content =
                "画质优先模式会损失部分性能优化，\n" +
                "全局 Flash 画质（如木叶村等场景）以及文字\n" +
                "都将被设置为高画质，游戏流畅度会受到较大影响。\n\n" +
                "除非游戏窗口显示过小，否则不建议开启画质优先。\n\n" +
                "此设置需要重新进入游戏才会生效。\n\n" +
                "确定要切换吗？",
            CloseButtonText = "取消",
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Secondary,
        };
        var result = await App.CurrentApp.DialogService.ShowSimpleDialogAsync(options);
        if (result == ContentDialogResult.Primary)
        {
            App.CurrentApp.Settings.DprMode = Services.DprMode.Quality;
        }
        else
        {
            // 取消：恢复下拉框到性能优先
            DprModeCombo.SelectedIndex = 0;
        }
    }

    private void OnFlashQualityChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        var quality = FlashQualityCombo.SelectedIndex switch
        {
            2 => "high",
            1 => "medium",
            _ => "low",
        };
        App.CurrentApp.Settings.FlashQuality = quality;
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
