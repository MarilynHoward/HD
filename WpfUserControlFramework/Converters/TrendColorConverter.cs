using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RestaurantPosWpf
{
    public sealed class TrendColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Int32 trend;

            if (!TryReadTrend(value, out trend))
                return new SolidColorBrush(Color.FromRgb(243, 244, 246)); // #F3F4F6

            if (trend > 0)
                return new SolidColorBrush(Color.FromRgb(220, 252, 231)); // #DCFCE7

            if (trend < 0)
                return new SolidColorBrush(Color.FromRgb(254, 226, 226)); // #FEE2E2

            return new SolidColorBrush(Color.FromRgb(243, 244, 246)); // #F3F4F6
        }

        private static bool TryReadTrend(object value, out Int32 trend)
        {
            trend = 0;
            if (value == null) return false;

            if (value is Int32 i32) { trend = i32; return true; }
            if (value is Int64 i64)
            {
                if (i64 > Int32.MaxValue) { trend = Int32.MaxValue; return true; }
                if (i64 < Int32.MinValue) { trend = Int32.MinValue; return true; }
                trend = (Int32)i64; return true;
            }

            string s = value.ToString();
            if (string.IsNullOrWhiteSpace(s)) return false;

            if (Int32.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 parsed))
            { trend = parsed; return true; }

            if (Double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out Double d))
            {
                if (d > Int32.MaxValue) { trend = Int32.MaxValue; return true; }
                if (d < Int32.MinValue) { trend = Int32.MinValue; return true; }
                trend = (Int32)Math.Round(d, MidpointRounding.AwayFromZero); return true;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
