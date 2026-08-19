using System.Windows;
using System.Windows.Media;

namespace NarutoLauncher.Services;

/// <summary>
/// 主题管理器：切换深色/浅色主题与强调色。
/// </summary>
public static class ThemeManager
{
    private const string LightThemePath = "Themes/LightTheme.xaml";
    private const string DarkThemePath = "Themes/DarkTheme.xaml";

    private static ResourceDictionary? _lightDict;
    private static ResourceDictionary? _darkDict;
    private static ResourceDictionary? _currentTheme;

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

        if (_lightDict == null)
            _lightDict = new ResourceDictionary { Source = new Uri(LightThemePath, UriKind.Relative) };
        if (_darkDict == null)
            _darkDict = new ResourceDictionary { Source = new Uri(DarkThemePath, UriKind.Relative) };

        var app = Application.Current;
        if (app == null) return;

        // 移除当前主题字典
        if (_currentTheme != null)
            app.Resources.MergedDictionaries.Remove(_currentTheme);

        _currentTheme = IsDark ? _darkDict : _lightDict;
        app.Resources.MergedDictionaries.Add(_currentTheme);

        ApplyAccent(useCustomAccent ? customAccent : SystemAccentColor());
    }

    /// <summary>应用强调色到 App.Resources。</summary>
    private static void ApplyAccent(Color accent)
    {
        var app = Application.Current;
        if (app == null) return;

        // 生成强调色系
        app.Resources["AccentBrush"] = new SolidColorBrush(accent);
        app.Resources["AccentBrushHover"] = new SolidColorBrush(Lighten(accent, 0.08));
        app.Resources["AccentBrushPressed"] = new SolidColorBrush(Darken(accent, 0.10));
        app.Resources["AccentFaint"] = new SolidColorBrush(Color.FromArgb(
            0x26, accent.R, accent.G, accent.B));
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

    // ---------- 颜色工具 ----------

    private static Color Lighten(Color c, double amount)
    {
        return Color.FromRgb(
            (byte)Math.Min(255, c.R + 255 * amount),
            (byte)Math.Min(255, c.G + 255 * amount),
            (byte)Math.Min(255, c.B + 255 * amount));
    }

    private static Color Darken(Color c, double amount)
    {
        return Color.FromRgb(
            (byte)Math.Max(0, c.R - 255 * amount),
            (byte)Math.Max(0, c.G - 255 * amount),
            (byte)Math.Max(0, c.B - 255 * amount));
    }
}
