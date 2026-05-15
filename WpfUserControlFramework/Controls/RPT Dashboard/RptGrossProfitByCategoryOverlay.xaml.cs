using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

public sealed class GrossProfitCategoryRow
{
    public string Category { get; set; } = "";
    public string Revenue { get; set; } = "";
    public string Cost { get; set; } = "";
    public string GrossProfit { get; set; } = "";
    public string MarginPct { get; set; } = "";
}

public partial class RptGrossProfitByCategoryOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<GrossProfitCategoryRow> _rows = new();

    public RptGrossProfitByCategoryOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        decimal tr = 0, tc = 0;
        void Add(string name, decimal rev, decimal cost, decimal gp, decimal marginPct)
        {
            tr += rev;
            tc += cost;
            _rows.Add(new GrossProfitCategoryRow
            {
                Category = name,
                Revenue = rev.ToString("C2", nfi),
                Cost = cost.ToString("C2", nfi),
                GrossProfit = gp.ToString("C2", nfi),
                MarginPct = marginPct.ToString("N1", nfi) + "%",
            });
        }

        Add("Mains", 98_450, 42_434.50m, 56_015.50m, 56.9m);
        Add("Starters", 32_180, 12_228.20m, 19_951.80m, 62.0m);
        Add("Beverages", 28_670, 8887.70m, 19_782.30m, 69.0m);
        Add("Desserts", 14_200, 6390.00m, 7810.00m, 55.0m);
        Add("Sides", 9200, 4416.00m, 4784.00m, 52.0m);

        RowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);
        FRev.Text = tr.ToString("C2", nfi);
        FCost.Text = tc.ToString("C2", nfi);
        var tgp = tr - tc;
        FGp.Text = tgp.ToString("C2", nfi);
        FMrg.Text = tr > 0 ? (tgp / tr * 100m).ToString("N1", nfi) + "%" : "—";
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

