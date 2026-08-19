using System.Globalization;
using System.Windows.Data;
using NarutoLauncher.Models;

namespace NarutoLauncher.Converters;

/// <summary>
/// 头像字符转换器：根据（账号, 全局头像显示类型）返回要显示的首字/首数字。
/// 传参：values[0]=Account, values[1]=AvatarType。
/// </summary>
public class AvatarCharDisplayConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not Account acc)
            return "?";

        var display = values[1] is AvatarType t ? t : AvatarType.NameChar;
        return display switch
        {
            AvatarType.QqFirstDigit =>
                !string.IsNullOrEmpty(acc.QQ) ? acc.QQ[..1] : "?",
            _ =>
                !string.IsNullOrEmpty(acc.Name) ? acc.Name[..1]
                : !string.IsNullOrEmpty(acc.QQ) ? acc.QQ[..1]
                : "?",
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}