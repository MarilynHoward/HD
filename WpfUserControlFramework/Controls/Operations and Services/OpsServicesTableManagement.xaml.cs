using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public partial class OpsServicesTableManagement : UserControl
{
    private sealed class WaiterOption
    {
        public Guid? Id { get; init; }
        public string Label { get; init; } = "";
    }

    private readonly Action _navigateToShiftScheduling;
    private readonly Action _navigateToFloorPlan;
    private readonly Action _openAddTableDialog;
    private readonly Action _openManageFloorsDialog;
    private OpsFloorTable? _selected;
    private OpsFloorTable? _editSnapshot;
    private bool _editing;
    private Guid? _pendingDeleteTableId;
    private bool _storeRefreshPosted;
    private bool _tableMgmtUnloaded;

    public OpsServicesTableManagement(
        Action navigateToShiftScheduling,
        Action navigateToFloorPlan,
        Action openAddTableDialog,
        Action openManageFloorsDialog)
    {
        _navigateToShiftScheduling = navigateToShiftScheduling ?? throw new ArgumentNullException(nameof(navigateToShiftScheduling));
        _navigateToFloorPlan = navigateToFloorPlan ?? throw new ArgumentNullException(nameof(navigateToFloorPlan));
        _openAddTableDialog = openAddTableDialog ?? throw new ArgumentNullException(nameof(openAddTableDialog));
        _openManageFloorsDialog = openManageFloorsDialog ?? throw new ArgumentNullException(nameof(openManageFloorsDialog));
        InitializeComponent();
        TableMgmtBodyScroll.SizeChanged += TableMgmtBodyScroll_SizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void TableMgmtBodyScroll_SizeChanged(object sender, SizeChangedEventArgs e) =>
        SyncTopWorkspaceMinHeight();

    /// <summary>
    /// Outer page scroll only works when content is taller than the viewport. Pin the top workspace
    /// to the scroll viewer's viewport height so the user can scroll the full table management layout.
    /// </summary>
    private void SyncTopWorkspaceMinHeight()
    {
        var vh = TableMgmtBodyScroll.ViewportHeight;
        if (vh <= 0 && TableMgmtBodyScroll.ActualHeight > 0)
            vh = TableMgmtBodyScroll.ActualHeight;
        if (vh > 0)
            TopWorkspaceGrid.MinHeight = vh;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null)
            return null;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            var nested = FindVisualChild<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void RouteWheelToPageScroll(ScrollViewer? inner, ScrollViewer page, MouseWheelEventArgs e)
    {
        var innerHas = inner is { ScrollableHeight: > 0 };
        var up = e.Delta > 0;
        var down = e.Delta < 0;

        if (!innerHas)
        {
            if (up)
                page.LineUp();
            else if (down)
                page.LineDown();
            e.Handled = true;
            return;
        }

        var atTop = inner!.VerticalOffset <= 0;
        var atBottom = inner.VerticalOffset >= inner.ScrollableHeight;

        if (up && atTop)
        {
            page.LineUp();
            e.Handled = true;
            return;
        }

        if (down && atBottom)
        {
            page.LineDown();
            e.Handled = true;
        }
    }

    private void DetailsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        RouteWheelToPageScroll(sender as ScrollViewer, TableMgmtBodyScroll, e);

    private void TablesListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var inner = FindVisualChild<ScrollViewer>(TablesListBox);
        RouteWheelToPageScroll(inner, TableMgmtBodyScroll, e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _tableMgmtUnloaded = false;
        OpsServicesStore.EnsureSeeded();
        OpsServicesStore.DataChanged += OnDataChanged;
        HighlightTablePill(true);
        RefreshFloorFilter(resetSelection: true);
        DetShape.ItemsSource = new[] { "Square", "Round" };
        RefreshAll();
        SelectFirstTableIfNone();
        SyncTopWorkspaceMinHeight();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _tableMgmtUnloaded = true;
        TableMgmtBodyScroll.SizeChanged -= TableMgmtBodyScroll_SizeChanged;
        OpsServicesStore.DataChanged -= OnDataChanged;
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        if (_tableMgmtUnloaded || _storeRefreshPosted)
            return;
        _storeRefreshPosted = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _storeRefreshPosted = false;
            if (_tableMgmtUnloaded)
                return;
            RefreshAll();
        }), DispatcherPriority.DataBind);
    }

    private static List<string> BuildDistinctSortedFloorsFromTables() =>
        OpsServicesStore.GetDistinctFloorNamesForFilter().ToList();

    /// <summary>Floor picker items: distinct floors from tables plus the current value (editable combo / save).</summary>
    private static List<string> BuildFloorItemsForCombo(string? currentLocation)
    {
        var floors = BuildDistinctSortedFloorsFromTables();
        var loc = string.IsNullOrWhiteSpace(currentLocation) ? "Main Floor" : currentLocation.Trim();
        if (!floors.Exists(s => string.Equals(s, loc, StringComparison.OrdinalIgnoreCase)))
            floors.Add(loc);
        floors.Sort(StringComparer.OrdinalIgnoreCase);
        return floors;
    }

    private void ScrollSelectedTableIntoView()
    {
        if (TablesListBox.SelectedItem == null)
            return;
        var item = TablesListBox.SelectedItem;
        Dispatcher.BeginInvoke(new Action(() => TablesListBox.ScrollIntoView(item)),
            DispatcherPriority.Loaded);
    }

    /// <summary>Rebuilds floor filter from current tables. Call after <see cref="RefreshAll"/> data changes so new floors appear.</summary>
    private void RefreshFloorFilter(bool resetSelection)
    {
        var floorNames = BuildDistinctSortedFloorsFromTables();
        var floors = new List<string> { "All Floors" };
        floors.AddRange(floorNames);

        var previous = resetSelection ? null : CmbFloorFilter.SelectedItem as string;
        CmbFloorFilter.ItemsSource = floors;

        if (resetSelection || string.IsNullOrEmpty(previous))
        {
            CmbFloorFilter.SelectedItem = floors[0];
            return;
        }

        var match = floors.FirstOrDefault(f => string.Equals(f, previous, StringComparison.OrdinalIgnoreCase));
        CmbFloorFilter.SelectedItem = match ?? floors[0];
    }

    private void RefreshAll()
    {
        UpdateStats();
        RefreshFloorFilter(resetSelection: false);
        RefreshTableList();
        if (_selected != null)
        {
            var live = OpsServicesStore.GetTables().FirstOrDefault(t => t.Id == _selected.Id);
            if (live == null)
            {
                _selected = null;
                ClearDetail();
            }
            else
            {
                _selected = live;
                if (!_editing)
                    PushDetailFromModel();
            }
        }
        else
        {
            ClearDetail();
        }
    }

    private void UpdateStats()
    {
        var tables = OpsServicesStore.GetTables().ToList();
        StatTotal.Text = tables.Count.ToString(CultureInfo.CurrentCulture);
        StatActive.Text = tables.Count(t => t.IsActive).ToString(CultureInfo.CurrentCulture);
        StatSeats.Text = tables.Sum(t => t.SeatCount).ToString(CultureInfo.CurrentCulture);
        StatAssigned.Text = tables.Count(t => t.AssignedWaiterId.HasValue).ToString(CultureInfo.CurrentCulture);
    }

    private void RefreshTableList()
    {
        var q = OpsServicesStore.GetTables().AsEnumerable();
        var term = (TxtSearch.Text ?? "").Trim();
        if (!string.IsNullOrEmpty(term))
            q = q.Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                             (t.LocationName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));

        if (CmbFloorFilter.SelectedItem is string f && f != "All Floors")
            q = q.Where(t => string.Equals(t.LocationName?.Trim(), f, StringComparison.OrdinalIgnoreCase));

        var list = q.OrderBy(t => t.LocationName).ThenBy(t => t.Name).ToList();
        TablesListBox.ItemsSource = list;
        if (_selected != null)
        {
            var match = list.FirstOrDefault(t => t.Id == _selected.Id);
            TablesListBox.SelectedItem = match;
            if (match != null)
                ScrollSelectedTableIntoView();
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshTableList();

    private void TableSearchHostBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Let clicks on the TextBox reach it (caret placement, selection). Only absorb chrome hits.
        if (e.OriginalSource is DependencyObject src && TxtSearch.IsAncestorOf(src))
            return;

        e.Handled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (TxtSearch.IsKeyboardFocusWithin)
                return;
            TxtSearch.Focus();
            Keyboard.Focus(TxtSearch);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void FloorFilterHostBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Do not mark handled when the click targets the ComboBox — otherwise the toggle never opens and the list looks "empty".
        if (e.OriginalSource is DependencyObject src && CmbFloorFilter.IsAncestorOf(src))
            return;

        e.Handled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (CmbFloorFilter.IsKeyboardFocusWithin)
                return;
            CmbFloorFilter.Focus();
            Keyboard.Focus(CmbFloorFilter);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>When opening Table Management, show the first table in the list in the detail panel.</summary>
    private void SelectFirstTableIfNone()
    {
        if (_editing || TablesListBox.Items.Count == 0)
            return;
        if (TablesListBox.SelectedItem != null)
        {
            ScrollSelectedTableIntoView();
            return;
        }

        TablesListBox.SelectedItem = TablesListBox.Items[0];
        ScrollSelectedTableIntoView();
    }

    private void CmbFloorFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        RefreshTableList();
    }

    private void TablesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        HideTableDeleteInlinePanels();
        if (_editing)
            return;
        _selected = TablesListBox.SelectedItem as OpsFloorTable;
        if (_selected == null)
        {
            ClearDetail();
            return;
        }

        DetailPanel.Visibility = Visibility.Visible;
        TxtNoSelection.Visibility = Visibility.Collapsed;
        PushDetailFromModel();
        BtnDelete.IsEnabled = true;
    }

    private void ClearDetail()
    {
        _editSnapshot = null;
        _editing = false;
        _selected = null;
        ExitEditUi();
        TxtNoSelection.Visibility = Visibility.Visible;
        DetailPanel.Visibility = Visibility.Collapsed;
    }

    private void PushDetailFromModel()
    {
        if (_selected == null)
            return;

        DetName.Text = _selected.Name;

        var loc = string.IsNullOrWhiteSpace(_selected.LocationName) ? "Main Floor" : _selected.LocationName.Trim();
        DetFloorPillText.Text = loc;
        var floorItems = BuildFloorItemsForCombo(_selected.LocationName);
        DetFloor.ItemsSource = floorItems;
        var floorMatch = floorItems.FirstOrDefault(f => string.Equals(f, loc, StringComparison.OrdinalIgnoreCase));
        DetFloor.SelectedItem = floorMatch ?? floorItems.FirstOrDefault();

        var seatN = _selected.SeatCount;
        DetSeatsReadOnly.Text = $"{seatN} seats";
        DetSeats.Text = seatN.ToString(CultureInfo.CurrentCulture);
        DetShapePillText.Text = _selected.Shape ?? "Square";
        DetShape.SelectedItem = _selected.Shape;
        var ordered = OpsServicesStore.GetTables().OrderBy(t => t.CreatedUtc).ThenBy(t => t.Name).ToList();
        var idx = ordered.FindIndex(t => t.Id == _selected.Id);
        DetId.Text = idx >= 0 ? $"#{idx + 1}" : _selected.Id.ToString("N", CultureInfo.InvariantCulture)[..8];
        DetCreated.Text = _selected.CreatedUtc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
        DetModified.Text = _selected.ModifiedUtc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

        var waiters = new List<WaiterOption> { new() { Id = null, Label = "Unassigned" } };
        waiters.AddRange(OpsServicesStore.GetEmployees().Select(e => new WaiterOption { Id = e.Id, Label = e.Name }));
        DetWaiter.ItemsSource = waiters;
        DetWaiter.DisplayMemberPath = nameof(WaiterOption.Label);
        var cur = waiters.FirstOrDefault(w => w.Id == _selected.AssignedWaiterId);
        DetWaiter.SelectedItem = cur ?? waiters[0];
        DetWaiterReadOnly.Text = cur?.Label ?? "Unassigned";

        DetActive.IsChecked = _selected.IsActive;
        ApplyActiveStatusPresentation(_selected.IsActive);
    }

    private void ApplyActiveStatusPresentation(bool isActive)
    {
        if (isActive)
        {
            TxtDetActiveCaption.Text = "Table is active";
            DetActivePillText.Text = "Active";
            var greenFill = new SolidColorBrush(Color.FromRgb(220, 252, 231));
            greenFill.Freeze();
            DetActivePillBorder.Background = greenFill;
            DetActivePillBorder.BorderBrush = (Brush)FindResource("Brush.SuccessGreen")!;
            DetActivePillText.Foreground = (Brush)FindResource("Brush.SuccessGreen")!;
        }
        else
        {
            TxtDetActiveCaption.Text = "Table is inactive";
            DetActivePillText.Text = "Inactive";
            DetActivePillBorder.Background = (Brush)FindResource("Brush.SurfaceGreySoft")!;
            DetActivePillBorder.BorderBrush = (Brush)FindResource("Brush.BorderSoft")!;
            DetActivePillText.Foreground = (Brush)FindResource("DimmedForeground")!;
        }
    }

    private void PullDetailToModel()
    {
        if (_selected == null)
            return;
        _selected.Name = DetName.Text.Trim();
        var floorTxt = (DetFloor.SelectedItem as string ?? "").Trim();
        _selected.LocationName = string.IsNullOrWhiteSpace(floorTxt) ? "Main Floor" : floorTxt;
        var seatRaw = DetSeats.Text.Trim();
        var seatToken = seatRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? seatRaw;
        if (int.TryParse(seatToken, NumberStyles.Integer, CultureInfo.CurrentCulture, out var seats))
            _selected.SeatCount = Math.Max(1, seats);
        _selected.Shape = DetShape.SelectedItem as string ?? _selected.Shape;
        if (DetWaiter.SelectedItem is WaiterOption wo)
            _selected.AssignedWaiterId = wo.Id;
        _selected.IsActive = DetActive.IsChecked == true;
        _selected.ModifiedUtc = DateTime.UtcNow;
        _selected.OpsServerId = _selected.AssignedWaiterId;
        if (!_selected.IsActive)
            _selected.OpsStatus = "Inactive";
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e) => BeginTableDetailsEdit();

    private void BeginTableDetailsEdit()
    {
        if (_selected == null)
            return;
        HideTableDeleteInlinePanels();
        _editSnapshot = OpsServicesStore.CloneTable(_selected);
        _editing = true;
        DetFloorPillBorder.Visibility = Visibility.Collapsed;
        DetFloor.Visibility = Visibility.Visible;
        DetShapePillBorder.Visibility = Visibility.Collapsed;
        DetShape.Visibility = Visibility.Visible;
        DetSeatsReadOnly.Visibility = Visibility.Collapsed;
        DetSeats.Visibility = Visibility.Visible;
        DetWaiterReadOnly.Visibility = Visibility.Collapsed;
        DetWaiter.Visibility = Visibility.Visible;
        DetActiveReadOnlyRow.Visibility = Visibility.Collapsed;
        DetActiveEditPanel.Visibility = Visibility.Visible;
        TxtActiveStatusSectionLabel.Visibility = Visibility.Collapsed;
        DetName.IsReadOnly = false;
        DetFloor.IsEnabled = true;
        DetSeats.IsReadOnly = false;
        DetShape.IsEnabled = true;
        DetWaiter.IsEnabled = true;
        DetActive.IsEnabled = true;
        BtnEdit.Visibility = Visibility.Collapsed;
        BtnSave.Visibility = Visibility.Visible;
        BtnCancelEdit.Visibility = Visibility.Visible;
        BtnDelete.IsEnabled = false;
        PushDetailFromModel();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
            return;
        PullDetailToModel();
        OpsServicesStore.RegisterFloorName(_selected.LocationName);
        _editSnapshot = null;
        ExitEditUi();
        OpsServicesStore.NotifyDataChanged();
        if (_selected != null)
        {
            TablesListBox.SelectedItem = _selected;
            ScrollSelectedTableIntoView();
        }
    }

    private void BtnCancelEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected != null && _editSnapshot != null)
            OpsServicesStore.CopyTable(_editSnapshot, _selected);
        _editSnapshot = null;
        ExitEditUi();
    }

    private void ExitEditUi()
    {
        HideTableDeleteInlinePanels();
        _editing = false;
        DetName.IsReadOnly = true;
        DetFloor.IsEnabled = false;
        DetSeats.IsReadOnly = true;
        DetShape.IsEnabled = false;
        DetWaiter.IsEnabled = false;
        DetActive.IsEnabled = false;
        DetFloor.Visibility = Visibility.Collapsed;
        DetFloorPillBorder.Visibility = Visibility.Visible;
        DetShape.Visibility = Visibility.Collapsed;
        DetShapePillBorder.Visibility = Visibility.Visible;
        DetSeats.Visibility = Visibility.Collapsed;
        DetSeatsReadOnly.Visibility = Visibility.Visible;
        DetWaiter.Visibility = Visibility.Collapsed;
        DetWaiterReadOnly.Visibility = Visibility.Visible;
        DetActiveEditPanel.Visibility = Visibility.Collapsed;
        DetActiveReadOnlyRow.Visibility = Visibility.Visible;
        TxtActiveStatusSectionLabel.Visibility = Visibility.Visible;
        BtnEdit.Visibility = Visibility.Visible;
        BtnSave.Visibility = Visibility.Collapsed;
        BtnCancelEdit.Visibility = Visibility.Collapsed;
        BtnDelete.IsEnabled = _selected != null && !_editing;
        if (_selected != null)
            PushDetailFromModel();
    }

    private void HideTableDeleteInlinePanels()
    {
        DeleteTableBlockedByShiftsPanel.Visibility = Visibility.Collapsed;
        DeleteTableConfirmPanel.Visibility = Visibility.Collapsed;
        _pendingDeleteTableId = null;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null || _editing)
            return;
        HideTableDeleteInlinePanels();
        var tableName = _selected.Name;
        var id = _selected.Id;
        var shiftCount = OpsServicesStore.GetBookedShiftCountForTable(id);
        if (shiftCount > 0)
        {
            TxtDeleteTableBlockedTitle.Text = shiftCount == 1
                ? $"\"{tableName}\" has a shift booked against it. This table cannot be deleted."
                : $"\"{tableName}\" has {shiftCount} shifts booked against it. This table cannot be deleted.";
            DeleteTableBlockedByShiftsPanel.Visibility = Visibility.Visible;
            return;
        }

        _pendingDeleteTableId = id;
        TxtDeleteTableConfirmMessage.Text = $"Delete \"{tableName}\"?";
        DeleteTableConfirmPanel.Visibility = Visibility.Visible;
    }

    private void BtnCancelDeleteTableConfirm_Click(object sender, RoutedEventArgs e) =>
        HideTableDeleteInlinePanels();

    private void BtnConfirmDeleteTable_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingDeleteTableId is not { } id)
        {
            HideTableDeleteInlinePanels();
            return;
        }

        if (_selected == null || _selected.Id != id)
        {
            HideTableDeleteInlinePanels();
            return;
        }

        HideTableDeleteInlinePanels();
        OpsServicesStore.RemoveTable(id);
        _selected = null;
        TablesListBox.SelectedItem = null;
    }

    private void BtnDismissDeleteBlockedByShifts_Click(object sender, RoutedEventArgs e) =>
        HideTableDeleteInlinePanels();

    private void BtnEditTableFromDeleteBlocked_Click(object sender, RoutedEventArgs e)
    {
        HideTableDeleteInlinePanels();
        BeginTableDetailsEdit();
    }

    private void BtnAddFloor_Click(object sender, RoutedEventArgs e) => _openManageFloorsDialog();

    private void BtnAddTable_Click(object sender, RoutedEventArgs e) => _openAddTableDialog();

    private void PillShift_Click(object sender, RoutedEventArgs e)
    {
        HighlightTablePill(false);
        _navigateToShiftScheduling();
    }

    private void PillFloor_Click(object sender, RoutedEventArgs e)
    {
        HighlightTablePill(false);
        _navigateToFloorPlan();
    }

    private void HighlightTablePill(bool tableSelected)
    {
        var blue = TryFindResource("Brush.PrimaryBlue") as Brush
                   ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")!);

        void SetActive(Button b)
        {
            b.Background = blue;
            b.BorderBrush = blue;
            b.Foreground = Brushes.White;
            b.BorderThickness = new Thickness(1);
            b.FontWeight = FontWeights.SemiBold;
        }

        void SetInactive(Button b)
        {
            b.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            b.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
            b.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            b.BorderThickness = new Thickness(1);
            b.FontWeight = FontWeights.Normal;
        }

        if (tableSelected)
        {
            SetInactive(PillShift);
            SetInactive(PillFloor);
            SetActive(PillTable);
        }
        else
        {
            SetActive(PillShift);
            SetInactive(PillFloor);
            SetInactive(PillTable);
        }
    }

    private void BtnHistory_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(Window.GetWindow(this), "History is not wired in this demo.", "History", MessageBoxButton.OK,
            MessageBoxImage.Information);

}
