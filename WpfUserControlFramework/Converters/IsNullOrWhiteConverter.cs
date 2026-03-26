using System;
using System.Globalization;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public sealed class IsNullOrWhiteConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return true;

            string? s = value as string;
            if (s != null)
                return string.IsNullOrWhiteSpace(s);

            return string.IsNullOrWhiteSpace(value.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
