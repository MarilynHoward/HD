using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace RestaurantPosWpf
{
    public sealed class BadgeListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string raw = value as string;
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            List<string> list = raw
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => s.Length > 0)
                .ToList();

            return list;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
