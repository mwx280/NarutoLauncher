using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace NarutoLauncher.Services;

/// <summary>
/// 主题管理器：基于 WPF-UI 切换深色/浅色主题与强调色。
/// </summary>
public static class ThemeManager
{
    private static ThemeMode _mode = ThemeMode.System;
    private static bool _useCustomAccent;
    private static Color _customAccent = Color.FromRgb(0xE8, 0x48, 0x2C);

    /// <summary>当前是否深色主题。</summary>
    public static bool IsDark { get; private set; }

    /// <summary>应用主题（启动时调用 + 设置变化时调用）。</summary>
    public static void Apply(ThemeMode mode, bool useCustomAccent, Color customAccent)
    {
        _mode = mode;
        _useCustomAccent = useCustomAccent;
        _customAccent = customAccent;

        // 确定是否深色
        IsDark = mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => SystemThemeIsDark(),
        };

        // WPF-UI 原生主题切换
        ApplicationThemeManager.Apply(IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light);

        // 强调色（自定义优先，否则用系统）
        var accent = useCustomAccent ? customAccent : SystemAccentColor();
        ApplicationAccentColorManager.Apply(accent, ApplicationThemeManager.GetAppTheme());
    }

    // ---------- 系统检测 ----------

    /// <summary>系统是否为深色主题。</summary>
    private static bool SystemThemeIsDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = key?.GetValue("AppsUseLightTheme");
            return v is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>系统强调色。</summary>
    private static Color SystemAccentColor()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\DWM");
            var v = key?.GetValue("ColorizationColor");
            if (v is int argb)
            {
                // ColorizationColor 格式为 0xAARRGGBB
                return Color.FromRgb(
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));
            }
        }
        catch { }
        return Color.FromRgb(0x00, 0x78, 0xD4);  // Win11 默认蓝
    }
}
