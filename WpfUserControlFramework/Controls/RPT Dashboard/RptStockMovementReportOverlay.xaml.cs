using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

public sealed class StockMovementReportEntryRow
{
    public string DateDisplay { get; set; } = "";
    public string ItemDisplay { get; set; } = "";
    public string MovementLabel { get; set; } = "";
    public string QuantityDisplay { get; set; } = "";
    public string CostDisplay { get; set; } = "";
    public string SupplierOrReason { get; set; } = "";
}

/// <summary>Stock movement report overlay — demo data until inventory facts are wired.</summary>
public partial class RptStockMovementReportOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<StockMovementReportEntryRow> _rows = new();

    public RptStockMovementReportOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        var inLabel = Application.Current.TryFindResource("Rpt.Report.Movement.In") as string ?? "In";
        var outLabel = Application.Current.TryFindResource("Rpt.Report.Movement.Out") as string ?? "Out";

        _rows.Clear();
        foreach (var row in BuildDemoSeed(nfi, inLabel, outLabel))
            _rows.Add(row);

        MovementRowsItems.ItemsSource = _rows;
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

    private static string FormatRand(decimal value, NumberFormatInfo nfi) => "R " + value.ToString("N2", nfi);

    private static string FormatQty(decimal value, NumberFormatInfo nfi) => value.ToString("0.0", nfi);

    private static string FormatDate(DateTime date) => date.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    private static StockMovementReportEntryRow[] BuildDemoSeed(NumberFormatInfo nfi, string inLabel, string outLabel) =>
            new[]
            {
                Row(new DateTime(2026, 4, 21), "Chicken Breast (kg)", inLabel, 25.0m, 2250.00m, "Fresh Foods Ltd", nfi),
                Row(new DateTime(2026, 4, 21), "Potatoes (kg)", outLabel, 18.5m, 277.50m, "Kitchen Usage", nfi),
                Row(new DateTime(2026, 4, 20), "Cooking Oil (liters)", inLabel, 40.0m, 3200.00m, "Metro Oil Co", nfi),
                Row(new DateTime(2026, 4, 20), "Onions (kg)", outLabel, 12.0m, 180.00m, "Kitchen Usage", nfi),
                Row(new DateTime(2026, 4, 19), "Tomatoes (kg)", inLabel, 30.0m, 600.00m, "SA Produce Co", nfi),
            };

    private static StockMovementReportEntryRow Row(
            DateTime date,
            string item,
            string movement,
            decimal quantity,
            decimal cost,
            string supplierOrReason,
            NumberFormatInfo nfi) =>
            new()
            {
                DateDisplay = FormatDate(date),
                ItemDisplay = item,
                MovementLabel = movement,
                QuantityDisplay = FormatQty(quantity, nfi),
                CostDisplay = FormatRand(cost, nfi),
                SupplierOrReason = supplierOrReason,
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
