using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public sealed class TillDrawerVarianceReportRow
{
    public string DateDisplay { get; set; } = "";
    public string TillDrawer { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Cashier { get; set; } = "";
    public string ExpectedDisplay { get; set; } = "";
    public string ActualDisplay { get; set; } = "";
    public string VarianceDisplay { get; set; } = "";
    public decimal ExpectedCash { get; set; }
    public decimal ActualCash { get; set; }
    public decimal VarianceCash { get; set; }
    public string VarianceSign { get; set; } = "Zero";
    public string StatusLabel { get; set; } = "";
    /// <summary><c>Short</c> or <c>Over</c> for status pill styling.</summary>
    public string StatusVariant { get; set; } = "";
}

/// <summary>
/// Till / drawer variance overlay for Attention Needed <c>rpt.till_balance</c>; demo rows until POS facts are wired.
/// </summary>
public partial class RptTillDrawerVarianceReportOverlay : UserControl
{
    private static readonly Brush FallbackReportNegFg = CreateFrozenSolid(0xDC, 0x26, 0x26);
    private static readonly Brush FallbackReportPosFg = CreateFrozenSolid(0x22, 0xC5, 0x5E);
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<TillDrawerVarianceReportRow> _rows = new();
    private DateTime _generatedAt;

    public RptTillDrawerVarianceReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        TxtGenerated.Text = _generatedAt.ToString("yyyy/MM/dd HH:mm", CultureInfo.CurrentCulture);

        var nfi = CloneReportNumberFormat();
        var shortLabel = Application.Current.TryFindResource("Rpt.Report.TillStatus.Short") as string ?? "Short";
        var overLabel = Application.Current.TryFindResource("Rpt.Report.TillStatus.Over") as string ?? "Over";

        _rows.Clear();
        foreach (var demo in BuildDemoSeed(nfi, shortLabel, overLabel))
            _rows.Add(demo);

        TillVarianceRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        var sumExpected = 0m;
        var sumActual = 0m;
        var sumVar = 0m;
        foreach (var r in _rows)
        {
            sumExpected += r.ExpectedCash;
            sumActual += r.ActualCash;
            sumVar += r.VarianceCash;
        }

        TxtTotalExpected.Text = FormatRandPlain(sumExpected, nfi);
        TxtTotalActual.Text = FormatRandPlain(sumActual, nfi);
        TxtTotalVariance.Text = FormatVarianceRandSigned(sumVar, nfi);
        ApplySignedBrush(TxtTotalVariance, sumVar);

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

    private void TillVarianceListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.Render);

    private void TillVarianceTableHostGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Dispatcher.BeginInvoke(new Action(ApplyTableColumnWidth), DispatcherPriority.Render);

    private void ApplyTableColumnWidth()
    {
        if (TillVarianceListScrollViewer is null || TillVarianceListWidthHost is null
            || TillVarianceHeaderColumnsGrid is null || TillVarianceTotalsColumnsGrid is null)
            return;

        var sv = TillVarianceListScrollViewer;
        var pad = sv.Padding;
        var w = sv.ViewportWidth - pad.Left - pad.Right;
        if (double.IsNaN(w) || double.IsInfinity(w) || w < 2)
        {
            if (TillVarianceTableHostGrid is { } host && host.ColumnDefinitions.Count > 0)
            {
                var cw = host.ColumnDefinitions[0].ActualWidth;
                if (!double.IsNaN(cw) && !double.IsInfinity(cw) && cw > 2)
                    w = cw;
            }
        }

        if (double.IsNaN(w) || double.IsInfinity(w) || w < 2)
            return;

        if (double.IsNaN(TillVarianceListWidthHost.Width) || Math.Abs(TillVarianceListWidthHost.Width - w) > 0.25)
            TillVarianceListWidthHost.Width = w;
        if (double.IsNaN(TillVarianceHeaderColumnsGrid.Width) || Math.Abs(TillVarianceHeaderColumnsGrid.Width - w) > 0.25)
            TillVarianceHeaderColumnsGrid.Width = w;
        if (double.IsNaN(TillVarianceTotalsColumnsGrid.Width) || Math.Abs(TillVarianceTotalsColumnsGrid.Width - w) > 0.25)
            TillVarianceTotalsColumnsGrid.Width = w;
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

    private static string FormatRandPlain(decimal v, NumberFormatInfo nfi) => $"R {v.ToString("N2", nfi)}";

    private static string FormatVarianceRandSigned(decimal v, NumberFormatInfo nfi)
    {
        var abs = Math.Abs(v).ToString("N2", nfi);
        if (v < 0)
            return $"-R {abs}";
        if (v > 0)
            return $"+R {abs}";
        return $"R {abs}";
    }

    private static string SignKey(decimal v) => v < 0 ? "Neg" : v > 0 ? "Pos" : "Zero";

    private static TillDrawerVarianceReportRow Row(
            DateTime rowDate,
            string till,
            string branch,
            string cashier,
            decimal expected,
            decimal actual,
            string shortLabel,
            string overLabel,
            NumberFormatInfo nfi)
    {
        var variance = actual - expected;
        var statusVariant = variance < 0 ? "Short" : "Over";
        var statusLabel = variance < 0 ? shortLabel : overLabel;
        return new TillDrawerVarianceReportRow
        {
            DateDisplay = rowDate.ToString("d", CultureInfo.CurrentCulture),
            TillDrawer = till,
            Branch = branch,
            Cashier = cashier,
            ExpectedCash = expected,
            ActualCash = actual,
            VarianceCash = variance,
            ExpectedDisplay = FormatRandPlain(expected, nfi),
            ActualDisplay = FormatRandPlain(actual, nfi),
            VarianceDisplay = FormatVarianceRandSigned(variance, nfi),
            VarianceSign = SignKey(variance),
            StatusLabel = statusLabel,
            StatusVariant = statusVariant,
        };
    }

    /// <summary>Demo rows for <c>rpt.till_balance</c> (count may differ from <c>dashboard_attention_count</c> until facts exist).</summary>
    private static TillDrawerVarianceReportRow[] BuildDemoSeed(
            NumberFormatInfo nfi,
            string shortLabel,
            string overLabel) =>
            new[]
            {
                Row(new DateTime(2026, 4, 28), "Till 01 — Front", "Main", "S. Mkhize", 9845.00m, 9725.00m, shortLabel, overLabel, nfi),
                Row(new DateTime(2026, 4, 29), "Till 02 — Bar", "Main", "J. Naidoo", 5120.00m, 5139.50m, shortLabel, overLabel, nfi),
                Row(new DateTime(2026, 4, 30), "Till 01 — Front", "Express", "T. Dlamini", 11000.00m, 11000.00m, shortLabel, overLabel, nfi),
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
