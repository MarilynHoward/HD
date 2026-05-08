using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RestaurantPosWpf;

/// <summary>Demo status for low-stock rows until DB-backed data exists.</summary>
public enum LowStockReportEntryStatus
{
    Ok,
    Low,
    Critical,
}

/// <summary>One stock line for the Low Stock / Stock on Hand grid (display strings for binding).</summary>
public sealed class LowStockReportEntryRow
{
    public string ItemDisplay { get; set; } = "";
    public decimal CurrentValue { get; set; }
    public string CurrentDisplay { get; set; } = "";
    public decimal MinimumValue { get; set; }
    public string MinimumDisplay { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    /// <summary>Binding key for badge styling: <c>Ok</c>, <c>Low</c>, or <c>Critical</c>.</summary>
    public string StatusVariant { get; set; } = "";
    public string LastOrderDisplay { get; set; } = "";
}

/// <summary>
/// Low Stock Items (Stock on Hand) overlay — same shell as <see cref="RptVoidsReportOverlay"/>; footer uses the same band/grid as voids totals with hidden measure text (no visible totals); demo data until inventory facts are wired.
/// </summary>
public partial class RptLowStockItemsReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<LowStockReportEntryRow> _rows = new();
    private DateTime _generatedAt;

    public RptLowStockItemsReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
    {
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));

        InitializeComponent();

        Loaded += (_, _) =>
        {
            ApplyStaticChrome();
            ReloadData();
            Keyboard.Focus(this);
            Dispatcher.BeginInvoke(new Action(ApplyLowStockTableColumnWidth), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(ApplyLowStockTableColumnWidth), DispatcherPriority.ApplicationIdle);
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
        var ok = Application.Current.TryFindResource("Rpt.Report.StockStatus.Ok") as string ?? "OK";
        var low = Application.Current.TryFindResource("Rpt.Report.StockStatus.Low") as string ?? "Low";
        var critical = Application.Current.TryFindResource("Rpt.Report.StockStatus.Critical") as string ?? "Critical";

        _rows.Clear();
        foreach (var demo in BuildDemoSeed(nfi, ok, low, critical))
            _rows.Add(demo);

        LowStockRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);
        Dispatcher.BeginInvoke(new Action(ApplyLowStockTableColumnWidth), DispatcherPriority.ContextIdle);
    }

    private void LowStockListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Dispatcher.BeginInvoke(new Action(ApplyLowStockTableColumnWidth), DispatcherPriority.Render);

    private void LowStockTableHostGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Dispatcher.BeginInvoke(new Action(ApplyLowStockTableColumnWidth), DispatcherPriority.Render);

    /// <summary>
    /// ItemsControl measures list items with an unconstrained width unless given an explicit width; star columns then collapse to their MinWidth sum.
    /// Bind header, body, and footer column grids to the scroll viewer's effective content width so all columns use the full table width.
    /// Theme style <c>IcPopupShellBodyScrollViewerStyle</c> adds left padding when the vertical scrollbar is visible; subtract padding so width matches the arrange slot.
    /// </summary>
    private void ApplyLowStockTableColumnWidth()
    {
        if (LowStockListScrollViewer is null || LowStockListWidthHost is null
            || LowStockHeaderColumnsGrid is null || LowStockFooterColumnsGrid is null)
            return;

        var sv = LowStockListScrollViewer;
        var pad = sv.Padding;
        var w = sv.ViewportWidth - pad.Left - pad.Right;
        if (double.IsNaN(w) || double.IsInfinity(w) || w < 2)
        {
            if (LowStockTableHostGrid is { } host && host.ColumnDefinitions.Count > 0)
            {
                var cw = host.ColumnDefinitions[0].ActualWidth;
                if (!double.IsNaN(cw) && !double.IsInfinity(cw) && cw > 2)
                    w = cw;
            }
        }

        if (double.IsNaN(w) || double.IsInfinity(w) || w < 2)
            return;

        if (double.IsNaN(LowStockListWidthHost.Width) || Math.Abs(LowStockListWidthHost.Width - w) > 0.25)
            LowStockListWidthHost.Width = w;
        if (double.IsNaN(LowStockHeaderColumnsGrid.Width) || Math.Abs(LowStockHeaderColumnsGrid.Width - w) > 0.25)
            LowStockHeaderColumnsGrid.Width = w;
        if (double.IsNaN(LowStockFooterColumnsGrid.Width) || Math.Abs(LowStockFooterColumnsGrid.Width - w) > 0.25)
            LowStockFooterColumnsGrid.Width = w;
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

    private static string FormatQty(decimal value, NumberFormatInfo nfi)
    {
        if (value == decimal.Truncate(value))
            return value.ToString("0", nfi);
        return value.ToString("0.##", nfi);
    }

    /// <summary>Eight demo rows aligned with seeded <c>dashboard_attention_count</c> for <c>rpt.low_stock</c>.</summary>
    private static LowStockReportEntryRow[] BuildDemoSeed(
            NumberFormatInfo nfi,
            string okLabel,
            string lowLabel,
            string criticalLabel) =>
            new[]
            {
                Row("Chicken Breast (kg)", 45.5m, 50m, lowLabel, nameof(LowStockReportEntryStatus.Low), "2 days ago", nfi),
                Row("Cooking Oil (liters)", 85m, 40m, okLabel, nameof(LowStockReportEntryStatus.Ok), "5 days ago", nfi),
                Row("Potatoes (kg)", 120m, 80m, okLabel, nameof(LowStockReportEntryStatus.Ok), "1 day ago", nfi),
                Row("Onions (kg)", 15m, 30m, criticalLabel, nameof(LowStockReportEntryStatus.Critical), "7 days ago", nfi),
                Row("Flour (kg)", 65m, 50m, okLabel, nameof(LowStockReportEntryStatus.Ok), "3 days ago", nfi),
                Row("Rice (kg)", 8m, 25m, criticalLabel, nameof(LowStockReportEntryStatus.Critical), "14 days ago", nfi),
                Row("Sugar (kg)", 22m, 30m, lowLabel, nameof(LowStockReportEntryStatus.Low), "4 days ago", nfi),
                Row("Milk (liters)", 12m, 20m, lowLabel, nameof(LowStockReportEntryStatus.Low), "1 day ago", nfi),
            };

    private static LowStockReportEntryRow Row(
            string item,
            decimal current,
            decimal minimum,
            string statusLabel,
            string statusVariant,
            string lastOrder,
            NumberFormatInfo nfi) =>
            new()
            {
                ItemDisplay = item,
                CurrentValue = current,
                CurrentDisplay = FormatQty(current, nfi),
                MinimumValue = minimum,
                MinimumDisplay = FormatQty(minimum, nfi),
                StatusLabel = statusLabel,
                StatusVariant = statusVariant,
                LastOrderDisplay = lastOrder,
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
