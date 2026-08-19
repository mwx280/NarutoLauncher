using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NarutoLauncher.Converters;

/// <summary>运行状态 → 状态条背景色。</summary>
public class RunBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true
            ? new SolidColorBrush(Color.FromArgb(0x26, 0xE8, 0x48, 0x2C))
            : new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>运行状态 → 状态文字色。</summary>
public class RunFgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true
            ? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x8A, 0x5C))
            : new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x91, 0xA5));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
