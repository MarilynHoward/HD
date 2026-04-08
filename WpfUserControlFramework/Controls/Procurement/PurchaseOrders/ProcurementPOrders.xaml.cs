using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;

namespace RestaurantPosWpf
{
    public partial class ProcurementPOrders : UserControl
    {
        private readonly Action<string> _onOpenPurchaseOrder;
        private readonly Action _onClose;
        private List<PurchaseOrderListRow> _allRows = new();
        private readonly DispatcherTimer _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private string _searchQuery = string.Empty;

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

            _searchDebounce.Tick += SearchDebounce_Tick;

            Loaded += (_, _) => RefreshFromStore();
            Unloaded += (_, _) => _searchDebounce.Stop();
        }

        //public ProcurementPOrders(
        //    Action<string> onOpenPurchaseOrder,
        //    Action onClose)
        //{
        //    _onOpenPurchaseOrder = onOpenPurchaseOrder ?? throw new ArgumentNullException(nameof(onOpenPurchaseOrder));
        //    _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));

        //    InitializeComponent();
        //    Loaded += (_, _) => RefreshFromStore();
        //}

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

        private void SearchDebounce_Tick(object sender, EventArgs e)
        {
            _searchDebounce.Stop();

            _searchQuery = (SearchTextBox.Text ?? string.Empty).Trim();

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var search = (_searchQuery ?? string.Empty).Trim();
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

        //private void ApplyFilter()
        //{
        //    var search = (SearchTextBox.Text ?? string.Empty).Trim();
        //    var status = StatusFilterCombo.SelectedItem?.ToString() ?? "All Statuses";
        //    var supplier = SupplierFilterCombo.SelectedItem?.ToString() ?? "All Suppliers";

        //    IEnumerable<PurchaseOrderListRow> q = _allRows;

        //    if (!string.IsNullOrEmpty(search))
        //    {
        //        q = q.Where(r =>
        //            r.PONumber.Contains(search, StringComparison.OrdinalIgnoreCase)
        //            || r.SupplierName.Contains(search, StringComparison.OrdinalIgnoreCase));
        //    }

        //    if (status != "All Statuses")
        //    {
        //        q = q.Where(r => string.Equals(r.StatusDisplay, status, StringComparison.OrdinalIgnoreCase));
        //    }

        //    if (supplier != "All Suppliers")
        //    {
        //        q = q.Where(r => string.Equals(r.SupplierName, supplier, StringComparison.OrdinalIgnoreCase));
        //    }

        //    FullPurchaseOrderList.ItemsSource = q.ToList();
        //}

        //private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        //{
        //    if (!IsLoaded)
        //        return;

        //    ApplyFilter();
        //}

        private void SearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (SearchFocusOuter != null)
                SearchFocusOuter.Visibility = Visibility.Visible;

            if (SearchFocusInner != null)
                SearchFocusInner.Visibility = Visibility.Visible;

            if (SearchBoxBorder != null)
                SearchBoxBorder.BorderBrush = Brushes.Transparent;
        }

        private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (SearchFocusOuter != null)
                SearchFocusOuter.Visibility = Visibility.Collapsed;

            if (SearchFocusInner != null)
                SearchFocusInner.Visibility = Visibility.Collapsed;

            if (SearchBoxBorder != null)
                SearchBoxBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#CCC");
        }

        private void SearchBoxBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (SearchTextBox != null && SearchTextBox.IsKeyboardFocusWithin)
                    return;

                SearchTextBox.Focus();
                Keyboard.Focus(SearchTextBox);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        //private void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (!IsLoaded)
        //        return;

        //    ApplyFilter();
        //}

        private void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            _searchDebounce.Stop();
            _searchQuery = (SearchTextBox.Text ?? string.Empty).Trim();

            ApplyFilter();
        }

        //private void SupplierFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (!IsLoaded)
        //        return;

        //    ApplyFilter();
        //}

        private void SupplierFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            _searchDebounce.Stop();
            _searchQuery = (SearchTextBox.Text ?? string.Empty).Trim();

            ApplyFilter();
        }

        private void ListScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer innerScrollViewer)
                return;

            if (PageScrollViewer == null)
                return;

            bool innerHasVerticalScroll = innerScrollViewer.ScrollableHeight > 0;
            bool scrollingUp = e.Delta > 0;
            bool scrollingDown = e.Delta < 0;

            bool innerAtTop = innerScrollViewer.VerticalOffset <= 0;
            bool innerAtBottom = innerScrollViewer.VerticalOffset >= innerScrollViewer.ScrollableHeight;

            if (!innerHasVerticalScroll)
            {
                if (scrollingUp)
                    PageScrollViewer.LineUp();
                else if (scrollingDown)
                    PageScrollViewer.LineDown();

                e.Handled = true;
                return;
            }

            if (scrollingUp && innerAtTop)
            {
                PageScrollViewer.LineUp();
                e.Handled = true;
                return;
            }

            if (scrollingDown && innerAtBottom)
            {
                PageScrollViewer.LineDown();
                e.Handled = true;
                return;
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder — export wiring can connect to reporting later
        }

        private void CreateNewOrder_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder — same as dashboard create action when wired
        }

        private void PurchaseOrderRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string po } || string.IsNullOrWhiteSpace(po))
                return;

            e.Handled = true;
            _onOpenPurchaseOrder(po);
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
