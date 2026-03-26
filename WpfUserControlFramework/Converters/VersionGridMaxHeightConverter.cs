using System;
using System.Globalization;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public sealed class VersionGridMaxHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double rightHeight = ReadDouble(values, 0);
            double footerHeight = ReadDouble(values, 1);
            double fontScale = ReadDouble(values, 2);
            if (fontScale <= 0) fontScale = 1;

            double baseOverhead = 0;
            if (parameter != null)
                double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out baseOverhead);

            double max = rightHeight - footerHeight - baseOverhead * fontScale;
            double min = 120 * fontScale;
            if (max < min) max = min;

            return max;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static double ReadDouble(object[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length || values[index] == null)
                return 0;
            if (values[index] is double d) return d;
            double.TryParse(values[index].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed);
            return parsed;
        }
    }
}
