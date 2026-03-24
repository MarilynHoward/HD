using System.Globalization;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public class FontScaleConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            double scale = UiScaleRead.ReadScaleOrDefault(value);

            double baseSize = 12.0;
            if (parameter != null)
            {
                double tmpBase;
                if (UiScaleRead.TryReadDouble(parameter, out tmpBase) && tmpBase > 0.1 && tmpBase < 200.0)
                    baseSize = tmpBase;
            }

            double result = baseSize * scale;

            if (result < 6.0) result = 6.0;
            if (result > 96.0) result = 96.0;

            return result;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
