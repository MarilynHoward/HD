using System;
using System.Globalization;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public sealed class ScaledDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = UiScaleRead.ReadScaleOrDefault(value);

            double baseValue = 1.0;
            if (parameter != null)
            {
                double tmpBase;
                if (UiScaleRead.TryReadDouble(parameter, out tmpBase))
                    baseValue = tmpBase;
            }

            return baseValue * scale;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
