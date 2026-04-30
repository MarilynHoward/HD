using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>
/// One branch row in the Daily Sales Summary grid (formatted strings for binding).
/// </summary>
public sealed class DailySalesBranchRow
{
    public string Branch { get; set; } = "";
    public string Sales { get; set; } = "";
    public string Transactions { get; set; } = "";
    public string AvgTicket { get; set; } = "";
    public string Growth { get; set; } = "";
}

/// <summary>
/// Modal-style Daily Sales Summary surface (Rule #225-style shell) hosted inside <see cref="RptDashboardMain"/>.
/// </summary>
public partial class RptDailySalesSummaryOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<DailySalesBranchRow> _rows = new();

    public RptDailySalesSummaryOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        var sep = " | ";
        TxtFilterSummary.Text =
                _filters.DateRangeDisplay + sep + _filters.BranchDisplay + sep + _filters.ChannelDisplay + sep + _filters.UserRoleDisplay;
        TxtPeriod.Text = _filters.DateRangeDisplay;
    }

    private static string Dash => Application.Current.TryFindResource("Rpt.Report.DashEm") as string ?? "—";

    private void ReloadData()
    {
        TxtGenerated.Text = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);

        var prev = ComputePreviousWindow(_filters.RangeStart, _filters.RangeEnd);
        var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
        const int timeoutSeconds = 60;

        DataTable current;
        DataTable previous;
        try
        {
            current = App.aps.pda.GetDataTable(
                    cn,
                    App.aps.sql.SelectLocalRptDailySalesAggregatedByBranch(
                            _filters.RangeStart,
                            _filters.RangeEnd,
                            _filters.BranchFilterId,
                            _filters.ChannelFilterId,
                            _filters.UserRoleFilterId),
                    timeoutSeconds);

            previous = App.aps.pda.GetDataTable(
                    cn,
                    App.aps.sql.SelectLocalRptDailySalesAggregatedByBranch(
                            prev.PrevStart,
                            prev.PrevEnd,
                            _filters.BranchFilterId,
                            _filters.ChannelFilterId,
                            _filters.UserRoleFilterId),
                    timeoutSeconds);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[RptDailySalesSummaryOverlay] query failed: " + ex.Message);
            _rows.Clear();
            RowsItems.ItemsSource = _rows;
            TxtTotalRecords.Text = "0";
            TxtTotalSales.Text = Dash;
            TxtTotalTransactions.Text = Dash;
            TxtTotalAvgTicket.Text = Dash;
            TxtTotalGrowth.Text = Dash;
            return;
        }

        var prevSalesByBranch = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (DataRow r in previous.Rows)
        {
            var code = DbCellString(r, "branch_code").Trim();
            if (code.Length == 0)
                continue;
            prevSalesByBranch[code] = DbCellDecimal(r, "sum_sales");
        }

        _rows.Clear();
        decimal sumSales = 0;
        var sumTxn = 0;
        var growthRatios = new List<decimal>();

        foreach (DataRow r in current.Rows)
        {
            var code = DbCellString(r, "branch_code").Trim();
            var branchLabel = DbCellString(r, "branch_descr").Trim();
            if (branchLabel.Length == 0)
                branchLabel = code;

            var sales = DbCellDecimal(r, "sum_sales");
            var txn = DbCellInt(r, "sum_transactions");

            sumSales += sales;
            sumTxn += txn;

            var avgTicket = txn > 0 ? sales / txn : (decimal?)null;

            var growthText = Dash;
            if (prevSalesByBranch.TryGetValue(code, out var prevSales) && prevSales > 0)
            {
                var ratio = (sales - prevSales) / prevSales;
                growthText = ratio.ToString("P1", CultureInfo.CurrentCulture);
                growthRatios.Add(ratio);
            }

            _rows.Add(new DailySalesBranchRow
            {
                Branch = branchLabel,
                Sales = FormatCurrency(sales),
                Transactions = txn.ToString("N0", CultureInfo.CurrentCulture),
                AvgTicket = avgTicket.HasValue ? FormatCurrency(avgTicket.Value) : Dash,
                Growth = growthText,
            });
        }

        RowsItems.ItemsSource = _rows;

        TxtTotalRecords.Text = _rows.Count.ToString(CultureInfo.CurrentCulture);

        TxtTotalSales.Text = FormatCurrency(sumSales);
        TxtTotalTransactions.Text = sumTxn.ToString("N0", CultureInfo.CurrentCulture);
        TxtTotalAvgTicket.Text = sumTxn > 0 ? FormatCurrency(sumSales / sumTxn) : Dash;

        if (growthRatios.Count > 0)
        {
            var mean = growthRatios.Average();
            TxtTotalGrowth.Text = mean.ToString("P1", CultureInfo.CurrentCulture);
        }
        else
            TxtTotalGrowth.Text = Dash;
    }

    private static (DateOnly PrevStart, DateOnly PrevEnd) ComputePreviousWindow(DateOnly start, DateOnly end)
    {
        var inclusiveDays = end.DayNumber - start.DayNumber + 1;
        var prevEnd = start.AddDays(-1);
        var prevStart = prevEnd.AddDays(-(inclusiveDays - 1));
        return (prevStart, prevEnd);
    }

    private static string FormatCurrency(decimal value) =>
            value.ToString("C2", CultureInfo.CurrentCulture);

    private static object CellInsensitive(DataRow row, string columnName)
    {
        foreach (DataColumn c in row.Table.Columns)
        {
            if (string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                return row[c];
        }

        return DBNull.Value;
    }

    private static string DbCellString(DataRow row, string columnName)
    {
        var v = CellInsensitive(row, columnName);
        return v == DBNull.Value || v == null ? string.Empty : Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static decimal DbCellDecimal(DataRow row, string columnName)
    {
        var v = CellInsensitive(row, columnName);
        if (v == DBNull.Value || v == null)
            return 0m;

        try
        {
            return Convert.ToDecimal(v, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0m;
        }
    }

    private static int DbCellInt(DataRow row, string columnName)
    {
        var v = CellInsensitive(row, columnName);
        if (v == DBNull.Value || v == null)
            return 0;

        try
        {
            return Convert.ToInt32(v, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

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
