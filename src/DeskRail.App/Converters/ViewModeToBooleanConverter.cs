using System.Globalization;
using System.Windows.Data;
using DeskRail.Core.Models;

namespace DeskRail.App.Converters;

public class ViewModeToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ViewMode mode
           && parameter is string modeName
           && Enum.TryParse(modeName, out ViewMode requestedMode)
           && mode == requestedMode;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
