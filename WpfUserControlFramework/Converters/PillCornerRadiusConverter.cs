using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RestaurantPosWpf;

/// <summary>
/// Capsule / pill shape: uniform radius ≈ half the measured height. Uses a small fallback before layout.
/// </summary>
public sealed class PillCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var height = value is double h ? h : 0.0;
        if (height <= 0.0)
            return new CornerRadius(4.0);

        var r = height / 2.0;
        if (r < 2.0)
            r = 2.0;
        return new CornerRadius(r);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
}
