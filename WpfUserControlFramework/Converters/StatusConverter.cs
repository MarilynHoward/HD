using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RestaurantPosWpf
{
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).Trim().ToLowerInvariant();
            return s switch
            {
                "active" => new SolidColorBrush(Color.FromRgb(43, 188, 94)),
                "suspended" => new SolidColorBrush(Color.FromRgb(204, 0, 0)),
                "outofstock" => new SolidColorBrush(Color.FromRgb(204, 0, 0)),
                "inactive" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                _ => Brushes.Transparent
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public sealed class StatusToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).Trim().ToLowerInvariant();
            return s switch
            {
                "active" => new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                "suspended" => new SolidColorBrush(Color.FromRgb(204, 0, 0)),
                "outofstock" => new SolidColorBrush(Color.FromRgb(204, 0, 0)),
                "inactive" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                _ => Brushes.Transparent
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public sealed class StatusToTextBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new SolidColorBrush(Color.FromRgb(255, 255, 255));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
