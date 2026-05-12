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
using System.Windows.Media;

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
    /// <summary>Compared to portfolio average ticket (total sales / total transactions). Same visual rules as <see cref="GrowthVariant"/>.</summary>
    public string AvgTicketVariant { get; set; } = "Default";
    public string Growth { get; set; } = "";
    /// <summary>Default: dash or exactly 0% — no trend icon. Positive / Negative match Procurement dashboard trend styling.</summary>
    public string GrowthVariant { get; set; } = "Default";
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
        var sep = "   |   ";
        TxtFilterSummary.Text =
                _filters.DateRangeDisplay + sep + _filters.BranchDisplay + sep + _filters.ChannelDisplay + sep + _filters.UserRoleDisplay;
        TxtPeriod.Text = _filters.DateRangeDisplay;
    }

    private static string Dash => Application.Current.TryFindResource("Rpt.Report.DashEm") as string ?? "—";

    private void ReloadData()
    {
        TxtGenerated.Text = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
        var reportNfi = CloneReportNumberFormat();

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
            ApplyGrowthFooterVisual(Dash, "Default");
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
            var sales = DbCellDecimal(r, "sum_sales");
            var txn = DbCellInt(r, "sum_transactions");
            sumSales += sales;
            sumTxn += txn;
        }

        var portfolioAvgTicket = sumTxn > 0 ? sumSales / sumTxn : (decimal?)null;

        foreach (DataRow r in current.Rows)
        {
            var code = DbCellString(r, "branch_code").Trim();
            var branchLabel = DbCellString(r, "branch_descr").Trim();
            if (branchLabel.Length == 0)
                branchLabel = code;

            var sales = DbCellDecimal(r, "sum_sales");
            var txn = DbCellInt(r, "sum_transactions");

            var avgTicket = txn > 0 ? sales / txn : (decimal?)null;

            var avgTicketVariant = "Default";
            if (avgTicket.HasValue && portfolioAvgTicket.HasValue)
            {
                if (avgTicket.Value > portfolioAvgTicket.Value)
                    avgTicketVariant = "Positive";
                else if (avgTicket.Value < portfolioAvgTicket.Value)
                    avgTicketVariant = "Negative";
            }

            var growthText = Dash;
            var growthVariant = "Default";
            if (prevSalesByBranch.TryGetValue(code, out var prevSales) && prevSales > 0)
            {
                var ratio = (sales - prevSales) / prevSales;
                growthText = ratio.ToString("P1", reportNfi);
                growthRatios.Add(ratio);
                if (ratio > 0m)
                    growthVariant = "Positive";
                else if (ratio < 0m)
                    growthVariant = "Negative";
            }

            _rows.Add(new DailySalesBranchRow
            {
                Branch = branchLabel,
                Sales = FormatCurrency(sales, reportNfi),
                Transactions = txn.ToString("N0", reportNfi),
                AvgTicket = avgTicket.HasValue ? FormatCurrency(avgTicket.Value, reportNfi) : Dash,
                AvgTicketVariant = avgTicketVariant,
                Growth = growthText,
                GrowthVariant = growthVariant,
            });
        }

        RowsItems.ItemsSource = _rows;

        TxtTotalRecords.Text = _rows.Count.ToString(reportNfi);

        TxtTotalSales.Text = FormatCurrency(sumSales, reportNfi);
        TxtTotalTransactions.Text = sumTxn.ToString("N0", reportNfi);
        TxtTotalAvgTicket.Text = sumTxn > 0 ? FormatCurrency(sumSales / sumTxn, reportNfi) : Dash;

        if (growthRatios.Count > 0)
        {
            var mean = growthRatios.Average();
            var meanText = mean.ToString("P1", reportNfi);
            var v = "Default";
            if (mean > 0m)
                v = "Positive";
            else if (mean < 0m)
                v = "Negative";
            ApplyGrowthFooterVisual(meanText, v);
        }
        else
            ApplyGrowthFooterVisual(Dash, "Default");
    }

    private static (DateOnly PrevStart, DateOnly PrevEnd) ComputePreviousWindow(DateOnly start, DateOnly end)
    {
        var inclusiveDays = end.DayNumber - start.DayNumber + 1;
        var prevEnd = start.AddDays(-1);
        var prevStart = prevEnd.AddDays(-(inclusiveDays - 1));
        return (prevStart, prevEnd);
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

    private void ApplyGrowthFooterVisual(string text, string variant)
    {
        TxtTotalGrowth.Text = text;
        TotalGrowthUpIcon.Visibility = variant == "Positive" ? Visibility.Visible : Visibility.Collapsed;
        TotalGrowthDownIcon.Visibility = variant == "Negative" ? Visibility.Visible : Visibility.Collapsed;
        TxtTotalGrowth.Foreground = variant switch
        {
            "Positive" => (Brush)FindResource("Brush.RptReportPositiveForeground"),
            "Negative" => (Brush)FindResource("Brush.RptReportNegativeForeground"),
            _ => (Brush)FindResource("MainForeground"),
        };
    }

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
