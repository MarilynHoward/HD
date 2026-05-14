using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

public sealed class ReorderReportEntryRow
{
    public string ItemDisplay { get; set; } = "";
    public string CurrentDisplay { get; set; } = "";
    public string MinimumDisplay { get; set; } = "";
    public string ToOrderDisplay { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string EstCostDisplay { get; set; } = "";
    public decimal EstCostValue { get; set; }
}

/// <summary>Reorder report overlay — demo data until inventory facts are wired.</summary>
public partial class RptReorderReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<ReorderReportEntryRow> _rows = new();

    public RptReorderReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        foreach (var row in BuildDemoSeed(nfi))
            _rows.Add(row);

        ReorderRowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        decimal sumEstCost = 0;
        foreach (var r in _rows)
            sumEstCost += r.EstCostValue;

        TxtSummaryEstCostTotal.Text = FormatRand(sumEstCost, nfi);
    }

    private static NumberFormatInfo CloneReportNumberFormat()
    {
        var nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        nfi.NumberDecimalSeparator = ".";
        nfi.CurrencyDecimalSeparator = ".";
        nfi.PercentDecimalSeparator = ".";
        return nfi;
    }

    private static string FormatRand(decimal value, NumberFormatInfo nfi) => "R " + value.ToString("N2", nfi);

    private static string FormatQty(decimal value, NumberFormatInfo nfi) => value.ToString("0.0", nfi);

    private static ReorderReportEntryRow[] BuildDemoSeed(NumberFormatInfo nfi) =>
            new[]
            {
                Row("Chicken Breast (kg)", 15.0m, 50.0m, 85.0m, "Fresh Foods Ltd", 7650.00m, nfi),
                Row("Onions (kg)", 8.0m, 30.0m, 72.0m, "SA Produce Co", 1080.00m, nfi),
                Row("Tomatoes (kg)", 12.0m, 40.0m, 78.0m, "SA Produce Co", 1560.00m, nfi),
                Row("Milk (liters)", 20.0m, 60.0m, 100.0m, "Dairy Direct", 1500.00m, nfi),
                Row("Lettuce (heads)", 5.0m, 25.0m, 50.0m, "Fresh Foods Ltd", 750.00m, nfi),
            };

    private static ReorderReportEntryRow Row(
            string item,
            decimal current,
            decimal minimum,
            decimal toOrder,
            string supplier,
            decimal estCost,
            NumberFormatInfo nfi) =>
            new()
            {
                ItemDisplay = item,
                CurrentDisplay = FormatQty(current, nfi),
                MinimumDisplay = FormatQty(minimum, nfi),
                ToOrderDisplay = FormatQty(toOrder, nfi),
                Supplier = supplier,
                EstCostDisplay = FormatRand(estCost, nfi),
                EstCostValue = estCost,
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
