using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

/// <summary>
/// One VAT band row (description + formatted currency columns).
/// </summary>
public sealed class VatSummaryBandRow
{
    public string Description { get; set; } = "";
    public string NetAmount { get; set; } = "";
    public string VatAmount { get; set; } = "";
    public string GrossAmount { get; set; } = "";
}

/// <summary>
/// VAT Summary report overlay — same shell pattern as <see cref="RptDailySalesSummaryOverlay"/>.
/// </summary>
public partial class RptVatSummaryOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<VatSummaryBandRow> _rows = new();

    public RptVatSummaryOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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

        var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
        const int timeoutSeconds = 60;

        DataTable dt;
        try
        {
            dt = App.aps.pda.GetDataTable(
                    cn,
                    App.aps.sql.SelectLocalRptVatAggregatedByVatRate(
                            _filters.RangeStart,
                            _filters.RangeEnd,
                            _filters.BranchFilterId,
                            _filters.ChannelFilterId,
                            _filters.UserRoleFilterId),
                    timeoutSeconds);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[RptVatSummaryOverlay] query failed: " + ex.Message);
            _rows.Clear();
            VatRowsItems.ItemsSource = _rows;
            TxtTotalRecords.Text = "0";
            TxtTotalNet.Text = Dash;
            TxtTotalVat.Text = Dash;
            TxtTotalGross.Text = Dash;
            return;
        }

        _rows.Clear();
        decimal sumNet = 0;
        decimal sumVat = 0;
        decimal sumGross = 0;

        foreach (DataRow r in dt.Rows)
        {
            var descr = DbCellString(r, "descr").Trim();
            if (descr.Length == 0)
                descr = DbCellString(r, "vat_rate_id");

            var net = DbCellDecimal(r, "sum_net_amount");
            var gross = DbCellDecimal(r, "sum_gross_amount");
            var vat = gross - net;

            sumNet += net;
            sumVat += vat;
            sumGross += gross;

            _rows.Add(new VatSummaryBandRow
            {
                Description = descr,
                NetAmount = FormatCurrency(net, reportNfi),
                VatAmount = FormatCurrency(vat, reportNfi),
                GrossAmount = FormatCurrency(gross, reportNfi),
            });
        }

        VatRowsItems.ItemsSource = _rows;

        TxtTotalRecords.Text = _rows.Count.ToString(reportNfi);
        TxtTotalNet.Text = FormatCurrency(sumNet, reportNfi);
        TxtTotalVat.Text = FormatCurrency(sumVat, reportNfi);
        TxtTotalGross.Text = FormatCurrency(sumGross, reportNfi);
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
