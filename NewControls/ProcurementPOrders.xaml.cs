using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RestaurantPosWpf
{
    public partial class ProcurementPOrders : UserControl
    {
        private readonly Action<string> _onOpenPurchaseOrder;
        private readonly Action _onClose;
        private List<PurchaseOrderListRow> _allRows = new();

        public ProcurementPOrders() : this(_ => { }, () => { })
        {
        }

        public ProcurementPOrders(
            Action<string> onOpenPurchaseOrder,
            Action onClose)
        {
            _onOpenPurchaseOrder = onOpenPurchaseOrder ?? throw new ArgumentNullException(nameof(onOpenPurchaseOrder));
            _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));

            InitializeComponent();
            Loaded += (_, _) => RefreshFromStore();
        }

        public double GetFooterStripHeight()
        {
            if (FooterStrip == null)
                return 0.0;

            FooterStrip.UpdateLayout();
            return FooterStrip.ActualHeight;
        }

        private void RefreshFromStore()
        {
            var pos = ProcurementPurchaseOrderStore.GetAll();
            _allRows = pos
                .OrderByDescending(p => p.CreatedOn)
                .Select(ProcurementPurchaseOrderDisplay.ToListRow)
                .ToList();

            var suppliers = pos.Select(p => p.SupplierName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            suppliers.Insert(0, "All Suppliers");
            SupplierFilterCombo.ItemsSource = suppliers;
            SupplierFilterCombo.SelectedIndex = 0;

            var statuses = _allRows.Select(r => r.StatusDisplay).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            statuses.Insert(0, "All Statuses");
            StatusFilterCombo.ItemsSource = statuses;
            StatusFilterCombo.SelectedIndex = 0;

            UpdateSummaryCards(pos);
            ApplyFilter();
        }

        private void UpdateSummaryCards(IReadOnlyList<ProcurementPurchaseOrderDetail> all)
        {
            TotalOrdersText.Text = all.Count.ToString(CultureInfo.InvariantCulture);
            TotalValueText.Text = $"R{all.Sum(p => p.Total):#,##0.00}";

            var s = ProcurementPurchaseOrderDisplay.SummarizeOrdersInProgress(all);
            PendingApprovalSummaryText.Text = s.AwaitingApprovalCount.ToString(CultureInfo.InvariantCulture);

            var overdue = all.Count(ProcurementPurchaseOrderDisplay.IsPurchaseOrderOverdue);
            OverdueSummaryText.Text = overdue.ToString(CultureInfo.InvariantCulture);
            OverdueSummaryText.Foreground = overdue > 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")!)
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")!);
        }

        private void ApplyFilter()
        {
            var search = (SearchTextBox.Text ?? string.Empty).Trim();
            var status = StatusFilterCombo.SelectedItem?.ToString() ?? "All Statuses";
            var supplier = SupplierFilterCombo.SelectedItem?.ToString() ?? "All Suppliers";

            IEnumerable<PurchaseOrderListRow> q = _allRows;

            if (!string.IsNullOrEmpty(search))
            {
                q = q.Where(r =>
                    r.PONumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || r.SupplierName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (status != "All Statuses")
            {
                q = q.Where(r => string.Equals(r.StatusDisplay, status, StringComparison.OrdinalIgnoreCase));
            }

            if (supplier != "All Suppliers")
            {
                q = q.Where(r => string.Equals(r.SupplierName, supplier, StringComparison.OrdinalIgnoreCase));
            }

            FullPurchaseOrderList.ItemsSource = q.ToList();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ApplyFilter();
        }

        private void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ApplyFilter();
        }

        private void SupplierFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ApplyFilter();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder — export wiring can connect to reporting later
        }

        private void CreateNewOrder_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder — same as dashboard create action when wired
        }

        private void ViewRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string po } || string.IsNullOrWhiteSpace(po))
                return;

            _onOpenPurchaseOrder(po);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _onClose();
        }
    }
}
