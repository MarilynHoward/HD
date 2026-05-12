using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public sealed class DeliveryVarianceReportRow
{
    public string PoNumber { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Item { get; set; } = "";
    public string OrderedDisplay { get; set; } = "";
    public string ReceivedDisplay { get; set; } = "";
    public string VarianceDisplay { get; set; } = "";
    public string ValueDisplay { get; set; } = "";
    public decimal OrderedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal VarianceQty { get; set; }
    public decimal ValueAmount { get; set; }
    /// <summary><c>Neg</c>, <c>Pos</c>, or <c>Zero</c> for variance column coloring.</summary>
    public string VarianceSign { get; set; } = "Zero";
    /// <summary><c>Neg</c>, <c>Pos</c>, or <c>Zero</c> for value column coloring.</summary>
    public string ValueSign { get; set; } = "Zero";
}

/// <summary>
/// Delivery Variance overlay — same shell as <see cref="RptVoidsReportOverlay"/>; demo rows until procurement facts are wired.
/// </summary>
public partial class RptDeliveryVarianceReportOverlay : UserControl
{
    private static readonly Brush FallbackReportNegFg = CreateFrozenSolid(0xDC, 0x26, 0x26);
    private static readonly Brush FallbackReportPosFg = CreateFrozenSolid(0x22, 0xC5, 0x5E);
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<DeliveryVarianceReportRow> _rows = new();
    private DateTime _generatedAt;

    public RptDeliveryVarianceReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        TxtGenerated.Text = FormatGeneratedCaption(_generatedAt);

        var nfi = CloneReportNumberFormat();
        _rows.Clear();
        foreach (var demo in BuildDemoSeed(nfi))
            _rows.Add(demo);

        DeliveryVarianceRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        var sumOrdered = 0m;
        var sumReceived = 0m;
        var sumVariance = 0m;
        var sumValue = 0m;
        foreach (var r in _rows)
        {
            sumOrdered += r.OrderedQty;
            sumReceived += r.ReceivedQty;
            sumVariance += r.VarianceQty;
            sumValue += r.ValueAmount;
        }

        TxtTotalOrdered.Text = FormatQtyKg(sumOrdered, nfi);
        TxtTotalReceived.Text = FormatQtyKg(sumReceived, nfi);
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

    private void DeliveryVarianceListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.Render);

    private void DeliveryVarianceTableHostGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.Render);

    private void ApplyTableColumnWidth()
    {
        if (DeliveryVarianceListScrollViewer is null || DeliveryVarianceListWidthHost is null
            || DeliveryVarianceHeaderColumnsGrid is null || DeliveryVarianceTotalsColumnsGrid is null)
            return;

        var sv = DeliveryVarianceListScrollViewer;
        var pad = sv.Padding;
        var w = sv.ViewportWidth - pad.Left - pad.Right;
        if (double.IsNaN(w) || double.IsInfinity(w) || w < 2)
        {
            if (DeliveryVarianceTableHostGrid is { } host && host.ColumnDefinitions.Count > 0)
            {
                var cw = host.ColumnDefinitions[0].ActualWidth;
                if (!double.IsNaN(cw) && !double.IsInfinity(cw) && cw > 2)
                    w = cw;
            }
        }

        if (double.IsNaN(w) || double.IsInfinity(w) || w < 2)
            return;

        if (double.IsNaN(DeliveryVarianceListWidthHost.Width) || Math.Abs(DeliveryVarianceListWidthHost.Width - w) > 0.25)
            DeliveryVarianceListWidthHost.Width = w;
        if (double.IsNaN(DeliveryVarianceHeaderColumnsGrid.Width) || Math.Abs(DeliveryVarianceHeaderColumnsGrid.Width - w) > 0.25)
            DeliveryVarianceHeaderColumnsGrid.Width = w;
        if (double.IsNaN(DeliveryVarianceTotalsColumnsGrid.Width) || Math.Abs(DeliveryVarianceTotalsColumnsGrid.Width - w) > 0.25)
            DeliveryVarianceTotalsColumnsGrid.Width = w;
    }

    private string FormatGeneratedCaption(DateTime generatedLocal)
    {
        var justNow = Application.Current.TryFindResource("Rpt.Report.Generated.JustNow") as string ?? "Just now";
        if ((DateTime.Now - generatedLocal).TotalMinutes < 2.0)
            return justNow;

        return generatedLocal.ToString("g", CultureInfo.CurrentCulture);
    }

    private static NumberFormatInfo CloneReportNumberFormat()
    {
        var nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        nfi.NumberDecimalSeparator = ".";
        nfi.CurrencyDecimalSeparator = ".";
        nfi.PercentDecimalSeparator = ".";
        return nfi;
    }

    private static string FormatQty(decimal value, NumberFormatInfo nfi) =>
            value == decimal.Truncate(value) ? value.ToString("0.0", nfi) : value.ToString("0.##", nfi);

    private static string FormatQtyKg(decimal value, NumberFormatInfo nfi) => $"{FormatQty(value, nfi)} kg";

    private static string FormatVarianceSigned(decimal v, NumberFormatInfo nfi)
    {
        var abs = FormatQty(Math.Abs(v), nfi);
        if (v < 0)
            return $"-{abs} kg";
        if (v > 0)
            return $"+{abs} kg";
        return $"{FormatQty(0, nfi)} kg";
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

    private static string SignKey(decimal v) => v < 0 ? "Neg" : v > 0 ? "Pos" : "Zero";

    private static DeliveryVarianceReportRow Row(
            string po,
            string supplier,
            string item,
            decimal ordered,
            decimal received,
            decimal valueAmount,
            NumberFormatInfo nfi)
    {
        var variance = received - ordered;
        return new DeliveryVarianceReportRow
        {
            PoNumber = po,
            Supplier = supplier,
            Item = item,
            OrderedQty = ordered,
            ReceivedQty = received,
            VarianceQty = variance,
            ValueAmount = valueAmount,
            OrderedDisplay = FormatQtyKg(ordered, nfi),
            ReceivedDisplay = FormatQtyKg(received, nfi),
            VarianceDisplay = FormatVarianceSigned(variance, nfi),
            ValueDisplay = FormatValueZarSigned(valueAmount, nfi),
            VarianceSign = SignKey(variance),
            ValueSign = SignKey(valueAmount),
        };
    }

    /// <summary>Four demo rows aligned with seeded <c>dashboard_attention_count</c> for <c>rpt.delivery_variances</c>.</summary>
    private static DeliveryVarianceReportRow[] BuildDemoSeed(NumberFormatInfo nfi) =>
            new[]
            {
                Row("PO-2456", "Fresh Foods Ltd", "Chicken Breast", 50.0m, 48.5m, -135.00m, nfi),
                Row("PO-2457", "Metro Produce", "Cooking Oil (20L)", 40.0m, 41.0m, 450.00m, nfi),
                Row("PO-2458", "Coastal Seafood", "Hake Fillets", 120.0m, 120.0m, 0m, nfi),
                Row("PO-2459", "GrainCo", "Bread Flour", 200.0m, 195.0m, -450.00m, nfi),
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
