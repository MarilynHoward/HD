using System.Globalization;
using System.Windows;

namespace RestaurantPosWpf;

public partial class OpsServicesAddTable : UserControl
{
    private sealed class WaiterOption
    {
        public Guid? Id { get; init; }
        public string Label { get; init; } = "";
    }

    private readonly Action _close;

    public OpsServicesAddTable(Action closeDialog)
    {
        _close = closeDialog ?? throw new ArgumentNullException(nameof(closeDialog));
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        OpsServicesStore.EnsureSeeded();

        CmbShape.ItemsSource = new[] { "Square", "Round" };
        CmbShape.SelectedIndex = 0;

        var floors = OpsServicesStore.GetDistinctFloorNamesForFilter().ToList();
        const string defaultFloor = "Main Floor";
        if (!floors.Exists(s => string.Equals(s, defaultFloor, StringComparison.OrdinalIgnoreCase)))
            floors.Add(defaultFloor);
        floors.Sort(StringComparer.OrdinalIgnoreCase);
        CmbFloor.ItemsSource = floors;
        CmbFloor.SelectedItem = floors.FirstOrDefault(f => string.Equals(f, defaultFloor, StringComparison.OrdinalIgnoreCase))
            ?? floors.FirstOrDefault();

        var waiters = new List<WaiterOption> { new() { Id = null, Label = "Unassigned" } };
        waiters.AddRange(OpsServicesStore.GetEmployees()
            .Select(e => new WaiterOption { Id = e.Id, Label = e.Name }));
        CmbWaiter.ItemsSource = waiters;
        CmbWaiter.DisplayMemberPath = nameof(WaiterOption.Label);
        CmbWaiter.SelectedIndex = 0;

        CmbStatus.ItemsSource = new[]
        {
            "Available", "Reserved", "Hold", "Cleaning", "Maintenance", "Occupied"
        };
        CmbStatus.SelectedIndex = 0;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _close();

    private void AddTable_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        var name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ShowErr("Table name is required.");
            return;
        }

        if (!int.TryParse(TxtSeats.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var seats) ||
            seats < 1)
        {
            ShowErr("Enter a valid seat count.");
            return;
        }

        if (!int.TryParse(TxtZone.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var zone))
            zone = 1;
        if (!int.TryParse(TxtStation.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var station))
            station = 1;
        if (!int.TryParse(TxtTurn.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var turn))
            turn = 60;

        var floor = (CmbFloor.SelectedItem as string ?? "").Trim();
        if (string.IsNullOrEmpty(floor))
            floor = "Main Floor";

        Guid? waiterId = CmbWaiter.SelectedItem is WaiterOption wo ? wo.Id : null;

        var table = new OpsFloorTable
        {
            Id = Guid.NewGuid(),
            Name = name,
            SeatCount = seats,
            Shape = CmbShape.SelectedItem as string ?? "Square",
            LocationName = floor,
            AssignedWaiterId = waiterId,
            Zone = zone,
            Station = station,
            TurnTimeMinutes = turn,
            Status = CmbStatus.SelectedItem as string ?? "Available",
            Notes = TxtNotes.Text.Trim(),
            IsActive = ChkActive.IsChecked == true,
            Accessible = ChkAccessible.IsChecked == true,
            VipPriority = ChkVip.IsChecked == true,
            CanMerge = ChkMerge.IsChecked == true,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow,
            OpsStatus = ChkActive.IsChecked == true ? "Available" : "Inactive",
            OpsServerId = waiterId
        };

        OpsServicesStore.AddTable(table);
        _close();
    }

    private void ShowErr(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
