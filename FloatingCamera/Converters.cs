using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FloatingCamera;

/// <summary>开关右侧的 On/Off 文案，随语言切换自动刷新。</summary>
public sealed class OnOffTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3) return "";
        var isChecked = values[0] is bool b && b;
        return (isChecked ? values[1] : values[2])?.ToString() ?? "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>镜像：true → ScaleX = -1。</summary>
public sealed class MirrorScaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? -1.0 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>true → Collapsed，false → Visible。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
