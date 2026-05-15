using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RestaurantPosWpf;

public sealed class OutstandingOrdersRow
{
    public string PoNumber { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string OrderDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string Total { get; set; } = "";
    public string DaysOverdue { get; set; } = "";
    public int DaysLate { get; set; }
    public Brush DaysForeground { get; set; } = System.Windows.SystemColors.ControlTextBrush;
}

public partial class RptOutstandingOrdersOverlay : UserControl
{
    private readonly RptDashboardFilterSnapshot _filters;
    private readonly Action _onClose;
    private readonly ObservableCollection<OutstandingOrdersRow> _rows = new();

    public RptOutstandingOrdersOverlay(RptDashboardFilterSnapshot filters, Action onClose)
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

    private static Brush MainFg()
    {
        if (Application.Current?.TryFindResource("MainForeground") is Brush b)
            return b;
        var s = new SolidColorBrush(Colors.Black);
        s.Freeze();
        return s;
    }

    private static Brush NegFg()
    {
        if (Application.Current?.TryFindResource("Brush.RptReportNegativeForeground") is Brush b)
            return b;
        var s = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        s.Freeze();
        return s;
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
        var main = MainFg();
        var neg = NegFg();
        _rows.Clear();
        decimal totalSum = 0;

        void Add(string po, string sup, decimal tot, int daysLate)
        {
            totalSum += tot;
            var daysText = daysLate.ToString("N0", nfi);
            _rows.Add(new OutstandingOrdersRow
            {
                PoNumber = po,
                Supplier = sup,
                OrderDate = "20 Apr 2026",
                DueDate = "22 Apr 2026",
                Total = tot.ToString("C2", nfi),
                DaysOverdue = daysText,
                DaysLate = daysLate,
                DaysForeground = daysLate > 0 ? neg : main,
            });
        }

        Add("PO-2455", "Metro Meats", 12_340.00m, 3);
        Add("PO-2454", "SA Produce Co", 3680.00m, 0);
        Add("PO-2453", "Dairy Direct", 2450.00m, 4);
        Add("PO-2452", "Premium Seafood", 15_670.00m, 0);

        RowsItems.ItemsSource = _rows;
        TxtTotalRecords.Text = _rows.Count.ToString(nfi);
        TxtFooterTotal.Text = totalSum.ToString("C2", nfi);
        if (_rows.Count == 0)
            TxtFooterAvgDaysOverdue.Text = string.Empty;
        else
        {
            var sumDays = 0;
            foreach (var r in _rows)
                sumDays += r.DaysLate;
            TxtFooterAvgDaysOverdue.Text = (sumDays / (double)_rows.Count).ToString("N1", nfi);
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
