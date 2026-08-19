using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using NarutoLauncher.Models;

namespace NarutoLauncher.Converters;

/// <summary>
/// QQ 头像转换器：根据（账号, 全局头像显示类型）返回头像图片。
/// 传参：values[0]=Account, values[1]=AvatarType。非 QQ头像 类型返回 null（不显示图片）。
/// </summary>
public class QqAvatarConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not Account acc)
            return null;
        var display = values[1] is AvatarType t ? t : AvatarType.NameChar;
        if (display != AvatarType.QqAvatar || string.IsNullOrEmpty(acc.QQ))
            return null;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(acc.AvatarUrl, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.Default;
            bmp.EndInit();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}