using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>One order edit audit line item for the Order Edit Audit grid (display strings for binding).</summary>
public sealed class OrderEditAuditReportEntryRow
{
    public string TimeDisplay { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string Item { get; set; } = "";
    public string ChangeDisplay { get; set; } = "";
    public string UserDisplay { get; set; } = "";
    public string Reason { get; set; } = "";
}

/// <summary>
/// Order Edit Audit overlay — same shell as <see cref="RptVoidsReportOverlay"/>; demo data only until fact sync is wired.
/// </summary>
public partial class RptOrderEditAuditReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<OrderEditAuditReportEntryRow> _rows = new();
    private DateTime _generatedAt;

    public RptOrderEditAuditReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        foreach (var demo in BuildDemoSeed())
            _rows.Add(demo);

        OrderEditRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);
    }

    private static NumberFormatInfo CloneReportNumberFormat()
    {
        var nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        nfi.NumberDecimalSeparator = ".";
        nfi.CurrencyDecimalSeparator = ".";
        nfi.PercentDecimalSeparator = ".";
        return nfi;
    }

    private static OrderEditAuditReportEntryRow[] BuildDemoSeed() =>
            new[]
            {
                new OrderEditAuditReportEntryRow
                {
                    TimeDisplay = "16:45",
                    TransactionId = "ORD-2456",
                    Item = "Beef Burger",
                    ChangeDisplay = "Qty: 2 → 3",
                    UserDisplay = "Sarah M.",
                    Reason = "Customer Request",
                },
                new OrderEditAuditReportEntryRow
                {
                    TimeDisplay = "15:32",
                    TransactionId = "ORD-2423",
                    Item = "Pizza Margherita",
                    ChangeDisplay = "Added Toppings",
                    UserDisplay = "John K.",
                    Reason = "Customization",
                },
                new OrderEditAuditReportEntryRow
                {
                    TimeDisplay = "14:18",
                    TransactionId = "ORD-2389",
                    Item = "Greek Salad",
                    ChangeDisplay = "No Olives",
                    UserDisplay = "Mike R.",
                    Reason = "Allergy",
                },
                new OrderEditAuditReportEntryRow
                {
                    TimeDisplay = "13:05",
                    TransactionId = "ORD-2367",
                    Item = "Fish & Chips",
                    ChangeDisplay = "Price Override",
                    UserDisplay = "Manager - Lisa P.",
                    Reason = "Manager Approval",
                },
                new OrderEditAuditReportEntryRow
                {
                    TimeDisplay = "12:22",
                    TransactionId = "ORD-2334",
                    Item = "Chicken Wings",
                    ChangeDisplay = "Qty: 1 → 0 (Removed)",
                    UserDisplay = "Tom H.",
                    Reason = "Kitchen Out of Stock",
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
