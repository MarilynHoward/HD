using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf;

public sealed class SupplierSpendRow
{
    public string Supplier { get; set; } = "";
    public string Jan { get; set; } = "";
    public string Feb { get; set; } = "";
    public string Mar { get; set; } = "";
    public string Total { get; set; } = "";
    public string AvgMonthly { get; set; } = "";
}

public partial class RptSupplierSpendAnalysisOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<SupplierSpendRow> _rows = new();

    public RptSupplierSpendAnalysisOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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
        decimal sj = 0, sf = 0, sm = 0;
        void Add(string s, decimal j, decimal f, decimal m)
        {
            var t = j + f + m;
            var avg = t / 3m;
            sj += j;
            sf += f;
            sm += m;
            _rows.Add(new SupplierSpendRow
            {
                Supplier = s,
                Jan = j.ToString("C2", nfi),
                Feb = f.ToString("C2", nfi),
                Mar = m.ToString("C2", nfi),
                Total = t.ToString("C2", nfi),
                AvgMonthly = avg.ToString("C2", nfi),
            });
        }

        Add("Fresh Foods Pty Ltd", 42_340, 38_120, 45_000);
        Add("Metro Meats", 28_900, 31_200, 29_500);
        Add("SA Produce Co", 19_400, 21_000, 20_100);
        Add("Dairy Direct", 12_800, 13_200, 12_500);

        RowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);

        FJan.Text = sj.ToString("C2", nfi);
        FFeb.Text = sf.ToString("C2", nfi);
        FMar.Text = sm.ToString("C2", nfi);
        var grand = sj + sf + sm;
        FTot.Text = grand.ToString("C2", nfi);
        var sumAvgMonthly = grand / 3m;
        FAvg.Text = (_rows.Count > 0 ? sumAvgMonthly / _rows.Count : 0m).ToString("C2", nfi);
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
