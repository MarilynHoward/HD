using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace RestaurantPosWpf;

public partial class OpsServicesTableManagement : UserControl
{
    private sealed class WaiterOption
    {
        public Guid? Id { get; init; }
        public string Label { get; init; } = "";
    }

    private readonly Action _navigateToShiftScheduling;
    private readonly Action _openAddTableDialog;
    private OpsFloorTable? _selected;
    private OpsFloorTable? _editSnapshot;
    private bool _editing;

    public OpsServicesTableManagement(
        Action navigateToShiftScheduling,
        Action openAddTableDialog)
    {
        _navigateToShiftScheduling = navigateToShiftScheduling ?? throw new ArgumentNullException(nameof(navigateToShiftScheduling));
        _openAddTableDialog = openAddTableDialog ?? throw new ArgumentNullException(nameof(openAddTableDialog));
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        OpsServicesStore.EnsureSeeded();
        OpsServicesStore.DataChanged += OnDataChanged;
        HighlightTablePill(true);
        InitFloorFilter();
        DetShape.ItemsSource = new[] { "Square", "Round" };
        RefreshAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        OpsServicesStore.DataChanged -= OnDataChanged;

    private void OnDataChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(RefreshAll);

    private void InitFloorFilter()
    {
        var floors = new List<string> { "All Floors" };
        floors.AddRange(OpsServicesStore.GetTables().Select(t => t.LocationName).Distinct().OrderBy(x => x));
        CmbFloorFilter.ItemsSource = floors;
        CmbFloorFilter.SelectedIndex = 0;
    }

    private void RefreshAll()
    {
        UpdateStats();
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
                             t.LocationName.Contains(term, StringComparison.OrdinalIgnoreCase));

        if (CmbFloorFilter.SelectedItem is string f && f != "All Floors")
            q = q.Where(t => t.LocationName == f);

        var list = q.OrderBy(t => t.LocationName).ThenBy(t => t.Name).ToList();
        TablesListBox.ItemsSource = list;
        if (_selected != null)
        {
            var match = list.FirstOrDefault(t => t.Id == _selected.Id);
            TablesListBox.SelectedItem = match;
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshTableList();

    private void CmbFloorFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        RefreshTableList();
    }

    private void TablesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
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
        var floors = OpsServicesStore.GetTables().Select(t => t.LocationName).Distinct().OrderBy(x => x).ToList();
        DetFloor.ItemsSource = floors;
        DetFloor.Text = _selected.LocationName;
        DetSeats.Text = _selected.SeatCount.ToString(CultureInfo.CurrentCulture);
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

        DetActive.IsChecked = _selected.IsActive;
        DetAccessible.IsChecked = _selected.Accessible;
        DetVip.IsChecked = _selected.VipPriority;

        DetOpsStatus.Text = _selected.OpsStatus;
        var server = _selected.OpsServerId is { } sid
            ? OpsServicesStore.GetEmployee(sid)?.Name ?? "—"
            : "—";
        TxtServerName.Text = $"Server: {server}";

        OverdueBanner.Visibility = Visibility.Collapsed;
        if (_selected.OpsStatus.Equals("Occupied", StringComparison.OrdinalIgnoreCase) &&
            _selected.SeatedAtUtc is { } seated &&
            _selected.TurnTimeMinutes > 0)
        {
            var mins = (DateTime.UtcNow - seated).TotalMinutes;
            if (mins > _selected.TurnTimeMinutes)
            {
                OverdueBanner.Visibility = Visibility.Visible;
                TxtOverdue.Text =
                    $"Table overdue — seated {Math.Round(mins)} minutes ago · Target: {_selected.TurnTimeMinutes} min · Party of {_selected.PartySize ?? 0}.";
            }
        }
    }

    private void PullDetailToModel()
    {
        if (_selected == null)
            return;
        _selected.Name = DetName.Text.Trim();
        _selected.LocationName = string.IsNullOrWhiteSpace(DetFloor.Text) ? "Main Floor" : DetFloor.Text.Trim();
        if (int.TryParse(DetSeats.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var seats))
            _selected.SeatCount = Math.Max(1, seats);
        _selected.Shape = DetShape.SelectedItem as string ?? _selected.Shape;
        if (DetWaiter.SelectedItem is WaiterOption wo)
            _selected.AssignedWaiterId = wo.Id;
        _selected.IsActive = DetActive.IsChecked == true;
        _selected.Accessible = DetAccessible.IsChecked == true;
        _selected.VipPriority = DetVip.IsChecked == true;
        _selected.ModifiedUtc = DateTime.UtcNow;
        _selected.OpsServerId = _selected.AssignedWaiterId;
        if (!_selected.IsActive)
            _selected.OpsStatus = "Inactive";
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
            return;
        _editSnapshot = OpsServicesStore.CloneTable(_selected);
        _editing = true;
        DetName.IsReadOnly = false;
        DetFloor.IsEnabled = true;
        DetSeats.IsReadOnly = false;
        DetShape.IsEnabled = true;
        DetWaiter.IsEnabled = true;
        DetActive.IsEnabled = true;
        DetAccessible.IsEnabled = true;
        DetVip.IsEnabled = true;
        BtnEdit.Visibility = Visibility.Collapsed;
        BtnSave.Visibility = Visibility.Visible;
        BtnCancelEdit.Visibility = Visibility.Visible;
        BtnDelete.IsEnabled = false;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
            return;
        PullDetailToModel();
        _editSnapshot = null;
        ExitEditUi();
        OpsServicesStore.NotifyDataChanged();
    }

    private void BtnCancelEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected != null && _editSnapshot != null)
            OpsServicesStore.CopyTable(_editSnapshot, _selected);
        _editSnapshot = null;
        ExitEditUi();
        PushDetailFromModel();
    }

    private void ExitEditUi()
    {
        _editing = false;
        DetName.IsReadOnly = true;
        DetFloor.IsEnabled = false;
        DetSeats.IsReadOnly = true;
        DetShape.IsEnabled = false;
        DetWaiter.IsEnabled = false;
        DetActive.IsEnabled = false;
        DetAccessible.IsEnabled = false;
        DetVip.IsEnabled = false;
        BtnEdit.Visibility = Visibility.Visible;
        BtnSave.Visibility = Visibility.Collapsed;
        BtnCancelEdit.Visibility = Visibility.Collapsed;
        BtnDelete.IsEnabled = _selected != null && !_editing;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
            return;
        if (MessageBox.Show(Window.GetWindow(this),
                $"Delete {_selected.Name}?",
                "Delete Table",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        var id = _selected.Id;
        OpsServicesStore.RemoveTable(id);
        _selected = null;
        TablesListBox.SelectedItem = null;
    }

    private void BtnAddTable_Click(object sender, RoutedEventArgs e) => _openAddTableDialog();

    private void PillShift_Click(object sender, RoutedEventArgs e)
    {
        HighlightTablePill(false);
        _navigateToShiftScheduling();
    }

    private void HighlightTablePill(bool tableSelected)
    {
        void Hi(Button b, bool on)
        {
            if (on)
            {
                b.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF")!);
                b.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D5FEF")!);
                b.BorderThickness = new Thickness(1);
                b.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                b.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
                b.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
                b.BorderThickness = new Thickness(1);
                b.FontWeight = FontWeights.Normal;
            }
        }

        Hi(PillTable, tableSelected);
        Hi(PillShift, !tableSelected);
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(Window.GetWindow(this), "Export is not wired in this demo.", "Export", MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void BtnHistory_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(Window.GetWindow(this), "History is not wired in this demo.", "History", MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
            return;
        var copy = OpsServicesStore.CloneTable(_selected);
        copy.Id = Guid.NewGuid();
        copy.Name = copy.Name + " (copy)";
        copy.CreatedUtc = DateTime.UtcNow;
        copy.ModifiedUtc = DateTime.UtcNow;
        OpsServicesStore.AddTable(copy);
    }

    private void BtnClearTable_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
            return;
        _selected.OpsStatus = "Available";
        _selected.SeatedAtUtc = null;
        _selected.PartySize = null;
        OpsServicesStore.NotifyDataChanged();
    }

    private void BtnMerge_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(Window.GetWindow(this), "Merge tables is not wired in this demo.", "Merge", MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void BtnSplit_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(Window.GetWindow(this), "Split tables is not wired in this demo.", "Split", MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void BtnTransfer_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(Window.GetWindow(this), "Use Edit to change the assigned waiter.", "Transfer", MessageBoxButton.OK,
            MessageBoxImage.Information);
}
