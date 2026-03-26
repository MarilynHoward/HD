using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public sealed class CornerRadiusFromHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double height = 0.0;
            if (value is double h && h > 0.0)
                height = h;

            double desired = 8.0;
            if (parameter != null &&
                double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                desired = parsed;
            }

            if (height <= 0.0)
                return new CornerRadius(desired);

            double max = Math.Max(0.0, (height / 2.0) - 1.0);
            const double min = 3.0;

            double r = desired;
            r = Math.Min(r, max);
            r = Math.Max(r, min);

            return new CornerRadius(r);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
