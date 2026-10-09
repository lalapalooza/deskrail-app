using System.Globalization;
using System.Windows.Data;

namespace DeskRail.App.Converters;

public class SortKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Core.Models.SortKey key && parameter is string param)
            return key.ToString() == param;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string param)
            return Enum.Parse<Core.Models.SortKey>(param);
        return Binding.DoNothing;
    }
}
