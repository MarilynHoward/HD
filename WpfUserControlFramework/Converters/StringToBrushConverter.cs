using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RestaurantPosWpf
{
    public sealed class StringToBrushConverter : IValueConverter
    {
        private static readonly BrushConverter BrushConverter = new BrushConverter();

        private static readonly Brush DefaultBrush = CreateDefaultBrush();

        private static Brush CreateDefaultBrush()
        {
            return BrushConverter.ConvertFromString("#22C55E") as Brush ?? Brushes.Green;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? colourText = value as string;

            if (!string.IsNullOrWhiteSpace(colourText))
            {
                try
                {
                    object? result = BrushConverter.ConvertFromString(colourText);
                    if (result is Brush brush)
                        return brush;
                }
                catch
                {
                    // Fall back to default
                }
            }

            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
