using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

public sealed class PurchaseOrderSummaryRow
{
    public string PoNumber { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Items { get; set; } = "";
    public string Total { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string PoDate { get; set; } = "";
    /// <summary>Theme pill style: <c>Approved</c> (grey) or <c>Pending</c> (amber); see <see cref="RptReportApprovedPendingBadgeBorderStyle"/>.</summary>
    public string StatusVariant { get; set; } = "";
}

public partial class RptPurchaseOrderSummaryOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<PurchaseOrderSummaryRow> _rows = new();

    public RptPurchaseOrderSummaryOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        TxtGenerated.Text = RptReportGeneratedCaption.Format(DateTime.Now);
        var nfi = CloneNfi();
        _rows.Clear();

        void Add(string po, string sup, int items, decimal tot, char status, string d)
        {
            string statusText;
            string statusVariant;
            switch (status)
            {
                case 'P':
                    statusText = (string)(Application.Current?.TryFindResource("Rpt.Report.PoStatus.Pending") ?? "Pending");
                    statusVariant = "Pending";
                    break;
                case 'T':
                    statusText = (string)(Application.Current?.TryFindResource("Rpt.Report.PoStatus.InTransit") ?? "In Transit");
                    statusVariant = "Approved";
                    break;
                default:
                    statusText = (string)(Application.Current?.TryFindResource("Rpt.Report.PoStatus.Delivered") ?? "Delivered");
                    statusVariant = "Approved";
                    break;
            }

            _rows.Add(new PurchaseOrderSummaryRow
            {
                PoNumber = po,
                Supplier = sup,
                Items = items.ToString("N0", nfi),
                Total = tot.ToString("C2", nfi),
                StatusText = statusText,
                PoDate = d,
                StatusVariant = statusVariant,
            });
        }

        Add("PO-2456", "Fresh Foods Ltd", 12, 8450.00m, 'D', "21 Apr 2026");
        Add("PO-2455", "Metro Meats", 8, 12_340.00m, 'T', "20 Apr 2026");
        Add("PO-2454", "SA Produce Co", 15, 3680.00m, 'D', "20 Apr 2026");
        Add("PO-2453", "Dairy Direct", 6, 2450.00m, 'P', "19 Apr 2026");
        Add("PO-2452", "Premium Seafood", 10, 15_670.00m, 'D', "18 Apr 2026");

        RowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);
        var sumI = 12 + 8 + 15 + 6 + 10;
        var sumT = 8450m + 12_340m + 3680m + 2450m + 15_670m;
        TxtFooterItems.Text = sumI.ToString("N0", nfi);
        TxtFooterTotal.Text = sumT.ToString("C2", nfi);
    }

    private static NumberFormatInfo CloneNfi()
    {
        var nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        nfi.NumberDecimalSeparator = ".";
        nfi.CurrencyDecimalSeparator = ".";
        return nfi;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => ReloadData();

    private void Print_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show(
                    Application.Current.TryFindResource("Rpt.Report.StubPrintBody") as string ?? "",
                    Application.Current.TryFindResource("Rpt.Report.StubPrintTitle") as string ?? "Print",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

    private void Export_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show(
                    Application.Current.TryFindResource("Rpt.Report.StubExportBody") as string ?? "",
                    Application.Current.TryFindResource("Rpt.Report.StubExportTitle") as string ?? "Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

    private void Close_Click(object sender, RoutedEventArgs e) => _onClose();
}
