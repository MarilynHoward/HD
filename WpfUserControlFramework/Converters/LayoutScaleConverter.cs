using System.Globalization;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public class LayoutScaleConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            double scale = UiScaleRead.ReadScaleOrDefault(value);

            double baseSize = 0.0;
            if (parameter != null)
            {
                double tmpBase;
                if (UiScaleRead.TryReadDouble(parameter, out tmpBase))
                    baseSize = tmpBase;
            }

            return baseSize * scale;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
