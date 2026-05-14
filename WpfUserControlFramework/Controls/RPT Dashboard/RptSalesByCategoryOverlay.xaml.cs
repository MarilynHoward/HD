using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>
/// One row for Sales By Category (category + counts, revenue, share, avg order).
/// </summary>
public sealed class SalesByCategoryBandRow
{
    public string Category { get; set; } = "";
    public string Items { get; set; } = "";
    public string Revenue { get; set; } = "";
    public string Percentage { get; set; } = "";
    public string AvgOrder { get; set; } = "";
}

/// <summary>
/// Sales By Category — shell and grid pattern match <see cref="RptVatSummaryOverlay"/>; demo rows until branch facts exist.
/// </summary>
public partial class RptSalesByCategoryOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<SalesByCategoryBandRow> _rows = new();

    public RptSalesByCategoryOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        var reportNfi = CloneReportNumberFormat();

        _rows.Clear();
        var sumItems = 0;
        decimal sumRevenue = 0;

        void Add(string category, int itemCount, decimal revenue, decimal percentOfTotalPoints, decimal avgOrder)
        {
            sumItems += itemCount;
            sumRevenue += revenue;
            _rows.Add(new SalesByCategoryBandRow
            {
                Category = category,
                Items = itemCount.ToString("N0", reportNfi),
                Revenue = FormatCurrency(revenue, reportNfi),
                Percentage = FormatPercentOneDecimal(percentOfTotalPoints, reportNfi),
                AvgOrder = FormatCurrency(avgOrder, reportNfi),
            });
        }

        // Client reference data (percentages sum to 100.0%).
        Add("Mains", 45, 98_450.00m, 52.3m, 185.00m);
        Add("Starters", 23, 32_180.00m, 17.1m, 95.00m);
        Add("Beverages", 18, 28_670.00m, 15.2m, 35.00m);
        Add("Desserts", 12, 18_450.00m, 9.8m, 65.00m);
        Add("Sides", 8, 10_680.00m, 5.6m, 45.00m);

        CategoryRowsItems.ItemsSource = _rows;

        TxtTotalRecords.Text = _rows.Count.ToString(reportNfi);

        TxtFooterItems.Text = sumItems.ToString("N0", reportNfi);
        TxtFooterRevenue.Text = FormatCurrency(sumRevenue, reportNfi);
        TxtFooterPercentage.Text = sumRevenue > 0 ? FormatPercentOneDecimal(100m, reportNfi) : FormatPercentOneDecimal(0m, reportNfi);
        if (sumItems > 0)
            TxtFooterAvgOrder.Text = FormatCurrency(sumRevenue / sumItems, reportNfi);
        else
            TxtFooterAvgOrder.Text = Application.Current.TryFindResource("Rpt.Report.DashEm") as string ?? "—";
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

    /// <summary>Formats a percentage as points (e.g. 52.3 → <c>52.3%</c>) with one decimal place.</summary>
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
