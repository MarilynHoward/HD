using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RestaurantPosWpf;

/// <summary>Supplier &amp; Procurement Reports picker — same layout as <see cref="RptBrowseStockReportsOverlay"/>.</summary>
public partial class RptBrowseProcurementReportsOverlay : UserControl
{
    private static readonly string[] ProcurementBrowseDisplayOrder =
    {
        RptDashboardMain.PurchasesBySupplierReportCode,
        RptDashboardMain.PurchaseOrderSummaryReportCode,
        RptDashboardMain.OutstandingOrdersReportCode,
        RptDashboardMain.DeliveryVarianceReportCode,
        RptDashboardMain.SupplierSpendAnalysisReportCode,
    };

    private readonly Action _onClose;
    private readonly Action<RptDashboardMain.ExecutableReportRef> _onPicked;

    private FrameworkElement? _widthHost;
    private SizeChangedEventHandler? _widthOnHostSized;

    public RptBrowseProcurementReportsOverlay(
            RptDashboardMain.BrowseGroupTile group,
            IReadOnlyCollection<string> recentlyUsedReportIds,
            Action onClose,
            Action<RptDashboardMain.ExecutableReportRef> onPicked)
    {
        ArgumentNullException.ThrowIfNull(group);
        _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
        _onPicked = onPicked ?? throw new ArgumentNullException(nameof(onPicked));

        InitializeComponent();

        RootGrid.DataContext = group;

        var fmt = Application.Current?.TryFindResource("Rpt.BrowseProcurement.AvailableCountFormat") as string ?? "{0} available reports";
        var list = (group.ReportsInGroup ?? Array.Empty<RptDashboardMain.ExecutableReportRef>()).ToList();
        var orderIndex = ProcurementBrowseDisplayOrder
                .Select((id, i) => (id, i))
                .ToDictionary(x => x.id, x => x.i, StringComparer.Ordinal);
        list.Sort((a, b) =>
        {
            var ia = orderIndex.TryGetValue(a.Id.Trim(), out var va) ? va : 99;
            var ib = orderIndex.TryGetValue(b.Id.Trim(), out var vb) ? vb : 99;
            var c = ia.CompareTo(ib);
            return c != 0 ? c : string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        });

        var recent = new HashSet<string>(
                (recentlyUsedReportIds ?? Array.Empty<string>()).Select(s => (s ?? string.Empty).Trim()),
                StringComparer.Ordinal);

        var rows = new ObservableCollection<RptSalesBrowsePickerRow>(
                list.Select(r => new RptSalesBrowsePickerRow(
                        r,
                        recent.Contains(r.Id.Trim()),
                        ResolveSubtitle(r.Id.Trim()))));

        ReportsList.ItemsSource = rows;
        TxtAvailableCount.Text = string.Format(CultureInfo.CurrentCulture, fmt, rows.Count);

        Loaded += (_, _) =>
        {
            Keyboard.Focus(this);
            HookWidthToParentControl();
            ApplyWidthFromHost();
        };
        Unloaded += (_, _) =>
        {
            if (_widthHost != null && _widthOnHostSized != null)
                _widthHost.SizeChanged -= _widthOnHostSized;
            _widthHost = null;
            _widthOnHostSized = null;
            RootChrome.ClearValue(WidthProperty);
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

    private void HookWidthToParentControl()
    {
        if (_widthHost != null || _widthOnHostSized != null)
            return;

        var host = ResolveOverlayWidthParent(this);
        if (host == null)
            return;

        _widthHost = host;
        _widthOnHostSized = (_, _) => ApplyWidthFromHost();
        _widthHost.SizeChanged += _widthOnHostSized;
    }

    private static FrameworkElement? ResolveOverlayWidthParent(DependencyObject self)
    {
        if (self is FrameworkElement fe && fe.Parent is FrameworkElement logicalParent)
            return logicalParent;

        for (var d = VisualTreeHelper.GetParent(self); d != null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ContentControl cc)
                return cc;
        }

        return null;
    }

    private void ApplyWidthFromHost()
    {
        if (_widthHost == null)
            return;

        var w = _widthHost.ActualWidth;
        if (w <= 0 || double.IsNaN(w) || double.IsInfinity(w))
            return;

        RootChrome.Width = w * 0.6;
    }

    private static string ResolveSubtitle(string reportCode)
    {
        var key = "Rpt.BrowseProcurement.Subtitle." + reportCode;
        if (Application.Current?.TryFindResource(key) is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        return string.Empty;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => _onClose();

    private void ReportRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not RptSalesBrowsePickerRow row)
            return;
        _onPicked(row.Report);
    }
}
