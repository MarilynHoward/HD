using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>One discount audit line item for the Discounts Audit grid (display strings for binding).</summary>
public sealed class DiscountAuditReportEntryRow
{
    public string DateDisplay { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string DiscountDisplay { get; set; } = "";
    public decimal AmountValue { get; set; }
    public string AmountDisplay { get; set; } = "";
    public string Reason { get; set; } = "";
    public string UserDisplay { get; set; } = "";
    public string ApprovedByDisplay { get; set; } = "";
}

/// <summary>
/// Discounts Audit overlay — same shell as <see cref="RptVoidsReportOverlay"/>; demo data only until fact sync is wired.
/// </summary>
public partial class RptDiscountsAuditReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<DiscountAuditReportEntryRow> _rows = new();
    private DateTime _generatedAt;

    public RptDiscountsAuditReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        _generatedAt = DateTime.Now;
        TxtGenerated.Text = RptReportGeneratedCaption.Format(_generatedAt);

        var nfi = CloneReportNumberFormat();

        _rows.Clear();
        foreach (var demo in BuildDemoSeed(nfi))
            _rows.Add(demo);

        DiscountsRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        var sumAmount = 0m;
        foreach (var r in _rows)
            sumAmount += r.AmountValue;

        TxtSummaryAmountTotal.Text = FormatCurrency(sumAmount, nfi);
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

    private static DiscountAuditReportEntryRow[] BuildDemoSeed(NumberFormatInfo nfi) =>
            new[]
            {
                new DiscountAuditReportEntryRow
                {
                    DateDisplay = "21 Apr 2026",
                    TransactionId = "TXN-8498",
                    DiscountDisplay = "10%",
                    AmountValue = 45.00m,
                    AmountDisplay = FormatCurrency(45.00m, nfi),
                    Reason = "Staff Discount",
                    UserDisplay = "Sarah M.",
                    ApprovedByDisplay = "Auto",
                },
                new DiscountAuditReportEntryRow
                {
                    DateDisplay = "21 Apr 2026",
                    TransactionId = "TXN-8467",
                    DiscountDisplay = "15%",
                    AmountValue = 78.50m,
                    AmountDisplay = FormatCurrency(78.50m, nfi),
                    Reason = "Manager Discount",
                    UserDisplay = "John K.",
                    ApprovedByDisplay = "Manager",
                },
                new DiscountAuditReportEntryRow
                {
                    DateDisplay = "20 Apr 2026",
                    TransactionId = "TXN-8432",
                    DiscountDisplay = "20%",
                    AmountValue = 124.00m,
                    AmountDisplay = FormatCurrency(124.00m, nfi),
                    Reason = "Loyalty Discount",
                    UserDisplay = "Mike R.",
                    ApprovedByDisplay = "Auto",
                },
                new DiscountAuditReportEntryRow
                {
                    DateDisplay = "20 Apr 2026",
                    TransactionId = "TXN-8401",
                    DiscountDisplay = "25%",
                    AmountValue = 156.25m,
                    AmountDisplay = FormatCurrency(156.25m, nfi),
                    Reason = "VIP Customer",
                    UserDisplay = "Lisa P.",
                    ApprovedByDisplay = "Manager",
                },
                new DiscountAuditReportEntryRow
                {
                    DateDisplay = "19 Apr 2026",
                    TransactionId = "TXN-8378",
                    DiscountDisplay = "10%",
                    AmountValue = 32.50m,
                    AmountDisplay = FormatCurrency(32.50m, nfi),
                    Reason = "Promo Code",
                    UserDisplay = "Tom H.",
                    ApprovedByDisplay = "Auto",
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
