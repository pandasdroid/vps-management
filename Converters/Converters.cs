using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VPSManager.Models;

namespace VPSManager.Converters;

public class BytesToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var param = parameter as string;

        if (param == "percent" && value is double percent)
        {
            // Return width based on percentage (max width ~200)
            return Math.Max(0, Math.Min(200, percent * 2));
        }

        if (value is long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            if (bytes >= 1024L * 1024L)
                return $"{bytes / (1024.0 * 1024.0):F0} MB";
            if (bytes >= 1024L)
                return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} B";
        }

        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ConnectionStatus status)
            return Brushes.Gray;

        var param = parameter as string;

        if (param == "bg")
        {
            return status switch
            {
                ConnectionStatus.Connected => new SolidColorBrush(Color.FromArgb(32, 34, 197, 94)),
                ConnectionStatus.Connecting => new SolidColorBrush(Color.FromArgb(32, 234, 179, 8)),
                ConnectionStatus.Error => new SolidColorBrush(Color.FromArgb(32, 239, 68, 68)),
                _ => new SolidColorBrush(Color.FromArgb(32, 142, 142, 160))
            };
        }

        if (param == "button")
        {
            return status switch
            {
                ConnectionStatus.Connected => "Disconnect",
                ConnectionStatus.Connecting => "Connecting...",
                _ => "Connect"
            };
        }

        return status switch
        {
            ConnectionStatus.Connected => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            ConnectionStatus.Connecting => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
            ConnectionStatus.Error => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(142, 142, 160))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var param = parameter as string;
        var boolValue = value is bool b && b;

        if (param == "inverse")
            boolValue = !boolValue;

        if (param == "inverse-bool")
            return !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var param = parameter as string;
        var isNull = value == null;

        if (value is int count)
            isNull = count == 0;

        if (param == "inverse")
            isNull = !isNull;

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
