using System;
using System.Globalization;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public sealed class WidthToHeightRatioConverter : IValueConverter
    {
        private const double DefaultRatio = 1.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double width;
            if (!TryReadDouble(value, out width))
                return 0.0;

            double ratio = DefaultRatio;
            double parsed;
            if (parameter != null &&
                Double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
            {
                ratio = parsed;
            }

            if (ratio < 0.0) ratio = 0.0;

            double height = width * ratio;

            if (Double.IsNaN(height) || Double.IsInfinity(height) || height < 0.0)
                return 0.0;

            return height;
        }

        private static bool TryReadDouble(object value, out double result)
        {
            result = 0.0;
            if (value == null) return false;
            if (value is double d) { result = d; return true; }
            if (value is float f) { result = f; return true; }
            string s = value.ToString();
            if (string.IsNullOrWhiteSpace(s)) return false;
            return Double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
