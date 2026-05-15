using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

public sealed class PurchasesBySupplierRow
{
    public string Supplier { get; set; } = "";
    public string Orders { get; set; } = "";
    public string TotalSpend { get; set; } = "";
    public string AvgOrder { get; set; } = "";
    public string LastOrder { get; set; } = "";
}

public partial class RptPurchasesBySupplierOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<PurchasesBySupplierRow> _rows = new();

    public RptPurchasesBySupplierOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        var nfi = CloneReportNumberFormat();
        _rows.Clear();
        void Add(string s, int o, decimal spend, string last)
        {
            var avg = o > 0 ? spend / o : 0m;
            _rows.Add(new PurchasesBySupplierRow
            {
                Supplier = s,
                Orders = o.ToString("N0", nfi),
                TotalSpend = FormatCurrency(spend, nfi),
                AvgOrder = FormatCurrency(avg, nfi),
                LastOrder = last,
            });
        }

        Add("Fresh Foods Pty Ltd", 45, 125_450.00m, "21 Apr 2026");
        Add("Metro Meats", 38, 98_230.00m, "20 Apr 2026");
        Add("SA Produce Co", 52, 78_640.00m, "21 Apr 2026");
        Add("Dairy Direct", 28, 42_180.00m, "19 Apr 2026");
        Add("Premium Seafood", 18, 56_720.00m, "20 Apr 2026");

        RowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        var sumO = 45 + 38 + 52 + 28 + 18;
        var sumS = 125_450 + 98_230 + 78_640 + 42_180 + 56_720;
        TxtFooter1.Text = sumO.ToString("N0", nfi);
        TxtFooter2.Text = FormatCurrency(sumS, nfi);
        TxtFooter3.Text = sumO > 0 ? FormatCurrency(sumS / sumO, nfi) : "—";
        TxtFooter4.Text = string.Empty;
    }

    private static NumberFormatInfo CloneReportNumberFormat()
    {
        var nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        nfi.NumberDecimalSeparator = ".";
        nfi.CurrencyDecimalSeparator = ".";
        return nfi;
    }

    private static string FormatCurrency(decimal value, NumberFormatInfo nfi) => value.ToString("C2", nfi);

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
