using System;
using System.Globalization;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public sealed class CountToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !int.TryParse(value.ToString(), out int count))
                return string.Empty;

            string noun = parameter?.ToString() ?? "item";
            string suffix = count == 1 ? noun : noun + "s";

            return $"{count} {suffix} selected";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
