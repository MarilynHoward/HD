using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>One row for Sales By Order Type (label, order count, revenue, share, avg ticket).</summary>
public sealed class SalesByOrderTypeBandRow
{
    public string OrderType { get; set; } = "";
    public string Orders { get; set; } = "";
    public string Revenue { get; set; } = "";
    public string Percentage { get; set; } = "";
    public string AvgTicket { get; set; } = "";
}

/// <summary>Sales By Order Type — shell and table layout match <see cref="RptSalesByProductOverlay"/>; demo rows until facts exist.</summary>
public partial class RptSalesByOrderTypeOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<SalesByOrderTypeBandRow> _rows = new();

    public RptSalesByOrderTypeOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        var sumOrders = 0;
        decimal sumRevenue = 0;

        void Add(string orderType, int orders, decimal revenue, decimal percentOfTotalPoints, decimal avgTicket)
        {
            sumOrders += orders;
            sumRevenue += revenue;
            _rows.Add(new SalesByOrderTypeBandRow
            {
                OrderType = orderType,
                Orders = orders.ToString("N0", reportNfi),
                Revenue = FormatCurrency(revenue, reportNfi),
                Percentage = FormatPercentOneDecimal(percentOfTotalPoints, reportNfi),
                AvgTicket = FormatCurrency(avgTicket, reportNfi),
            });
        }

        Add("Dine-In", 456, 98_450.00m, 52.3m, 215.90m);
        Add("Takeaway", 312, 45_230.00m, 24.0m, 144.97m);
        Add("Delivery", 189, 28_670.00m, 15.2m, 151.69m);
        Add("Online", 98, 16_080.00m, 8.5m, 164.08m);

        OrderTypeRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(reportNfi);

        TxtFooterOrders.Text = sumOrders.ToString("N0", reportNfi);
        TxtFooterRevenue.Text = FormatCurrency(sumRevenue, reportNfi);
        TxtFooterPercentage.Text = sumRevenue > 0 ? FormatPercentOneDecimal(100m, reportNfi) : FormatPercentOneDecimal(0m, reportNfi);
        if (sumOrders > 0)
            TxtFooterAvgTicket.Text = FormatCurrency(sumRevenue / sumOrders, reportNfi);
        else
            TxtFooterAvgTicket.Text = Application.Current.TryFindResource("Rpt.Report.DashEm") as string ?? "—";
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

    private static string FormatPercentOneDecimal(decimal percentPoints, NumberFormatInfo nfi) =>
            percentPoints.ToString("0.0", nfi) + "%";

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
