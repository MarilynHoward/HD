using System;
using System.Globalization;

namespace RestaurantPosWpf;

internal static class RptReportGeneratedCaption
{
    public static string Format(DateTime generatedLocal) =>
        generatedLocal.ToString("yyyy/MM/dd HH:mm", CultureInfo.CurrentCulture);
}
