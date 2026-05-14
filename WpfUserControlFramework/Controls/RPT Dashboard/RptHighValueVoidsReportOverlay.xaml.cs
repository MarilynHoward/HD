using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>Demo-only status for void rows until DB-backed data exists.</summary>
public enum HighVoidReportEntryStatus
{
    Approved,
    Pending,
}

/// <summary>
/// One void line item for the Voids Report grid (display strings for binding).
/// </summary>
public sealed class HighVoidReportEntryRow
{
    public string TimeDisplay { get; set; } = "";
    public string TransactionId { get; set; } = "";
    /// <summary>Raw amount for footer totals (not bound in row template).</summary>
    public decimal AmountValue { get; set; }
    public string AmountDisplay { get; set; } = "";
    public string Reason { get; set; } = "";
    public string UserDisplay { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    /// <summary>Binding key for badge styling: <c>Approved</c> or <c>Pending</c>.</summary>
    public string StatusVariant { get; set; } = "";
}

/// <summary>
/// Voids Report overlay — same shell as <see cref="RptDailySalesSummaryOverlay"/>; demo data only until fact sync is wired.
/// </summary>
public partial class RptHighValueVoidsReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<HighVoidReportEntryRow> _rows = new();
    private DateTime _generatedAt;

    public RptHighValueVoidsReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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

        VoidsRowsItems.ItemsSource = _rows;
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

    /// <summary>Static demo rows matching the design mock (replace with SQL when void facts are available).</summary>
    private static HighVoidReportEntryRow[] BuildDemoSeed(NumberFormatInfo nfi, string approvedLabel, string pendingLabel) =>
            new[]
            {
                new HighVoidReportEntryRow
                {
                    TimeDisplay = "14:32",
                    TransactionId = "TXN-8234",
                    AmountValue = 845.00m,
                    AmountDisplay = FormatCurrency(845.00m, nfi),
                    Reason = "Customer Request",
                    UserDisplay = "Sarah M.",
                    StatusLabel = approvedLabel,
                    StatusVariant = nameof(HighVoidReportEntryStatus.Approved),
                },
                new HighVoidReportEntryRow
                {
                    TimeDisplay = "15:18",
                    TransactionId = "TXN-8267",
                    AmountValue = 1234.50m,
                    AmountDisplay = FormatCurrency(1234.50m, nfi),
                    Reason = "Wrong Order",
                    UserDisplay = "John K.",
                    StatusLabel = approvedLabel,
                    StatusVariant = nameof(HighVoidReportEntryStatus.Approved),
                },
                new HighVoidReportEntryRow
                {
                    TimeDisplay = "16:45",
                    TransactionId = "TXN-8301",
                    AmountValue = 567.00m,
                    AmountDisplay = FormatCurrency(567.00m, nfi),
                    Reason = "Kitchen Error",
                    UserDisplay = "Mike R.",
                    StatusLabel = pendingLabel,
                    StatusVariant = nameof(HighVoidReportEntryStatus.Pending),
                },
                new HighVoidReportEntryRow
                {
                    TimeDisplay = "17:22",
                    TransactionId = "TXN-8339",
                    AmountValue = 2130.00m,
                    AmountDisplay = FormatCurrency(2130.00m, nfi),
                    Reason = "Payment Issue",
                    UserDisplay = "Sarah M.",
                    StatusLabel = approvedLabel,
                    StatusVariant = nameof(VoidReportEntryStatus.Approved),
                },
                new HighVoidReportEntryRow
                {
                    TimeDisplay = "18:05",
                    TransactionId = "TXN-8356",
                    AmountValue = 678.50m,
                    AmountDisplay = FormatCurrency(678.50m, nfi),
                    Reason = "Customer Complaint",
                    UserDisplay = "Lisa P.",
                    StatusLabel = pendingLabel,
                    StatusVariant = nameof(HighVoidReportEntryStatus.Pending),
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
