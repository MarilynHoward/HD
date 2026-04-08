using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Specialized;

namespace RestaurantPosWpf
{
    public partial class SupplierNewPO : UserControl
    {
        private enum PendingPoAction
        {
            None,
            ClosePage,
            DiscardAndStay
        }

        private bool _lineItemsHandlersWired;

        public ObservableCollection<PoLineItem> PoLineItems { get; private set; }

        public event EventHandler RequestClose;

        private readonly string _code;
        private readonly string _supplierName;
        private readonly string _emailContact;
        private readonly string _phoneNumber;
        private readonly string _paymentTerms;

        private bool _card2HandlersWired;

        private bool _pageDirtyHandlersWired;
        private bool _dirtyTrackingInitialized;
        private bool _suppressDirtyTracking;
        private bool _isDirty;
        private PoDirtySnapshot _cleanSnapshot;
        private PendingPoAction _pendingPoAction;

        public SupplierNewPO(string code, string supplierName, string emailContact, string phoneNumber, string paymentTerms)
        {
            InitializeComponent();

            _code = code ?? string.Empty;
            _supplierName = supplierName ?? string.Empty;
            _emailContact = emailContact ?? string.Empty;
            _phoneNumber = phoneNumber ?? string.Empty;
            _paymentTerms = paymentTerms ?? string.Empty;

            PoLineItems = new ObservableCollection<PoLineItem>();
            _pendingPoAction = PendingPoAction.None;

            Loaded += SupplierNewPO_Loaded;

            ApplySupplierCardValues();
        }

        private void SupplierNewPO_Loaded(object sender, RoutedEventArgs e)
        {
            Focus();

            if (!_card2HandlersWired)
            {
                _card2HandlersWired = true;

                if (dpPoOrderDate != null)
                    dpPoOrderDate.SelectedDateChanged += DpPoOrderDate_SelectedDateChanged;

                if (dpPoRequiredDate != null)
                    dpPoRequiredDate.SelectedDateChanged += DpPoRequiredDate_SelectedDateChanged;

                if (txtPoLeadTimeDays != null)
                {
                    txtPoLeadTimeDays.TextChanged += TxtPoLeadTimeDays_TextChanged;
                    txtPoLeadTimeDays.LostFocus += TxtPoLeadTimeDays_LostFocus;
                }
            }

            if (!_pageDirtyHandlersWired)
            {
                _pageDirtyHandlersWired = true;

                if (txtPoDeliveryStreetAddress != null)
                    txtPoDeliveryStreetAddress.TextChanged += PoDirtyField_TextChanged;

                if (txtPoDeliveryCity != null)
                    txtPoDeliveryCity.TextChanged += PoDirtyField_TextChanged;

                if (txtPoDeliveryPostalCode != null)
                    txtPoDeliveryPostalCode.TextChanged += PoDirtyField_TextChanged;

                if (txtPoDeliveryCountryRegion != null)
                    txtPoDeliveryCountryRegion.TextChanged += PoDirtyField_TextChanged;
            }

            if (!_lineItemsHandlersWired)
            {
                _lineItemsHandlersWired = true;

                if (dgPoLineItems != null)
                {
                    dgPoLineItems.ItemsSource = PoLineItems;
                    dgPoLineItems.PreviewKeyDown += DgPoLineItems_PreviewKeyDown;
                    dgPoLineItems.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(DgPoLineItems_ButtonBaseClick));
                }

                PoLineItems.CollectionChanged += PoLineItems_CollectionChanged;

                foreach (PoLineItem existingItem in PoLineItems)
                    SubscribeLineItem(existingItem);

                if (btnAddNewItemRecord != null)
                    btnAddNewItemRecord.Click += BtnAddNewItemRecord_Click;
            }

            UpdateCalculatedDelivery();
            InitializeDirtyTracking();
        }

        private void BtnAddNewItemRecord_Click(object sender, RoutedEventArgs e)
        {
            PoLineItem item = new PoLineItem
            {
                ItemCode = string.Empty,
                Description = string.Empty,
                Quantity = 1,
                Uom = "Each",
                UnitPrice = null
            };

            PoLineItems.Add(item);

            if (dgPoLineItems == null)
                return;

            dgPoLineItems.SelectedItem = item;
            dgPoLineItems.CurrentCell = new DataGridCellInfo(item, dgPoLineItems.Columns[0]);
            dgPoLineItems.ScrollIntoView(item);

            Dispatcher.BeginInvoke(new Action(delegate
            {
                dgPoLineItems.UpdateLayout();
                FocusLineItemEditor(item, 0);
            }), DispatcherPriority.Background);
        }

        private void DgPoLineItems_ButtonBaseClick(object sender, RoutedEventArgs e)
        {
            ButtonBase button = e.OriginalSource as ButtonBase;
            if (button == null)
                return;

            DataGridCell cell = FindVisualParent<DataGridCell>(button);
            if (cell == null)
                return;

            if (cell.Column == null)
                return;

            if (cell.Column.DisplayIndex != 6)
                return;

            PoLineItem item = cell.DataContext as PoLineItem;
            if (item == null)
                return;

            PoLineItems.Remove(item);
            e.Handled = true;
        }

        private void DgPoLineItems_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (dgPoLineItems == null)
                return;

            DataGridCellInfo currentCell = dgPoLineItems.CurrentCell;
            if (currentCell.Column == null)
                return;

            PoLineItem item = currentCell.Item as PoLineItem;
            if (item == null)
                return;

            Int32 currentDisplayIndex = currentCell.Column.DisplayIndex;
            Int32 nextEditableDisplayIndex = GetNextEditableDisplayIndex(currentDisplayIndex);

            dgPoLineItems.CommitEdit(DataGridEditingUnit.Cell, true);
            dgPoLineItems.CommitEdit(DataGridEditingUnit.Row, true);

            e.Handled = true;

            if (nextEditableDisplayIndex < 0)
                return;

            Dispatcher.BeginInvoke(new Action(delegate
            {
                FocusLineItemEditor(item, nextEditableDisplayIndex);
            }), DispatcherPriority.Background);
        }

        private Int32 GetNextEditableDisplayIndex(Int32 currentDisplayIndex)
        {
            switch (currentDisplayIndex)
            {
                case 0: return 1;
                case 1: return 2;
                case 2: return 3;
                case 3: return 4;
                default: return -1;
            }
        }

        private void FocusLineItemEditor(PoLineItem item, Int32 columnDisplayIndex)
        {
            if (dgPoLineItems == null || item == null)
                return;

            DataGridColumn column = GetColumnByDisplayIndex(dgPoLineItems, columnDisplayIndex);
            if (column == null)
                return;

            dgPoLineItems.SelectedItem = item;
            dgPoLineItems.CurrentCell = new DataGridCellInfo(item, column);
            dgPoLineItems.ScrollIntoView(item, column);
            dgPoLineItems.UpdateLayout();
            dgPoLineItems.BeginEdit();

            DataGridRow row = (DataGridRow)dgPoLineItems.ItemContainerGenerator.ContainerFromItem(item);
            if (row == null)
                return;

            DataGridCell cell = GetCell(dgPoLineItems, row, columnDisplayIndex);
            if (cell == null)
                return;

            cell.Focus();

            TextBox textBox = FindVisualChild<TextBox>(cell);
            if (textBox != null)
            {
                textBox.Focus();
                textBox.SelectAll();
                return;
            }

            SpinnerTextBox spinner = FindVisualChild<SpinnerTextBox>(cell);
            if (spinner != null)
            {
                spinner.Focus();
                spinner.SelectAll();
                return;
            }

            ComboBox combo = FindVisualChild<ComboBox>(cell);
            if (combo != null)
            {
                combo.Focus();
                return;
            }
        }

        private DataGridColumn GetColumnByDisplayIndex(DataGrid dataGrid, Int32 displayIndex)
        {
            if (dataGrid == null)
                return null;

            foreach (DataGridColumn column in dataGrid.Columns)
            {
                if (column.DisplayIndex == displayIndex)
                    return column;
            }

            return null;
        }

        private DataGridCell GetCell(DataGrid dataGrid, DataGridRow row, Int32 columnDisplayIndex)
        {
            if (dataGrid == null || row == null)
                return null;

            DataGridCellsPresenter presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
            {
                dataGrid.ScrollIntoView(row, dataGrid.Columns[columnDisplayIndex]);
                row.UpdateLayout();
                presenter = FindVisualChild<DataGridCellsPresenter>(row);
            }

            if (presenter == null)
                return null;

            DataGridCell cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnDisplayIndex) as DataGridCell;
            if (cell == null)
            {
                dataGrid.ScrollIntoView(row, dataGrid.Columns[columnDisplayIndex]);
                presenter.UpdateLayout();
                cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnDisplayIndex) as DataGridCell;
            }

            return cell;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            Int32 count = VisualTreeHelper.GetChildrenCount(parent);

            for (Int32 i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                T typedChild = child as T;
                if (typedChild != null)
                    return typedChild;

                T descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                T typed = child as T;
                if (typed != null)
                    return typed;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private void DpPoOrderDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCalculatedDelivery();
            ReevaluateDirtyState();
        }

        private void DpPoRequiredDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            ReevaluateDirtyState();
        }

        private void TxtPoLeadTimeDays_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCalculatedDelivery();
            ReevaluateDirtyState();
        }

        private void TxtPoLeadTimeDays_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateCalculatedDelivery();
            ReevaluateDirtyState();
        }

        private void UpdateCalculatedDelivery()
        {
            if (tbPoCalculatedDelivery == null)
                return;

            if (dpPoOrderDate == null)
            {
                tbPoCalculatedDelivery.Text = string.Empty;
                return;
            }

            DateTime? orderDate = dpPoOrderDate.SelectedDate;

            if (!orderDate.HasValue)
            {
                tbPoCalculatedDelivery.Text = string.Empty;
                return;
            }

            Int32 leadDays = 0;

            if (txtPoLeadTimeDays != null)
            {
                string raw = (txtPoLeadTimeDays.Text ?? string.Empty).Trim();

                Int32 parsed;
                if (Int32.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    leadDays = parsed;
                else
                    leadDays = 0;
            }

            if (leadDays < 0)
                leadDays = 0;

            DateTime delivery = orderDate.Value.Date.AddDays(leadDays);
            tbPoCalculatedDelivery.Text = delivery.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }

        private void ApplySupplierCardValues()
        {
            if (tbPoSupplierDisplay != null)
            {
                string display = string.IsNullOrWhiteSpace(_code)
                    ? _supplierName
                    : (string.IsNullOrWhiteSpace(_supplierName) ? _code : (_code + " - " + _supplierName));

                tbPoSupplierDisplay.Text = display ?? string.Empty;
            }

            if (tbPoEmailContact != null)
                tbPoEmailContact.Text = _emailContact ?? string.Empty;

            if (tbPoPhoneNumber != null)
                tbPoPhoneNumber.Text = _phoneNumber ?? string.Empty;

            if (tbPoPaymentTerms != null)
                tbPoPaymentTerms.Text = _paymentTerms ?? string.Empty;
        }

        private void InitializeDirtyTracking()
        {
            if (_dirtyTrackingInitialized)
            {
                ReevaluateDirtyState();
                return;
            }

            _cleanSnapshot = CaptureCurrentSnapshot();
            _dirtyTrackingInitialized = true;
            _pendingPoAction = PendingPoAction.None;

            ReevaluateDirtyState();
        }

        private void PoDirtyField_TextChanged(object sender, TextChangedEventArgs e)
        {
            ReevaluateDirtyState();
        }

        private void PoLineItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (object oldItem in e.OldItems)
                {
                    PoLineItem poLineItem = oldItem as PoLineItem;
                    if (poLineItem != null)
                        UnsubscribeLineItem(poLineItem);
                }
            }

            if (e.NewItems != null)
            {
                foreach (object newItem in e.NewItems)
                {
                    PoLineItem poLineItem = newItem as PoLineItem;
                    if (poLineItem != null)
                        SubscribeLineItem(poLineItem);
                }
            }

            ReevaluateDirtyState();
        }

        private void SubscribeLineItem(PoLineItem item)
        {
            if (item == null)
                return;

            item.PropertyChanged -= PoLineItem_PropertyChanged;
            item.PropertyChanged += PoLineItem_PropertyChanged;
        }

        private void UnsubscribeLineItem(PoLineItem item)
        {
            if (item == null)
                return;

            item.PropertyChanged -= PoLineItem_PropertyChanged;
        }

        private void PoLineItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.PropertyName))
            {
                ReevaluateDirtyState();
                return;
            }

            if (e.PropertyName == "ItemCode" ||
                e.PropertyName == "Description" ||
                e.PropertyName == "Quantity" ||
                e.PropertyName == "Uom" ||
                e.PropertyName == "UnitPrice")
            {
                ReevaluateDirtyState();
            }
        }

        private void ReevaluateDirtyState()
        {
            if (!_dirtyTrackingInitialized)
                return;

            if (_suppressDirtyTracking)
                return;

            PoDirtySnapshot currentSnapshot = CaptureCurrentSnapshot();
            bool isDirtyNow = !_cleanSnapshot.IsEquivalentTo(currentSnapshot);

            _isDirty = isDirtyNow;
            ApplyDirtyUiState();
        }

        private void ApplyDirtyUiState()
        {
            if (txtPoFooterUnsaved != null)
                txtPoFooterUnsaved.Visibility = _isDirty ? Visibility.Visible : Visibility.Collapsed;

            if (btnGeneratePo != null)
                btnGeneratePo.IsEnabled = _isDirty;
        }

        private PoDirtySnapshot CaptureCurrentSnapshot()
        {
            PoDirtySnapshot snapshot = new PoDirtySnapshot();

            snapshot.OrderDate = NormalizeDate(dpPoOrderDate != null ? dpPoOrderDate.SelectedDate : null);
            snapshot.RequiredDate = NormalizeDate(dpPoRequiredDate != null ? dpPoRequiredDate.SelectedDate : null);
            snapshot.LeadTimeDays = NormalizeText(txtPoLeadTimeDays != null ? txtPoLeadTimeDays.Text : string.Empty);

            snapshot.DeliveryStreetAddress = NormalizeText(txtPoDeliveryStreetAddress != null ? txtPoDeliveryStreetAddress.Text : string.Empty);
            snapshot.DeliveryCity = NormalizeText(txtPoDeliveryCity != null ? txtPoDeliveryCity.Text : string.Empty);
            snapshot.DeliveryPostalCode = NormalizeText(txtPoDeliveryPostalCode != null ? txtPoDeliveryPostalCode.Text : string.Empty);
            snapshot.DeliveryCountryRegion = NormalizeText(txtPoDeliveryCountryRegion != null ? txtPoDeliveryCountryRegion.Text : string.Empty);

            foreach (PoLineItem item in PoLineItems)
            {
                if (!IsMeaningfulLineItem(item))
                    continue;

                snapshot.LineItems.Add(CaptureLineItemSnapshot(item));
            }

            return snapshot;
        }

        private PoLineItemSnapshot CaptureLineItemSnapshot(PoLineItem item)
        {
            PoLineItemSnapshot snapshot = new PoLineItemSnapshot();

            if (item == null)
                return snapshot;

            snapshot.ItemCode = NormalizeText(item.ItemCode);
            snapshot.Description = NormalizeText(item.Description);
            snapshot.Quantity = item.Quantity;
            snapshot.Uom = NormalizeText(item.Uom);
            snapshot.UnitPrice = item.UnitPrice;

            return snapshot;
        }

        private void RestoreFromCleanSnapshot()
        {
            if (_cleanSnapshot == null)
                return;

            _suppressDirtyTracking = true;

            try
            {
                if (dpPoOrderDate != null)
                    dpPoOrderDate.SelectedDate = ParseNormalizedDate(_cleanSnapshot.OrderDate);

                if (dpPoRequiredDate != null)
                    dpPoRequiredDate.SelectedDate = ParseNormalizedDate(_cleanSnapshot.RequiredDate);

                if (txtPoLeadTimeDays != null)
                    txtPoLeadTimeDays.Text = _cleanSnapshot.LeadTimeDays ?? string.Empty;

                if (txtPoDeliveryStreetAddress != null)
                    txtPoDeliveryStreetAddress.Text = _cleanSnapshot.DeliveryStreetAddress ?? string.Empty;

                if (txtPoDeliveryCity != null)
                    txtPoDeliveryCity.Text = _cleanSnapshot.DeliveryCity ?? string.Empty;

                if (txtPoDeliveryPostalCode != null)
                    txtPoDeliveryPostalCode.Text = _cleanSnapshot.DeliveryPostalCode ?? string.Empty;

                if (txtPoDeliveryCountryRegion != null)
                    txtPoDeliveryCountryRegion.Text = _cleanSnapshot.DeliveryCountryRegion ?? string.Empty;

                RebuildLineItemsFromSnapshot(_cleanSnapshot);

                UpdateCalculatedDelivery();

                if (dgPoLineItems != null)
                {
                    dgPoLineItems.SelectedItem = null;
                    dgPoLineItems.CurrentCell = new DataGridCellInfo();
                    dgPoLineItems.UpdateLayout();
                }
            }
            finally
            {
                _suppressDirtyTracking = false;
            }

            _isDirty = false;
            ApplyDirtyUiState();

            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (btnDiscardDraft != null)
                    btnDiscardDraft.Focus();
                else
                    Focus();
            }), DispatcherPriority.Background);
        }

        private void RebuildLineItemsFromSnapshot(PoDirtySnapshot snapshot)
        {
            if (snapshot == null)
                return;

            PoLineItems.Clear();

            foreach (PoLineItemSnapshot lineItemSnapshot in snapshot.LineItems)
            {
                if (lineItemSnapshot == null)
                    continue;

                PoLineItem item = new PoLineItem
                {
                    ItemCode = lineItemSnapshot.ItemCode ?? string.Empty,
                    Description = lineItemSnapshot.Description ?? string.Empty,
                    Quantity = lineItemSnapshot.Quantity,
                    Uom = string.IsNullOrWhiteSpace(lineItemSnapshot.Uom) ? "Each" : lineItemSnapshot.Uom,
                    UnitPrice = lineItemSnapshot.UnitPrice
                };

                PoLineItems.Add(item);
            }
        }

        private bool IsMeaningfulLineItem(PoLineItem item)
        {
            if (item == null)
                return false;

            return !IsDefaultLineItem(item);
        }

        private bool IsDefaultLineItem(PoLineItem item)
        {
            if (item == null)
                return true;

            string itemCode = NormalizeText(item.ItemCode);
            string description = NormalizeText(item.Description);
            string uom = NormalizeText(item.Uom);
            string unitPrice = NormalizeUnitPriceForDirtyCompare(item.UnitPrice);

            return itemCode.Length == 0 &&
                   description.Length == 0 &&
                   item.Quantity == 1 &&
                   string.Equals(uom, "Each", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(unitPrice, "0.00", StringComparison.Ordinal);
        }

        private string NormalizeText(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private string NormalizeDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.Date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private DateTime? ParseNormalizedDate(string value)
        {
            string normalized = NormalizeText(value);
            if (normalized.Length == 0)
                return null;

            DateTime parsed;
            if (DateTime.TryParseExact(normalized, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed.Date;

            return null;
        }

        private string NormalizeUnitPriceForDirtyCompare(Decimal? value)
        {
            if (!value.HasValue)
                return "0.00";

            if (value.Value == 0m)
                return "0.00";

            return value.Value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private sealed class PoLineItemSnapshot
        {
            public string ItemCode { get; set; }
            public string Description { get; set; }
            public Int32 Quantity { get; set; }
            public string Uom { get; set; }
            public Decimal? UnitPrice { get; set; }

            public PoLineItemSnapshot()
            {
                ItemCode = string.Empty;
                Description = string.Empty;
                Quantity = 1;
                Uom = "Each";
                UnitPrice = null;
            }

            public bool IsEquivalentTo(PoLineItemSnapshot other)
            {
                if (other == null)
                    return false;

                if (!string.Equals(ItemCode ?? string.Empty, other.ItemCode ?? string.Empty, StringComparison.Ordinal))
                    return false;

                if (!string.Equals(Description ?? string.Empty, other.Description ?? string.Empty, StringComparison.Ordinal))
                    return false;

                if (Quantity != other.Quantity)
                    return false;

                if (!string.Equals(Uom ?? string.Empty, other.Uom ?? string.Empty, StringComparison.Ordinal))
                    return false;

                if (!string.Equals(
                        NormalizeComparableUnitPrice(UnitPrice),
                        NormalizeComparableUnitPrice(other.UnitPrice),
                        StringComparison.Ordinal))
                    return false;

                return true;
            }

            private static string NormalizeComparableUnitPrice(Decimal? value)
            {
                if (!value.HasValue)
                    return "0.00";

                if (value.Value == 0m)
                    return "0.00";

                return value.Value.ToString("0.00", CultureInfo.InvariantCulture);
            }
        }

        private sealed class PoDirtySnapshot
        {
            public string OrderDate { get; set; }
            public string RequiredDate { get; set; }
            public string LeadTimeDays { get; set; }
            public string DeliveryStreetAddress { get; set; }
            public string DeliveryCity { get; set; }
            public string DeliveryPostalCode { get; set; }
            public string DeliveryCountryRegion { get; set; }

            public Collection<PoLineItemSnapshot> LineItems { get; private set; }

            public PoDirtySnapshot()
            {
                OrderDate = string.Empty;
                RequiredDate = string.Empty;
                LeadTimeDays = string.Empty;
                DeliveryStreetAddress = string.Empty;
                DeliveryCity = string.Empty;
                DeliveryPostalCode = string.Empty;
                DeliveryCountryRegion = string.Empty;
                LineItems = new Collection<PoLineItemSnapshot>();
            }

            public bool IsEquivalentTo(PoDirtySnapshot other)
            {
                if (other == null)
                    return false;

                if (!string.Equals(OrderDate, other.OrderDate, StringComparison.Ordinal))
                    return false;

                if (!string.Equals(RequiredDate, other.RequiredDate, StringComparison.Ordinal))
                    return false;

                if (!string.Equals(LeadTimeDays, other.LeadTimeDays, StringComparison.Ordinal))
                    return false;

                if (!string.Equals(DeliveryStreetAddress, other.DeliveryStreetAddress, StringComparison.Ordinal))
                    return false;

                if (!string.Equals(DeliveryCity, other.DeliveryCity, StringComparison.Ordinal))
                    return false;

                if (!string.Equals(DeliveryPostalCode, other.DeliveryPostalCode, StringComparison.Ordinal))
                    return false;

                if (!string.Equals(DeliveryCountryRegion, other.DeliveryCountryRegion, StringComparison.Ordinal))
                    return false;

                if (LineItems.Count != other.LineItems.Count)
                    return false;

                for (Int32 i = 0; i < LineItems.Count; i++)
                {
                    if (!LineItems[i].IsEquivalentTo(other.LineItems[i]))
                        return false;
                }

                return true;
            }
        }

        private bool IsPoUnsavedConfirmOverlayVisible()
        {
            return pnlPoUnsavedConfirmOverlay != null &&
                   pnlPoUnsavedConfirmOverlay.Visibility == Visibility.Visible;
        }

        private void ShowPoUnsavedConfirmOverlay()
        {
            if (pnlPoUnsavedConfirmOverlay == null)
                return;

            pnlPoUnsavedConfirmOverlay.Visibility = Visibility.Visible;

            if (btnPoUnsavedNo != null)
                btnPoUnsavedNo.Focus();
        }

        private void HidePoUnsavedConfirmOverlay()
        {
            if (pnlPoUnsavedConfirmOverlay == null)
                return;

            pnlPoUnsavedConfirmOverlay.Visibility = Visibility.Collapsed;
        }

        private void AttemptCloseSupplierNewPo()
        {
            if (IsPoUnsavedConfirmOverlayVisible())
                return;

            if (_isDirty)
            {
                _pendingPoAction = PendingPoAction.ClosePage;
                ShowPoUnsavedConfirmOverlay();
                return;
            }

            _pendingPoAction = PendingPoAction.None;
            RaiseRequestClose();
        }

        private void AttemptDiscardDraftSupplierNewPo()
        {
            if (IsPoUnsavedConfirmOverlayVisible())
                return;

            if (_isDirty)
            {
                _pendingPoAction = PendingPoAction.DiscardAndStay;
                ShowPoUnsavedConfirmOverlay();
                return;
            }

            _pendingPoAction = PendingPoAction.None;
        }

        private void PoUnsavedOverlayScrim_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            HidePoUnsavedConfirmOverlay();
            _pendingPoAction = PendingPoAction.None;
        }

        private void PoUnsavedOverlayCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void BtnPoUnsavedNo_Click(object sender, RoutedEventArgs e)
        {
            HidePoUnsavedConfirmOverlay();
            _pendingPoAction = PendingPoAction.None;
        }

        private void BtnPoUnsavedYes_Click(object sender, RoutedEventArgs e)
        {
            PendingPoAction pendingAction = _pendingPoAction;

            HidePoUnsavedConfirmOverlay();
            _pendingPoAction = PendingPoAction.None;

            if (pendingAction == PendingPoAction.DiscardAndStay)
            {
                RestoreFromCleanSnapshot();
                return;
            }

            if (pendingAction == PendingPoAction.ClosePage)
            {
                RaiseRequestClose();
                return;
            }
        }

        private void RaiseRequestClose()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            AttemptCloseSupplierNewPo();
        }

        private void BtnDiscardDraft_Click(object sender, RoutedEventArgs e)
        {
            AttemptDiscardDraftSupplierNewPo();
        }

        private void SupplierNewPO_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            e.Handled = true;

            if (IsPoUnsavedConfirmOverlayVisible())
            {
                HidePoUnsavedConfirmOverlay();
                _pendingPoAction = PendingPoAction.None;
                return;
            }

            AttemptCloseSupplierNewPo();
        }

        private void BtnGeneratePo_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder: later validation + create workflow
            // Intentionally no host manipulation here.
        }
    }

    public class PoLineItem : INotifyPropertyChanged
    {
        private string _itemCode;
        private string _description;
        private Int32 _quantity;
        private string _uom;
        private Decimal? _unitPrice;
        private Decimal _rowTotal;

        public string ItemCode
        {
            get { return _itemCode; }
            set { SetField(ref _itemCode, value); }
        }

        public string Description
        {
            get { return _description; }
            set { SetField(ref _description, value); }
        }

        public Int32 Quantity
        {
            get { return _quantity; }
            set
            {
                if (SetField(ref _quantity, value))
                    RecalculateRowTotal();
            }
        }

        public string Uom
        {
            get { return _uom; }
            set { SetField(ref _uom, value); }
        }

        public Decimal? UnitPrice
        {
            get { return _unitPrice; }
            set
            {
                if (SetField(ref _unitPrice, value))
                    RecalculateRowTotal();
            }
        }

        public Decimal RowTotal
        {
            get { return _rowTotal; }
            private set
            {
                if (SetField(ref _rowTotal, value))
                    OnPropertyChanged("RowTotalDisplay");
            }
        }

        public string RowTotalDisplay
        {
            get { return string.Format(CultureInfo.InvariantCulture, "R {0:0.00}", RowTotal); }
        }

        public PoLineItem()
        {
            _itemCode = string.Empty;
            _description = string.Empty;
            _quantity = 1;
            _uom = "Each";
            _unitPrice = null;

            RecalculateRowTotal();
        }

        private void RecalculateRowTotal()
        {
            Decimal price = UnitPrice.HasValue ? UnitPrice.Value : 0m;
            Decimal total = Convert.ToDecimal(Quantity, CultureInfo.InvariantCulture) * price;
            RowTotal = total;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (object.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(string? propertyName)
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


//using System;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Input;
//using System.Globalization;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Runtime.CompilerServices;
//using System.Windows.Controls.Primitives;
//using System.Windows.Media;
//using System.Windows.Threading;
//using System.Collections.Specialized;

//namespace RestaurantPosWpf
//{
//    public partial class SupplierNewPO : UserControl
//    {

//        private bool _lineItemsHandlersWired;

//        public ObservableCollection<PoLineItem> PoLineItems { get; private set; }

//        public event EventHandler RequestClose;

//        private readonly string _code;
//        private readonly string _supplierName;
//        private readonly string _emailContact;
//        private readonly string _phoneNumber;
//        private readonly string _paymentTerms;

//        private bool _card2HandlersWired;

//        private bool _pageDirtyHandlersWired;
//        private bool _dirtyTrackingInitialized;
//        private bool _isDirty;
//        private PoDirtySnapshot _cleanSnapshot;

//        public SupplierNewPO(string code, string supplierName, string emailContact, string phoneNumber, string paymentTerms)
//        {
//            InitializeComponent();

//            _code = code ?? string.Empty;
//            _supplierName = supplierName ?? string.Empty;
//            _emailContact = emailContact ?? string.Empty;
//            _phoneNumber = phoneNumber ?? string.Empty;
//            _paymentTerms = paymentTerms ?? string.Empty;

//            PoLineItems = new ObservableCollection<PoLineItem>();

//            Loaded += SupplierNewPO_Loaded;

//            ApplySupplierCardValues();
//        }

//        private void SupplierNewPO_Loaded(object sender, RoutedEventArgs e)
//        {
//            Focus();

//            // Wire schedule/address change events once (Loaded can fire more than once)
//            if (!_card2HandlersWired)
//            {
//                _card2HandlersWired = true;

//                if (dpPoOrderDate != null)
//                    dpPoOrderDate.SelectedDateChanged += DpPoOrderDate_SelectedDateChanged;

//                if (dpPoRequiredDate != null)
//                    dpPoRequiredDate.SelectedDateChanged += DpPoRequiredDate_SelectedDateChanged;

//                if (txtPoLeadTimeDays != null)
//                {
//                    txtPoLeadTimeDays.TextChanged += TxtPoLeadTimeDays_TextChanged; 
//                    txtPoLeadTimeDays.LostFocus += TxtPoLeadTimeDays_LostFocus;
//                }
//            }

//            if (!_pageDirtyHandlersWired)
//            {
//                _pageDirtyHandlersWired = true;

//                if (txtPoDeliveryStreetAddress != null)
//                    txtPoDeliveryStreetAddress.TextChanged += PoDirtyField_TextChanged;

//                if (txtPoDeliveryCity != null)
//                    txtPoDeliveryCity.TextChanged += PoDirtyField_TextChanged;

//                if (txtPoDeliveryPostalCode != null)
//                    txtPoDeliveryPostalCode.TextChanged += PoDirtyField_TextChanged;

//                if (txtPoDeliveryCountryRegion != null)
//                    txtPoDeliveryCountryRegion.TextChanged += PoDirtyField_TextChanged;
//            }

//            if (!_lineItemsHandlersWired)
//            {
//                _lineItemsHandlersWired = true;

//                if (dgPoLineItems != null)
//                {
//                    dgPoLineItems.ItemsSource = PoLineItems;
//                    dgPoLineItems.PreviewKeyDown += DgPoLineItems_PreviewKeyDown;
//                    dgPoLineItems.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(DgPoLineItems_ButtonBaseClick));
//                }

//                PoLineItems.CollectionChanged += PoLineItems_CollectionChanged;

//                foreach (PoLineItem existingItem in PoLineItems)
//                    SubscribeLineItem(existingItem);

//                if (btnAddNewItemRecord != null)
//                    btnAddNewItemRecord.Click += BtnAddNewItemRecord_Click;
//            }


//            UpdateCalculatedDelivery();
//            InitializeDirtyTracking();
//        }

//        private void BtnAddNewItemRecord_Click(object sender, RoutedEventArgs e)
//        {
//            PoLineItem item = new PoLineItem
//            {
//                ItemCode = string.Empty,
//                Description = string.Empty,
//                Quantity = 1,
//                Uom = "Each",
//                UnitPrice = null
//            };

//            PoLineItems.Add(item);

//            if (dgPoLineItems == null)
//                return;

//            dgPoLineItems.SelectedItem = item;
//            dgPoLineItems.CurrentCell = new DataGridCellInfo(item, dgPoLineItems.Columns[0]);
//            dgPoLineItems.ScrollIntoView(item);

//            Dispatcher.BeginInvoke(new Action(delegate
//            {
//                dgPoLineItems.UpdateLayout();
//                FocusLineItemEditor(item, 0);
//            }), DispatcherPriority.Background);
//        }

//        private void DgPoLineItems_ButtonBaseClick(object sender, RoutedEventArgs e)
//        {
//            ButtonBase button = e.OriginalSource as ButtonBase;
//            if (button == null)
//                return;

//            DataGridCell cell = FindVisualParent<DataGridCell>(button);
//            if (cell == null)
//                return;

//            if (cell.Column == null)
//                return;

//            // Last column = delete column
//            if (cell.Column.DisplayIndex != 6)
//                return;

//            PoLineItem item = cell.DataContext as PoLineItem;
//            if (item == null)
//                return;

//            PoLineItems.Remove(item);
//            e.Handled = true;
//        }

//        private void DgPoLineItems_PreviewKeyDown(object sender, KeyEventArgs e)
//        {
//            if (e.Key != Key.Enter)
//                return;

//            if (dgPoLineItems == null)
//                return;

//            DataGridCellInfo currentCell = dgPoLineItems.CurrentCell;
//            if (currentCell.Column == null)
//                return;

//            PoLineItem item = currentCell.Item as PoLineItem;
//            if (item == null)
//                return;

//            Int32 currentDisplayIndex = currentCell.Column.DisplayIndex;
//            Int32 nextEditableDisplayIndex = GetNextEditableDisplayIndex(currentDisplayIndex);

//            dgPoLineItems.CommitEdit(DataGridEditingUnit.Cell, true);
//            dgPoLineItems.CommitEdit(DataGridEditingUnit.Row, true);

//            e.Handled = true;

//            if (nextEditableDisplayIndex < 0)
//                return;

//            Dispatcher.BeginInvoke(new Action(delegate
//            {
//                FocusLineItemEditor(item, nextEditableDisplayIndex);
//            }), DispatcherPriority.Background);
//        }

//        private Int32 GetNextEditableDisplayIndex(Int32 currentDisplayIndex)
//        {
//            // Editable columns:
//            // 0 Item Code
//            // 1 Description
//            // 2 Quantity
//            // 3 Unit
//            // 4 Unit Price
//            switch (currentDisplayIndex)
//            {
//                case 0: return 1;
//                case 1: return 2;
//                case 2: return 3;
//                case 3: return 4;
//                default: return -1;
//            }
//        }

//        private void FocusLineItemEditor(PoLineItem item, Int32 columnDisplayIndex)
//        {
//            if (dgPoLineItems == null || item == null)
//                return;

//            DataGridColumn column = GetColumnByDisplayIndex(dgPoLineItems, columnDisplayIndex);
//            if (column == null)
//                return;

//            dgPoLineItems.SelectedItem = item;
//            dgPoLineItems.CurrentCell = new DataGridCellInfo(item, column);
//            dgPoLineItems.ScrollIntoView(item, column);
//            dgPoLineItems.UpdateLayout();
//            dgPoLineItems.BeginEdit();

//            DataGridRow row = (DataGridRow)dgPoLineItems.ItemContainerGenerator.ContainerFromItem(item);
//            if (row == null)
//                return;

//            DataGridCell cell = GetCell(dgPoLineItems, row, columnDisplayIndex);
//            if (cell == null)
//                return;

//            cell.Focus();

//            TextBox textBox = FindVisualChild<TextBox>(cell);
//            if (textBox != null)
//            {
//                textBox.Focus();
//                textBox.SelectAll();
//                return;
//            }

//            SpinnerTextBox spinner = FindVisualChild<SpinnerTextBox>(cell);
//            if (spinner != null)
//            {
//                spinner.Focus();
//                spinner.SelectAll();
//                return;
//            }

//            ComboBox combo = FindVisualChild<ComboBox>(cell);
//            if (combo != null)
//            {
//                combo.Focus();
//                return;
//            }
//        }

//        private DataGridColumn GetColumnByDisplayIndex(DataGrid dataGrid, Int32 displayIndex)
//        {
//            if (dataGrid == null)
//                return null;

//            foreach (DataGridColumn column in dataGrid.Columns)
//            {
//                if (column.DisplayIndex == displayIndex)
//                    return column;
//            }

//            return null;
//        }

//        private DataGridCell GetCell(DataGrid dataGrid, DataGridRow row, Int32 columnDisplayIndex)
//        {
//            if (dataGrid == null || row == null)
//                return null;

//            DataGridCellsPresenter presenter = FindVisualChild<DataGridCellsPresenter>(row);
//            if (presenter == null)
//            {
//                dataGrid.ScrollIntoView(row, dataGrid.Columns[columnDisplayIndex]);
//                row.UpdateLayout();
//                presenter = FindVisualChild<DataGridCellsPresenter>(row);
//            }

//            if (presenter == null)
//                return null;

//            DataGridCell cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnDisplayIndex) as DataGridCell;
//            if (cell == null)
//            {
//                dataGrid.ScrollIntoView(row, dataGrid.Columns[columnDisplayIndex]);
//                presenter.UpdateLayout();
//                cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnDisplayIndex) as DataGridCell;
//            }

//            return cell;
//        }

//        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
//        {
//            if (parent == null)
//                return null;

//            Int32 count = VisualTreeHelper.GetChildrenCount(parent);

//            for (Int32 i = 0; i < count; i++)
//            {
//                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

//                T typedChild = child as T;
//                if (typedChild != null)
//                    return typedChild;

//                T descendant = FindVisualChild<T>(child);
//                if (descendant != null)
//                    return descendant;
//            }

//            return null;
//        }

//        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
//        {
//            while (child != null)
//            {
//                T typed = child as T;
//                if (typed != null)
//                    return typed;

//                child = VisualTreeHelper.GetParent(child);
//            }

//            return null;
//        }

//        private void DpPoOrderDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
//        {
//            UpdateCalculatedDelivery();
//            ReevaluateDirtyState();
//        }

//        private void DpPoRequiredDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
//        {
//            ReevaluateDirtyState();
//        }

//        private void TxtPoLeadTimeDays_TextChanged(object sender, TextChangedEventArgs e)
//        {
//            UpdateCalculatedDelivery();
//            ReevaluateDirtyState();        
//        }

//        private void TxtPoLeadTimeDays_LostFocus(object sender, RoutedEventArgs e)
//        {
//            UpdateCalculatedDelivery();
//            ReevaluateDirtyState();
//        }

//        private void UpdateCalculatedDelivery()
//        {
//            if (tbPoCalculatedDelivery == null)
//                return;

//            if (dpPoOrderDate == null)
//            {
//                tbPoCalculatedDelivery.Text = string.Empty;
//                return;
//            }

//            DateTime? orderDate = dpPoOrderDate.SelectedDate;

//            if (!orderDate.HasValue)
//            {
//                tbPoCalculatedDelivery.Text = string.Empty;
//                return;
//            }

//            Int32 leadDays = 0;

//            if (txtPoLeadTimeDays != null)
//            {
//                string raw = (txtPoLeadTimeDays.Text ?? string.Empty).Trim();

//                Int32 parsed;
//                if (Int32.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
//                    leadDays = parsed;
//                else
//                    leadDays = 0;
//            }

//            if (leadDays < 0)
//                leadDays = 0;

//            DateTime delivery = orderDate.Value.Date.AddDays(leadDays);

//            // Display rule: yyyy/MM/dd (culture-invariant)
//            tbPoCalculatedDelivery.Text = delivery.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
//        }

//        private void ApplySupplierCardValues()
//        {
//            if (tbPoSupplierDisplay != null)
//            {
//                // Display-only field: "CODE - Supplier Name"
//                string display = string.IsNullOrWhiteSpace(_code)
//                    ? _supplierName
//                    : (string.IsNullOrWhiteSpace(_supplierName) ? _code : (_code + " - " + _supplierName));

//                tbPoSupplierDisplay.Text = display ?? string.Empty;
//            }

//            if (tbPoEmailContact != null)
//                tbPoEmailContact.Text = _emailContact ?? string.Empty;

//            if (tbPoPhoneNumber != null)
//                tbPoPhoneNumber.Text = _phoneNumber ?? string.Empty;

//            if (tbPoPaymentTerms != null)
//                tbPoPaymentTerms.Text = _paymentTerms ?? string.Empty;
//        }

//        private void InitializeDirtyTracking()
//        {
//            if (_dirtyTrackingInitialized)
//            {
//                ReevaluateDirtyState();
//                return;
//            }

//            _cleanSnapshot = CaptureCurrentSnapshot();
//            _dirtyTrackingInitialized = true;

//            ReevaluateDirtyState();
//        }

//        private void PoDirtyField_TextChanged(object sender, TextChangedEventArgs e)
//        {
//            ReevaluateDirtyState();
//        }

//        private void PoLineItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
//        {
//            if (e.OldItems != null)
//            {
//                foreach (object oldItem in e.OldItems)
//                {
//                    PoLineItem poLineItem = oldItem as PoLineItem;
//                    if (poLineItem != null)
//                        UnsubscribeLineItem(poLineItem);
//                }
//            }

//            if (e.NewItems != null)
//            {
//                foreach (object newItem in e.NewItems)
//                {
//                    PoLineItem poLineItem = newItem as PoLineItem;
//                    if (poLineItem != null)
//                        SubscribeLineItem(poLineItem);
//                }
//            }

//            ReevaluateDirtyState();
//        }

//        private void SubscribeLineItem(PoLineItem item)
//        {
//            if (item == null)
//                return;

//            item.PropertyChanged -= PoLineItem_PropertyChanged;
//            item.PropertyChanged += PoLineItem_PropertyChanged;
//        }

//        private void UnsubscribeLineItem(PoLineItem item)
//        {
//            if (item == null)
//                return;

//            item.PropertyChanged -= PoLineItem_PropertyChanged;
//        }

//        private void PoLineItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
//        {
//            if (e == null || string.IsNullOrWhiteSpace(e.PropertyName))
//            {
//                ReevaluateDirtyState();
//                return;
//            }

//            if (e.PropertyName == "ItemCode" ||
//                e.PropertyName == "Description" ||
//                e.PropertyName == "Quantity" ||
//                e.PropertyName == "Uom" ||
//                e.PropertyName == "UnitPrice")
//            {
//                ReevaluateDirtyState();
//            }
//        }

//        private void ReevaluateDirtyState()
//        {
//            if (!_dirtyTrackingInitialized)
//                return;

//            PoDirtySnapshot currentSnapshot = CaptureCurrentSnapshot();
//            bool isDirtyNow = !_cleanSnapshot.IsEquivalentTo(currentSnapshot);

//            _isDirty = isDirtyNow;
//            ApplyDirtyUiState();
//        }

//        private void ApplyDirtyUiState()
//        {
//            if (txtPoFooterUnsaved != null)
//                txtPoFooterUnsaved.Visibility = _isDirty ? Visibility.Visible : Visibility.Collapsed;

//            if (btnGeneratePo != null)
//                btnGeneratePo.IsEnabled = _isDirty;
//        }

//        private PoDirtySnapshot CaptureCurrentSnapshot()
//        {
//            PoDirtySnapshot snapshot = new PoDirtySnapshot();

//            snapshot.OrderDate = NormalizeDate(dpPoOrderDate != null ? dpPoOrderDate.SelectedDate : null);
//            snapshot.RequiredDate = NormalizeDate(dpPoRequiredDate != null ? dpPoRequiredDate.SelectedDate : null);
//            snapshot.LeadTimeDays = NormalizeText(txtPoLeadTimeDays != null ? txtPoLeadTimeDays.Text : string.Empty);

//            snapshot.DeliveryStreetAddress = NormalizeText(txtPoDeliveryStreetAddress != null ? txtPoDeliveryStreetAddress.Text : string.Empty);
//            snapshot.DeliveryCity = NormalizeText(txtPoDeliveryCity != null ? txtPoDeliveryCity.Text : string.Empty);
//            snapshot.DeliveryPostalCode = NormalizeText(txtPoDeliveryPostalCode != null ? txtPoDeliveryPostalCode.Text : string.Empty);
//            snapshot.DeliveryCountryRegion = NormalizeText(txtPoDeliveryCountryRegion != null ? txtPoDeliveryCountryRegion.Text : string.Empty);

//            foreach (PoLineItem item in PoLineItems)
//            {
//                if (!IsMeaningfulLineItem(item))
//                    continue;

//                snapshot.LineItemSignatures.Add(BuildLineItemSignature(item));
//            }

//            return snapshot;
//        }

//        private bool IsMeaningfulLineItem(PoLineItem item)
//        {
//            if (item == null)
//                return false;

//            return !IsDefaultLineItem(item);
//        }

//        private bool IsDefaultLineItem(PoLineItem item)
//        {
//            if (item == null)
//                return true;

//            string itemCode = NormalizeText(item.ItemCode);
//            string description = NormalizeText(item.Description);
//            string uom = NormalizeText(item.Uom);
//            string unitPrice = NormalizeUnitPriceForDirtyCompare(item.UnitPrice);

//            return itemCode.Length == 0 &&
//                   description.Length == 0 &&
//                   item.Quantity == 1 &&
//                   string.Equals(uom, "Each", StringComparison.OrdinalIgnoreCase) &&
//                   string.Equals(unitPrice, "0.00", StringComparison.Ordinal);
//        }

//        private string BuildLineItemSignature(PoLineItem item)
//        {
//            string unitPrice = NormalizeUnitPriceForDirtyCompare(item.UnitPrice);

//            return NormalizeText(item.ItemCode) + "¦" +
//                   NormalizeText(item.Description) + "¦" +
//                   item.Quantity.ToString(CultureInfo.InvariantCulture) + "¦" +
//                   NormalizeText(item.Uom) + "¦" +
//                   unitPrice;
//        }

//        private string NormalizeText(string value)
//        {
//            return (value ?? string.Empty).Trim();
//        }

//        private string NormalizeDate(DateTime? value)
//        {
//            return value.HasValue
//                ? value.Value.Date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
//                : string.Empty;
//        }

//        private string NormalizeUnitPriceForDirtyCompare(Decimal? value)
//        {
//            if (!value.HasValue)
//                return "0.00";

//            if (value.Value == 0m)
//                return "0.00";

//            return value.Value.ToString("0.00", CultureInfo.InvariantCulture);
//        }

//        private sealed class PoDirtySnapshot
//        {
//            public string OrderDate { get; set; }
//            public string RequiredDate { get; set; }
//            public string LeadTimeDays { get; set; }
//            public string DeliveryStreetAddress { get; set; }
//            public string DeliveryCity { get; set; }
//            public string DeliveryPostalCode { get; set; }
//            public string DeliveryCountryRegion { get; set; }

//            public Collection<string> LineItemSignatures { get; private set; }

//            public PoDirtySnapshot()
//            {
//                OrderDate = string.Empty;
//                RequiredDate = string.Empty;
//                LeadTimeDays = string.Empty;
//                DeliveryStreetAddress = string.Empty;
//                DeliveryCity = string.Empty;
//                DeliveryPostalCode = string.Empty;
//                DeliveryCountryRegion = string.Empty;
//                LineItemSignatures = new Collection<string>();
//            }

//            public bool IsEquivalentTo(PoDirtySnapshot other)
//            {
//                if (other == null)
//                    return false;

//                if (!string.Equals(OrderDate, other.OrderDate, StringComparison.Ordinal))
//                    return false;

//                if (!string.Equals(RequiredDate, other.RequiredDate, StringComparison.Ordinal))
//                    return false;

//                if (!string.Equals(LeadTimeDays, other.LeadTimeDays, StringComparison.Ordinal))
//                    return false;

//                if (!string.Equals(DeliveryStreetAddress, other.DeliveryStreetAddress, StringComparison.Ordinal))
//                    return false;

//                if (!string.Equals(DeliveryCity, other.DeliveryCity, StringComparison.Ordinal))
//                    return false;

//                if (!string.Equals(DeliveryPostalCode, other.DeliveryPostalCode, StringComparison.Ordinal))
//                    return false;

//                if (!string.Equals(DeliveryCountryRegion, other.DeliveryCountryRegion, StringComparison.Ordinal))
//                    return false;

//                if (LineItemSignatures.Count != other.LineItemSignatures.Count)
//                    return false;

//                for (Int32 i = 0; i < LineItemSignatures.Count; i++)
//                {
//                    if (!string.Equals(LineItemSignatures[i], other.LineItemSignatures[i], StringComparison.Ordinal))
//                        return false;
//                }

//                return true;
//            }
//        }

//        private bool IsPoUnsavedConfirmOverlayVisible()
//        {
//            return pnlPoUnsavedConfirmOverlay != null &&
//                   pnlPoUnsavedConfirmOverlay.Visibility == Visibility.Visible;
//        }

//        private void ShowPoUnsavedConfirmOverlay()
//        {
//            if (pnlPoUnsavedConfirmOverlay == null)
//                return;

//            pnlPoUnsavedConfirmOverlay.Visibility = Visibility.Visible;

//            if (btnPoUnsavedNo != null)
//                btnPoUnsavedNo.Focus();
//        }

//        private void HidePoUnsavedConfirmOverlay()
//        {
//            if (pnlPoUnsavedConfirmOverlay == null)
//                return;

//            pnlPoUnsavedConfirmOverlay.Visibility = Visibility.Collapsed;
//        }

//        private void AttemptCloseSupplierNewPo()
//        {
//            if (IsPoUnsavedConfirmOverlayVisible())
//                return;

//            if (_isDirty)
//            {
//                ShowPoUnsavedConfirmOverlay();
//                return;
//            }

//            RaiseRequestClose();
//        }

//        private void PoUnsavedOverlayScrim_MouseDown(object sender, MouseButtonEventArgs e)
//        {
//            e.Handled = true;
//            HidePoUnsavedConfirmOverlay();
//        }

//        private void PoUnsavedOverlayCard_MouseDown(object sender, MouseButtonEventArgs e)
//        {
//            e.Handled = true;
//        }

//        private void BtnPoUnsavedNo_Click(object sender, RoutedEventArgs e)
//        {
//            HidePoUnsavedConfirmOverlay();
//        }

//        private void BtnPoUnsavedYes_Click(object sender, RoutedEventArgs e)
//        {
//            HidePoUnsavedConfirmOverlay();
//            RaiseRequestClose();
//        }

//        private void RaiseRequestClose()
//        {
//            RequestClose?.Invoke(this, EventArgs.Empty);
//        }

//        private void BtnClose_Click(object sender, RoutedEventArgs e)
//        {
//            AttemptCloseSupplierNewPo();
//        }

//        private void BtnDiscardDraft_Click(object sender, RoutedEventArgs e)
//        {
//            AttemptCloseSupplierNewPo();
//        }

//        private void SupplierNewPO_PreviewKeyDown(object sender, KeyEventArgs e)
//        {
//            if (e.Key != Key.Escape)
//                return;

//            e.Handled = true;

//            if (IsPoUnsavedConfirmOverlayVisible())
//            {
//                HidePoUnsavedConfirmOverlay();
//                return;
//            }

//            AttemptCloseSupplierNewPo();
//        }

//        private void BtnGeneratePo_Click(object sender, RoutedEventArgs e)
//        {
//            // Placeholder: later validation + create workflow
//            // Intentionally no host manipulation here.
//        }
//    }

//    public class PoLineItem : INotifyPropertyChanged
//    {
//        private string _itemCode;
//        private string _description;
//        private Int32 _quantity;
//        private string _uom;
//        private Decimal? _unitPrice;
//        private Decimal _rowTotal;

//        public string ItemCode
//        {
//            get { return _itemCode; }
//            set { SetField(ref _itemCode, value); }
//        }

//        public string Description
//        {
//            get { return _description; }
//            set { SetField(ref _description, value); }
//        }

//        public Int32 Quantity
//        {
//            get { return _quantity; }
//            set
//            {
//                if (SetField(ref _quantity, value))
//                    RecalculateRowTotal();
//            }
//        }

//        public string Uom
//        {
//            get { return _uom; }
//            set { SetField(ref _uom, value); }
//        }

//        public Decimal? UnitPrice
//        {
//            get { return _unitPrice; }
//            set
//            {
//                if (SetField(ref _unitPrice, value))
//                    RecalculateRowTotal();
//            }
//        }

//        public Decimal RowTotal
//        {
//            get { return _rowTotal; }
//            private set
//            {
//                if (SetField(ref _rowTotal, value))
//                    OnPropertyChanged("RowTotalDisplay");
//            }
//        }

//        public string RowTotalDisplay
//        {
//            get { return string.Format(CultureInfo.InvariantCulture, "R {0:0.00}", RowTotal); }
//        }

//        public PoLineItem()
//        {
//            _itemCode = string.Empty;
//            _description = string.Empty;
//            _quantity = 1;
//            _uom = "Each";
//            _unitPrice = null;

//            RecalculateRowTotal();
//        }

//        private void RecalculateRowTotal()
//        {
//            Decimal price = UnitPrice.HasValue ? UnitPrice.Value : 0m;
//            Decimal total = Convert.ToDecimal(Quantity, CultureInfo.InvariantCulture) * price;
//            RowTotal = total;
//        }

//        public event PropertyChangedEventHandler PropertyChanged;

//        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
//        {
//            if (object.Equals(field, value))
//                return false;

//            field = value;
//            OnPropertyChanged(propertyName);
//            return true;
//        }

//        protected void OnPropertyChanged(string propertyName)
//        {
//            PropertyChangedEventHandler handler = PropertyChanged;
//            if (handler != null)
//                handler(this, new PropertyChangedEventArgs(propertyName));
//        }
//    }
//}