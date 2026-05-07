using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>
/// One wastage line item for the Stock &amp; Waste Impact grid (display strings + numeric fields for totals).
/// </summary>
public sealed class WastageReportEntryRow
{
    public string ItemName { get; set; } = "";
    public string WastageDisplay { get; set; } = "";
    public string CostDisplay { get; set; } = "";
    public string LostRevenueDisplay { get; set; } = "";
    public string ImpactOnProfitDisplay { get; set; } = "";
    public string Reason { get; set; } = "";

    public decimal CostValue { get; set; }
    public decimal LostRevenueValue { get; set; }
    public decimal ImpactOnProfitValue { get; set; }
}

/// <summary>
/// Wastage / stock waste impact report overlay — same shell as <see cref="RptVoidsReportOverlay"/>; demo data until DB sync exists.
/// </summary>
public partial class RptWastageReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<WastageReportEntryRow> _rows = new();

    public RptWastageReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
    {
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));

        InitializeComponent();

        Loaded += (_, _) =>
        {
            ApplyStaticChrome();
            ReloadData();
            Keyboard.Focus(this);
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
        TxtGenerated.Text = FormatGeneratedCaption(DateTime.Now);

        var nfi = CloneReportNumberFormat();

        _rows.Clear();
        foreach (var row in BuildDemoSeed(nfi))
            _rows.Add(row);

        WastageRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        decimal sumCost = 0, sumLost = 0, sumImpact = 0;
        foreach (var r in _rows)
        {
            sumCost += r.CostValue;
            sumLost += r.LostRevenueValue;
            sumImpact += r.ImpactOnProfitValue;
        }

        TxtSummaryCostTotal.Text = FormatCurrency(sumCost, nfi);
        TxtSummaryLostRevenueTotal.Text = FormatCurrency(sumLost, nfi);
        TxtSummaryImpactTotal.Text = FormatCurrency(sumImpact, nfi);
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

    private static string FormatCurrency(decimal value, NumberFormatInfo nfi) => value.ToString("C2", nfi);

    private static WastageReportEntryRow[] BuildDemoSeed(NumberFormatInfo nfi) =>
            new[]
            {
                new WastageReportEntryRow
                {
                    ItemName = "Chicken Breast",
                    WastageDisplay = "12.5 kg",
                    CostValue = 1125.00m,
                    CostDisplay = FormatCurrency(1125.00m, nfi),
                    LostRevenueValue = 2850.00m,
                    LostRevenueDisplay = FormatCurrency(2850.00m, nfi),
                    ImpactOnProfitValue = 1725.00m,
                    ImpactOnProfitDisplay = FormatCurrency(1725.00m, nfi),
                    Reason = "Expired",
                },
                new WastageReportEntryRow
                {
                    ItemName = "Fresh Lettuce",
                    WastageDisplay = "8 heads",
                    CostValue = 240.00m,
                    CostDisplay = FormatCurrency(240.00m, nfi),
                    LostRevenueValue = 640.00m,
                    LostRevenueDisplay = FormatCurrency(640.00m, nfi),
                    ImpactOnProfitValue = 400.00m,
                    ImpactOnProfitDisplay = FormatCurrency(400.00m, nfi),
                    Reason = "Quality Issue",
                },
                new WastageReportEntryRow
                {
                    ItemName = "Tomatoes",
                    WastageDisplay = "5.2 kg",
                    CostValue = 156.00m,
                    CostDisplay = FormatCurrency(156.00m, nfi),
                    LostRevenueValue = 416.00m,
                    LostRevenueDisplay = FormatCurrency(416.00m, nfi),
                    ImpactOnProfitValue = 260.00m,
                    ImpactOnProfitDisplay = FormatCurrency(260.00m, nfi),
                    Reason = "Over-ordered",
                },
                new WastageReportEntryRow
                {
                    ItemName = "Milk",
                    WastageDisplay = "15 liters",
                    CostValue = 225.00m,
                    CostDisplay = FormatCurrency(225.00m, nfi),
                    LostRevenueValue = 450.00m,
                    LostRevenueDisplay = FormatCurrency(450.00m, nfi),
                    ImpactOnProfitValue = 225.00m,
                    ImpactOnProfitDisplay = FormatCurrency(225.00m, nfi),
                    Reason = "Freezer Failure",
                },
                new WastageReportEntryRow
                {
                    ItemName = "Ground Beef",
                    WastageDisplay = "8.0 kg",
                    CostValue = 960.00m,
                    CostDisplay = FormatCurrency(960.00m, nfi),
                    LostRevenueValue = 2400.00m,
                    LostRevenueDisplay = FormatCurrency(2400.00m, nfi),
                    ImpactOnProfitValue = 1440.00m,
                    ImpactOnProfitDisplay = FormatCurrency(1440.00m, nfi),
                    Reason = "Expired",
                },
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
