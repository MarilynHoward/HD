using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public class ThicknessScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = UiScaleRead.ReadScaleOrDefault(value);

            double left = 0.0;
            double top = 0.0;
            double right = 0.0;
            double bottom = 0.0;

            string raw = parameter as string;

            if (!string.IsNullOrEmpty(raw))
            {
                string[] parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                double p0, p1, p2, p3;

                switch (parts.Length)
                {
                    case 1:
                        if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out p0))
                            left = top = right = bottom = p0;
                        break;

                    case 2:
                        if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out p0))
                            left = right = p0;
                        if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out p1))
                            top = bottom = p1;
                        break;

                    default:
                        if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out p0))
                            left = p0;
                        if (parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out p1))
                            top = p1;
                        if (parts.Length > 2 && double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out p2))
                            right = p2;
                        if (parts.Length > 3 && double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out p3))
                            bottom = p3;
                        break;
                }
            }

            left *= scale;
            top *= scale;
            right *= scale;
            bottom *= scale;

            left = Clamp(left, 0.0, 500.0);
            top = Clamp(top, 0.0, 500.0);
            right = Clamp(right, 0.0, 500.0);
            bottom = Clamp(bottom, 0.0, 500.0);

            return new Thickness(left, top, right, bottom);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
