using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DeskRail.App.Converters;

public class GridGapToThicknessConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double columnGap || values[1] is not double rowGap)
            return DependencyProperty.UnsetValue;

        return new Thickness(columnGap / 2, rowGap / 2, columnGap / 2, rowGap / 2);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
