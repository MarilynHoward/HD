using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    /// <summary>
    /// Builds a <see cref="CornerRadius"/> from <see cref="UiScaleState.FontScale"/> (or any scale factor)
    /// and a comma-separated list of base radii in layout pixels (before scale).
    /// One value → uniform corners; four values → top-left, top-right, bottom-right, bottom-left.
    /// </summary>
    public sealed class ScaledCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = UiScaleRead.ReadScaleOrDefault(value);
            string raw = parameter?.ToString()?.Trim() ?? "8";

            string[] parts = raw.Split(',');
            if (parts.Length == 1)
            {
                if (!double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double u))
                    u = 8.0;
                u *= scale;
                return new CornerRadius(u);
            }

            double tl = 0, tr = 0, br = 0, bl = 0;
            if (parts.Length > 0)
                double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out tl);
            if (parts.Length > 1)
                double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out tr);
            if (parts.Length > 2)
                double.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out br);
            if (parts.Length > 3)
                double.TryParse(parts[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out bl);

            return new CornerRadius(tl * scale, tr * scale, br * scale, bl * scale);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
