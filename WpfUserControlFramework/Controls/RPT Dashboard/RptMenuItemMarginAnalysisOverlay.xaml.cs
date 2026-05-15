using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

public sealed class MenuItemMarginRow
{
    public string ItemName { get; set; } = "";
    public string SellPrice { get; set; } = "";
    public string Cost { get; set; } = "";
    public string MarginAmt { get; set; } = "";
    public string MarginPct { get; set; } = "";
    public string Sales { get; set; } = "";
}

public partial class RptMenuItemMarginAnalysisOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<MenuItemMarginRow> _rows = new();

    public RptMenuItemMarginAnalysisOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        decimal sumSell = 0;
        decimal sumCost = 0;
        var rowCount = 0;
        var sumS = 0;
        void Add(string item, decimal sell, decimal cost, int sales)
        {
            rowCount++;
            sumSell += sell;
            sumCost += cost;
            sumS += sales;
            var m = sell - cost;
            _rows.Add(new MenuItemMarginRow
            {
                ItemName = item,
                SellPrice = sell.ToString("C2", nfi),
                Cost = cost.ToString("C2", nfi),
                MarginAmt = m.ToString("C2", nfi),
                MarginPct = sell > 0 ? (m / sell * 100m).ToString("N1", nfi) + "%" : "0%",
                Sales = sales.ToString("N0", nfi),
            });
        }

        Add("Beef Burger Deluxe", 165.00m, 68.20m, 89);
        Add("Chicken Caesar Salad", 95.00m, 32.30m, 76);
        Add("Seafood Platter", 285.00m, 142.50m, 34);
        Add("Vegetarian Pizza", 125.00m, 43.75m, 67);
        Add("Pasta Carbonara", 145.00m, 58.00m, 82);

        RowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);
        if (rowCount == 0)
        {
            FSellAvg.Text = string.Empty;
            FCostAvg.Text = string.Empty;
            FMrgAmt.Text = string.Empty;
            FMrgPct.Text = string.Empty;
            FSales.Text = string.Empty;
        }
        else
        {
            var aveSell = sumSell / rowCount;
            var aveCost = sumCost / rowCount;
            var mFromAve = aveSell - aveCost;
            FSellAvg.Text = aveSell.ToString("C2", nfi);
            FCostAvg.Text = aveCost.ToString("C2", nfi);
            FMrgAmt.Text = mFromAve.ToString("C2", nfi);
            FMrgPct.Text = aveSell > 0 ? (mFromAve / aveSell * 100m).ToString("N1", nfi) + "%" : "0%";
            FSales.Text = sumS.ToString("N0", nfi);
        }
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
