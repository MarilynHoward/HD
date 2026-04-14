using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public partial class OpsServicesManageFloors : UserControl
{
    public sealed class FloorRowVm
    {
        public FloorRowVm(string name, int tableCount)
        {
            Name = name;
            TableCount = tableCount;
        }

        public string Name { get; }
        public int TableCount { get; }
        public bool CanDelete => TableCount == 0;
    }

    private readonly Action _close;
    private string? _renameFrom;
    private string? _pendingDeleteFloorName;
    private bool _storeRefreshPosted;
    private bool _manageFloorsUnloaded;

    public OpsServicesManageFloors(Action closeDialog)
    {
        _close = closeDialog ?? throw new ArgumentNullException(nameof(closeDialog));
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _manageFloorsUnloaded = false;
        OpsServicesStore.EnsureSeeded();
        RefreshList();
        OpsServicesStore.DataChanged += OnStoreChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        _manageFloorsUnloaded = true;
        OpsServicesStore.DataChanged -= OnStoreChanged;
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (_manageFloorsUnloaded || _storeRefreshPosted)
            return;
        _storeRefreshPosted = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _storeRefreshPosted = false;
            if (_manageFloorsUnloaded)
                return;
            RefreshList();
        }), DispatcherPriority.DataBind);
    }

    private void RefreshList()
    {
        FloorsList.ItemsSource = OpsServicesStore.GetFloorSummaries()
            .Select(p => new FloorRowVm(p.Name, p.TableCount))
            .ToList();
        _renameFrom = null;
        RenamePanel.Visibility = Visibility.Collapsed;
        _pendingDeleteFloorName = null;
        DeleteConfirmPanel.Visibility = Visibility.Collapsed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _close();

    private void ShowError(string? msg)
    {
        if (string.IsNullOrEmpty(msg))
        {
            TxtError.Visibility = Visibility.Collapsed;
            return;
        }

        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }

    private void AddFloorInner_Click(object sender, RoutedEventArgs e)
    {
        ShowError(null);
        if (!OpsServicesStore.TryAddFloor(TxtNewFloor.Text, out var err))
        {
            ShowError(err);
            return;
        }

        TxtNewFloor.Clear();
    }

    private void EditFloorRow_Click(object sender, RoutedEventArgs e)
    {
        ShowError(null);
        HideDeleteConfirm();
        if (sender is not Button b || b.Tag is not string name)
            return;
        _renameFrom = name;
        TxtRenameTitle.Text = $"Rename \"{name}\"";
        TxtRename.Text = name;
        RenamePanel.Visibility = Visibility.Visible;
    }

    private void CancelRename_Click(object sender, RoutedEventArgs e)
    {
        _renameFrom = null;
        RenamePanel.Visibility = Visibility.Collapsed;
        ShowError(null);
    }

    private void ApplyRename_Click(object sender, RoutedEventArgs e)
    {
        ShowError(null);
        if (_renameFrom == null)
            return;
        if (!OpsServicesStore.TryRenameFloor(_renameFrom, TxtRename.Text, out var err))
        {
            ShowError(err);
            return;
        }

        _renameFrom = null;
        RenamePanel.Visibility = Visibility.Collapsed;
    }

    private void HideDeleteConfirm()
    {
        _pendingDeleteFloorName = null;
        DeleteConfirmPanel.Visibility = Visibility.Collapsed;
    }

    private void DeleteFloorRow_Click(object sender, RoutedEventArgs e)
    {
        ShowError(null);
        if (sender is not Button b || b.Tag is not string name)
            return;

        _renameFrom = null;
        RenamePanel.Visibility = Visibility.Collapsed;

        _pendingDeleteFloorName = name;
        TxtDeleteConfirm.Text = $"Delete floor \"{name}\"?";
        DeleteConfirmPanel.Visibility = Visibility.Visible;
    }

    private void CancelDeleteConfirm_Click(object sender, RoutedEventArgs e)
    {
        ShowError(null);
        HideDeleteConfirm();
    }

    private void ConfirmDeleteFloor_Click(object sender, RoutedEventArgs e)
    {
        ShowError(null);
        if (_pendingDeleteFloorName == null)
            return;

        var name = _pendingDeleteFloorName;
        HideDeleteConfirm();

        if (!OpsServicesStore.TryDeleteFloor(name, out var err))
            ShowError(err);
    }
}
