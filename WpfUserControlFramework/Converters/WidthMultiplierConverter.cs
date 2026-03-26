using System;
using System.Globalization;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public class WidthMultiplierConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Double width;

            if (value == null || !Double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out width))
                return 0.0;

            Double multiplier = 1.0;

            if (parameter != null)
                Double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out multiplier);

            return width * multiplier;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
