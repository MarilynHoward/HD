using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

public sealed class DeliveryChannelRow
{
    public string Channel { get; set; } = "";
    public string Revenue { get; set; } = "";
    public string Cost { get; set; } = "";
    public string GrossProfit { get; set; } = "";
    public string MarginPct { get; set; } = "";
    public string Commission { get; set; } = "";
}

public partial class RptDeliveryChannelProfitabilityOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<DeliveryChannelRow> _rows = new();

    public RptDeliveryChannelProfitabilityOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        decimal tr = 0, tc = 0, tcom = 0;
        void Add(string ch, decimal rev, decimal cost, decimal comm)
        {
            tr += rev;
            tc += cost;
            tcom += comm;
            var gp = rev - cost;
            _rows.Add(new DeliveryChannelRow
            {
                Channel = ch,
                Revenue = rev.ToString("C2", nfi),
                Cost = cost.ToString("C2", nfi),
                GrossProfit = gp.ToString("C2", nfi),
                MarginPct = rev > 0 ? (gp / rev * 100m).ToString("N1", nfi) + "%" : "0%",
                Commission = comm.ToString("C2", nfi),
            });
        }

        Add("Dine-In", 98_450, 39_380, 0);
        Add("Takeaway", 45_230, 18_092, 0);
        Add("Uber Eats", 28_670, 12_054.80m, 8601.00m);
        Add("Mr D Food", 16_080, 6753.60m, 4824.00m);

        RowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);
        FRev.Text = tr.ToString("C2", nfi);
        FCost.Text = tc.ToString("C2", nfi);
        var tgp = tr - tc;
        FGp.Text = tgp.ToString("C2", nfi);
        FMrg.Text = tr > 0 ? (tgp / tr * 100m).ToString("N1", nfi) + "%" : "—";
        FCom.Text = tcom.ToString("C2", nfi);
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
