using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public sealed class StockVarianceReportRow
{
    public string Item { get; set; } = "";
    public string ExpectedDisplay { get; set; } = "";
    public string ActualDisplay { get; set; } = "";
    public string VarianceDisplay { get; set; } = "";
    public string VarPercentDisplay { get; set; } = "";
    public string ValueDisplay { get; set; } = "";
    public decimal ExpectedQty { get; set; }
    public decimal ActualQty { get; set; }
    public decimal VarianceQty { get; set; }
    public decimal VarPercent { get; set; }
    public decimal ValueAmount { get; set; }
    public string VarianceSign { get; set; } = "Zero";
    public string ValueSign { get; set; } = "Zero";
}

/// <summary>Stock variance overlay — same shell as <see cref="RptDeliveryVarianceReportOverlay"/>; demo rows until stock facts are wired.</summary>
public partial class RptStockVarianceReportOverlay : UserControl
{
    private static readonly Brush FallbackReportNegFg = CreateFrozenSolid(0xDC, 0x26, 0x26);
    private static readonly Brush FallbackReportPosFg = CreateFrozenSolid(0x22, 0xC5, 0x5E);
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<StockVarianceReportRow> _rows = new();
    private DateTime _generatedAt;

    public RptStockVarianceReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
    {
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));

        InitializeComponent();

        Loaded += (_, _) =>
        {
            ApplyStaticChrome();
            ReloadData();
            Keyboard.Focus(this);
            Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.ApplicationIdle);
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                _onClose();
                e.Handled = true;
            }
        };
    }

    private void ApplyStaticChrome()
    {
        var sep = "   |   ";
        TxtFilterSummary.Text =
                _filters.DateRangeDisplay + sep + _filters.BranchDisplay + sep + _filters.ChannelDisplay + sep + _filters.UserRoleDisplay;
        TxtPeriod.Text = _filters.DateRangeDisplay;
    }

    private void ReloadData()
    {
        _generatedAt = DateTime.Now;
        TxtGenerated.Text = RptReportGeneratedCaption.Format(_generatedAt);

        var nfi = CloneReportNumberFormat();
        _rows.Clear();
        foreach (var demo in BuildDemoSeed(nfi))
            _rows.Add(demo);

        StockVarianceRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        var sumExpected = 0m;
        var sumActual = 0m;
        var sumVariance = 0m;
        var sumValue = 0m;
        foreach (var r in _rows)
        {
            sumExpected += r.ExpectedQty;
            sumActual += r.ActualQty;
            sumVariance += r.VarianceQty;
            sumValue += r.ValueAmount;
        }

        TxtTotalExpected.Text = FormatQty(sumExpected, nfi);
        TxtTotalActual.Text = FormatQty(sumActual, nfi);
        TxtTotalVariance.Text = FormatVarianceSigned(sumVariance, nfi);
        TxtTotalValue.Text = FormatValueZarSigned(sumValue, nfi);
        ApplySignedBrush(TxtTotalVariance, sumVariance);
        ApplySignedBrush(TxtTotalValue, sumValue);

        Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.ContextIdle);
    }

    private static void ApplySignedBrush(TextBlock tb, decimal v)
    {
        tb.Foreground = v switch
        {
            < 0 => ReportSignBrushNeg(),
            > 0 => ReportSignBrushPos(),
            _ => Application.Current.TryFindResource("MainForeground") as Brush ?? Brushes.Black,
        };
    }

    private static SolidColorBrush CreateFrozenSolid(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Brush ReportSignBrushNeg() =>
            Application.Current?.TryFindResource("Brush.RptReportNegativeForeground") as Brush ?? FallbackReportNegFg;

    private static Brush ReportSignBrushPos() =>
            Application.Current?.TryFindResource("Brush.RptReportPositiveForeground") as Brush ?? FallbackReportPosFg;

    private void StockVarianceListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.Render);

    private void StockVarianceTableHostGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.Render);

    private void ApplyTableColumnWidth()
    {
        if (StockVarianceListScrollViewer is null || StockVarianceListWidthHost is null
            || StockVarianceHeaderColumnsGrid is null || StockVarianceTotalsColumnsGrid is null)
            return;

        var sv = StockVarianceListScrollViewer;
        var pad = sv.Padding;
        var w = sv.ViewportWidth - pad.Left - pad.Right;
        if (double.IsNaN(w) || double.IsInfinity(w) || w < 2)
        {
            if (StockVarianceTableHostGrid is { } host && host.ColumnDefinitions.Count > 0)
            {
                var cw = host.ColumnDefinitions[0].ActualWidth;
                if (!double.IsNaN(cw) && !double.IsInfinity(cw) && cw > 2)
                    w = cw;
            }
        }

        if (double.IsNaN(w) || double.IsInfinity(w) || w < 2)
            return;

        if (double.IsNaN(StockVarianceListWidthHost.Width) || Math.Abs(StockVarianceListWidthHost.Width - w) > 0.25)
            StockVarianceListWidthHost.Width = w;
        if (double.IsNaN(StockVarianceHeaderColumnsGrid.Width) || Math.Abs(StockVarianceHeaderColumnsGrid.Width - w) > 0.25)
            StockVarianceHeaderColumnsGrid.Width = w;
        if (double.IsNaN(StockVarianceTotalsColumnsGrid.Width) || Math.Abs(StockVarianceTotalsColumnsGrid.Width - w) > 0.25)
            StockVarianceTotalsColumnsGrid.Width = w;
    }

    private static NumberFormatInfo CloneReportNumberFormat()
    {
        var nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        nfi.NumberDecimalSeparator = ".";
        nfi.CurrencyDecimalSeparator = ".";
        nfi.PercentDecimalSeparator = ".";
        return nfi;
    }

    private static string FormatQty(decimal value, NumberFormatInfo nfi) => value.ToString("0.0", nfi);

    private static string FormatVarianceSigned(decimal v, NumberFormatInfo nfi)
    {
        var abs = FormatQty(Math.Abs(v), nfi);
        if (v < 0)
            return $"-{abs}";
        if (v > 0)
            return $"+{abs}";
        return FormatQty(0, nfi);
    }

    private static string FormatPercentSigned(decimal v, NumberFormatInfo nfi)
    {
        var abs = Math.Abs(v).ToString("0.0", nfi);
        if (v < 0)
            return $"-{abs}%";
        if (v > 0)
            return $"+{abs}%";
        return $"{abs}%";
    }

    private static string FormatValueZarSigned(decimal v, NumberFormatInfo nfi)
    {
        var abs = Math.Abs(v).ToString("N2", nfi);
        if (v < 0)
            return $"-R {abs}";
        if (v > 0)
            return $"+R {abs}";
        return $"R {abs}";
    }

    private static decimal ComputeVarPercent(decimal expected, decimal actual)
    {
        if (expected == 0m)
            return 0m;
        return (actual - expected) / expected * 100m;
    }

    private static string SignKey(decimal v) => v < 0 ? "Neg" : v > 0 ? "Pos" : "Zero";

    private static StockVarianceReportRow Row(
            string item,
            decimal expected,
            decimal actual,
            decimal valueAmount,
            NumberFormatInfo nfi)
    {
        var variance = actual - expected;
        var varPct = ComputeVarPercent(expected, actual);
        return new StockVarianceReportRow
        {
            Item = item,
            ExpectedQty = expected,
            ActualQty = actual,
            VarianceQty = variance,
            VarPercent = varPct,
            ValueAmount = valueAmount,
            ExpectedDisplay = FormatQty(expected, nfi),
            ActualDisplay = FormatQty(actual, nfi),
            VarianceDisplay = FormatVarianceSigned(variance, nfi),
            VarPercentDisplay = FormatPercentSigned(varPct, nfi),
            ValueDisplay = FormatValueZarSigned(valueAmount, nfi),
            VarianceSign = SignKey(variance),
            ValueSign = SignKey(valueAmount),
        };
    }

    private static StockVarianceReportRow[] BuildDemoSeed(NumberFormatInfo nfi) =>
            new[]
            {
                Row("Chicken Breast (kg)", 50.0m, 48.5m, -135.00m, nfi),
                Row("Beef Mince (kg)", 35.0m, 35.0m, 0m, nfi),
                Row("Cooking Oil (liters)", 80.0m, 82.0m, 80.00m, nfi),
                Row("Potatoes (kg)", 120.0m, 118.5m, -22.50m, nfi),
                Row("Onions (kg)", 95.0m, 95.0m, 0m, nfi),
            };

    private void Refresh_Click(object sender, RoutedEventArgs e) => ReloadData();

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var title = Application.Current.TryFindResource("Rpt.Report.StubPrintTitle") as string ?? "Print";
        var body = Application.Current.TryFindResource("Rpt.Report.StubPrintBody") as string ?? "";
        MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var title = Application.Current.TryFindResource("Rpt.Report.StubExportTitle") as string ?? "Export";
        var body = Application.Current.TryFindResource("Rpt.Report.StubExportBody") as string ?? "";
        MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => _onClose();
}
