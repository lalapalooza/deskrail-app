using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DeskRail.Core.Models;

namespace DeskRail.App.Converters;

public class FileTypeToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FileType type) return Brushes.Gray;

        var color = type switch
        {
            FileType.Folder => "#FFB347",
            FileType.App => "#00E5A0",
            FileType.Document => "#60A5FA",
            FileType.Image => "#E879F9",
            FileType.Music => "#F472B6",
            FileType.Video => "#FB923C",
            FileType.Archive => "#A78BFA",
            FileType.System => "#4E5A6E",
            FileType.Code => "#47B3FF",
            _ => "#888888"
        };

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
