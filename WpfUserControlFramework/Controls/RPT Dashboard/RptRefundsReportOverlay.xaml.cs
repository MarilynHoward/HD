using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>Demo-only status for refund rows until DB-backed data exists.</summary>
public enum RefundReportEntryStatus
{
    Approved,
    Pending,
}

/// <summary>One refund line item for the Refunds Report grid (display strings for binding).</summary>
public sealed class RefundReportEntryRow
{
    public string DateDisplay { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public decimal AmountValue { get; set; }
    public string AmountDisplay { get; set; } = "";
    public string Reason { get; set; } = "";
    public string UserDisplay { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string StatusVariant { get; set; } = "";
}

/// <summary>
/// Refunds Report overlay — same shell as <see cref="RptVoidsReportOverlay"/>; demo data only until fact sync is wired.
/// </summary>
public partial class RptRefundsReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<RefundReportEntryRow> _rows = new();
    private DateTime _generatedAt;

    public RptRefundsReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        var approved = Application.Current.TryFindResource("Rpt.Report.Status.Approved") as string ?? "Approved";
        var pending = Application.Current.TryFindResource("Rpt.Report.Status.Pending") as string ?? "Pending";

        _rows.Clear();
        foreach (var demo in BuildDemoSeed(nfi, approved, pending))
            _rows.Add(demo);

        RefundsRowsItems.ItemsSource = _rows;
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

    private static RefundReportEntryRow[] BuildDemoSeed(NumberFormatInfo nfi, string approvedLabel, string pendingLabel) =>
            new[]
            {
                new RefundReportEntryRow
                {
                    DateDisplay = "21 Apr 2026",
                    TransactionId = "TXN-8456",
                    AmountValue = 234.50m,
                    AmountDisplay = FormatCurrency(234.50m, nfi),
                    Reason = "Food Quality Issue",
                    UserDisplay = "Manager - Sarah M.",
                    StatusLabel = approvedLabel,
                    StatusVariant = nameof(RefundReportEntryStatus.Approved),
                },
                new RefundReportEntryRow
                {
                    DateDisplay = "21 Apr 2026",
                    TransactionId = "TXN-8489",
                    AmountValue = 156.00m,
                    AmountDisplay = FormatCurrency(156.00m, nfi),
                    Reason = "Wrong Order Delivered",
                    UserDisplay = "Cashier - John K.",
                    StatusLabel = approvedLabel,
                    StatusVariant = nameof(RefundReportEntryStatus.Approved),
                },
                new RefundReportEntryRow
                {
                    DateDisplay = "20 Apr 2026",
                    TransactionId = "TXN-8423",
                    AmountValue = 89.75m,
                    AmountDisplay = FormatCurrency(89.75m, nfi),
                    Reason = "Customer Complaint",
                    UserDisplay = "Manager - Sarah M.",
                    StatusLabel = pendingLabel,
                    StatusVariant = nameof(RefundReportEntryStatus.Pending),
                },
                new RefundReportEntryRow
                {
                    DateDisplay = "20 Apr 2026",
                    TransactionId = "TXN-8401",
                    AmountValue = 412.30m,
                    AmountDisplay = FormatCurrency(412.30m, nfi),
                    Reason = "Delivery Delay",
                    UserDisplay = "Supervisor - Mike R.",
                    StatusLabel = approvedLabel,
                    StatusVariant = nameof(RefundReportEntryStatus.Approved),
                },
                new RefundReportEntryRow
                {
                    DateDisplay = "19 Apr 2026",
                    TransactionId = "TXN-8378",
                    AmountValue = 67.25m,
                    AmountDisplay = FormatCurrency(67.25m, nfi),
                    Reason = "Incorrect Billing",
                    UserDisplay = "Cashier - Lisa P.",
                    StatusLabel = pendingLabel,
                    StatusVariant = nameof(RefundReportEntryStatus.Pending),
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
