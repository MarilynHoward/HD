using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>One row for Sales By Product (product name, quantity, revenue, avg price, margin %).</summary>
public sealed class SalesByProductBandRow
{
    public string Product { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Revenue { get; set; } = "";
    public string AvgPrice { get; set; } = "";
    public string Margin { get; set; } = "";
}

/// <summary>Sales By Product — shell and table layout match <see cref="RptSalesByCategoryOverlay"/>; demo rows until facts exist.</summary>
public partial class RptSalesByProductOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<SalesByProductBandRow> _rows = new();

    public RptSalesByProductOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        TxtGenerated.Text = Application.Current.TryFindResource("Rpt.Report.Generated.JustNow") as string ?? "Just now";
        var reportNfi = CloneReportNumberFormat();

        _rows.Clear();
        var sumQty = 0;
        decimal sumRevenue = 0;
        decimal weightedMarginPoints = 0;

        void Add(string product, int qty, decimal revenue, decimal avgPrice, int marginPercentPoints)
        {
            sumQty += qty;
            sumRevenue += revenue;
            weightedMarginPoints += marginPercentPoints / 100m * revenue;

            _rows.Add(new SalesByProductBandRow
            {
                Product = product,
                Quantity = qty.ToString("N0", reportNfi),
                Revenue = FormatCurrency(revenue, reportNfi),
                AvgPrice = FormatCurrency(avgPrice, reportNfi),
                Margin = FormatMarginWholePercent(marginPercentPoints, reportNfi),
            });
        }

        Add("Beef Burger", 156, 23_400.00m, 150.00m, 58);
        Add("Chicken Wings (12pc)", 134, 18_760.00m, 140.00m, 62);
        Add("Pizza Margherita", 98, 11_270.00m, 115.00m, 55);
        Add("Fish & Chips", 87, 13_050.00m, 150.00m, 48);
        Add("Greek Salad", 76, 6_080.00m, 80.00m, 68);

        ProductRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(reportNfi);

        TxtFooterQuantity.Text = sumQty.ToString("N0", reportNfi);
        TxtFooterRevenue.Text = FormatCurrency(sumRevenue, reportNfi);
        if (sumQty > 0)
            TxtFooterAvgPrice.Text = FormatCurrency(sumRevenue / sumQty, reportNfi);
        else
            TxtFooterAvgPrice.Text = Application.Current.TryFindResource("Rpt.Report.DashEm") as string ?? "—";

        if (sumRevenue > 0)
        {
            var totalMarginPct = weightedMarginPoints / sumRevenue * 100m;
            TxtFooterMargin.Text = FormatMarginWholePercentRounded(totalMarginPct, reportNfi);
        }
        else
        {
            TxtFooterMargin.Text = Application.Current.TryFindResource("Rpt.Report.DashEm") as string ?? "—";
        }
    }

    private static NumberFormatInfo CloneReportNumberFormat()
    {
        var nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        nfi.NumberDecimalSeparator = ".";
        nfi.CurrencyDecimalSeparator = ".";
        nfi.PercentDecimalSeparator = ".";
        return nfi;
    }

    private static string FormatCurrency(decimal value, NumberFormatInfo nfi) =>
            value.ToString("C2", nfi);

    private static string FormatMarginWholePercent(int marginPercentPoints, NumberFormatInfo nfi) =>
            marginPercentPoints.ToString("N0", nfi) + "%";

    private static string FormatMarginWholePercentRounded(decimal marginPercentPoints, NumberFormatInfo nfi) =>
            decimal.Round(marginPercentPoints, 0, MidpointRounding.AwayFromZero).ToString("N0", nfi) + "%";

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
