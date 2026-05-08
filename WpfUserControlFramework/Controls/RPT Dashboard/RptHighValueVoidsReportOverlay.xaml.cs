using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>
/// High-Value Voids overlay — same shell and grid as <see cref="RptVoidsReportOverlay"/>; demo rows until void facts are wired.
/// </summary>
public partial class RptHighValueVoidsReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<VoidReportEntryRow> _rows = new();
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
        TxtGenerated.Text = FormatGeneratedCaption(_generatedAt);

        var nfi = CloneReportNumberFormat();
        var approved = Application.Current.TryFindResource("Rpt.Report.Status.Approved") as string ?? "Approved";
        var pending = Application.Current.TryFindResource("Rpt.Report.Status.Pending") as string ?? "Pending";

        _rows.Clear();
        foreach (var demo in BuildHighValueDemoSeed(nfi, approved, pending))
            _rows.Add(demo);

        HvvRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        var sumAmount = 0m;
        foreach (var r in _rows)
            sumAmount += r.AmountValue;

        TxtSummaryAmountTotal.Text = FormatCurrency(sumAmount, nfi);
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

    /// <summary>Demo rows for attention-count alignment (replace with SQL threshold filter when available).</summary>
    private static VoidReportEntryRow[] BuildHighValueDemoSeed(NumberFormatInfo nfi, string approvedLabel, string pendingLabel) =>
            new[]
            {
                new VoidReportEntryRow
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
                new VoidReportEntryRow
                {
                    TimeDisplay = "15:18",
                    TransactionId = "TXN-8267",
                    AmountValue = 1234.50m,
                    AmountDisplay = FormatCurrency(1234.50m, nfi),
                    Reason = "Wrong Order",
                    UserDisplay = "John K.",
                    StatusLabel = approvedLabel,
                    StatusVariant = nameof(VoidReportEntryStatus.Approved),
                },
                new VoidReportEntryRow
                {
                    TimeDisplay = "14:32",
                    TransactionId = "TXN-8234",
                    AmountValue = 845.00m,
                    AmountDisplay = FormatCurrency(845.00m, nfi),
                    Reason = "Customer Request",
                    UserDisplay = "Sarah M.",
                    StatusLabel = pendingLabel,
                    StatusVariant = nameof(VoidReportEntryStatus.Pending),
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
