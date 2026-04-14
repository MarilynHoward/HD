using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf;

public partial class OpsServicesEditReservation : UserControl
{
    private sealed class TableOption
    {
        public Guid Id { get; init; }
        public string Label { get; init; } = "";
    }

    private readonly Action _close;
    private readonly OpsReservation _model;

    public OpsServicesEditReservation(OpsReservation model, Action close)
    {
        _close = close ?? throw new ArgumentNullException(nameof(close));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        CmbStatus.ItemsSource = Enum.GetValues<OpsReservationStatus>();
        TxtCustomerName.Text = _model.CustomerName;
        TxtPhone.Text = _model.Phone;
        TxtEmail.Text = _model.Email ?? "";
        TxtPartySize.Text = _model.PartySize.ToString(CultureInfo.CurrentCulture);
        DpDate.SelectedDate = _model.Date.ToDateTime(TimeOnly.MinValue);
        TxtTime.Text = _model.Time.ToString("t", CultureInfo.CurrentCulture);
        TxtNotes.Text = _model.Notes;
        CmbStatus.SelectedItem = _model.Status;

        var floor = OpsServicesStore.NormalizeFloorNamePublic(_model.FloorName);
        var tables = OpsServicesStore.GetTables()
            .Where(t => t.IsActive
                        && string.Equals(
                            OpsServicesStore.NormalizeFloorNamePublic(t.LocationName), floor,
                            StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new TableOption
            {
                Id = t.Id,
                Label = $"{t.Name} ({t.SeatCount} seats)"
            })
            .ToList();
        CmbTable.ItemsSource = tables;
        CmbTable.SelectedItem = tables.FirstOrDefault(t => t.Id == _model.TableId) ?? tables.FirstOrDefault();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _close();

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _close();

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        TxtValidation.Visibility = Visibility.Collapsed;
        var name = (TxtCustomerName.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            ShowErr("Customer name is required.");
            return;
        }

        var phone = (TxtPhone.Text ?? "").Trim();
        if (string.IsNullOrEmpty(phone))
        {
            ShowErr("Phone number is required.");
            return;
        }

        if (CmbTable.SelectedItem is not TableOption opt)
        {
            ShowErr("Select a table.");
            return;
        }

        if (!int.TryParse((TxtPartySize.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture,
                out var party) || party < 1)
        {
            ShowErr("Enter a valid number of guests.");
            return;
        }

        if (DpDate.SelectedDate is not { } dt)
        {
            ShowErr("Select a date.");
            return;
        }

        var date = DateOnly.FromDateTime(dt);
        var timeText = (TxtTime.Text ?? "").Trim();
        if (!TimeOnly.TryParse(timeText, CultureInfo.CurrentCulture, DateTimeStyles.None, out var time)
            && !TimeOnly.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            ShowErr("Enter a valid time (for example 7:30 PM or 19:30).");
            return;
        }

        if (CmbStatus.SelectedItem is not OpsReservationStatus status)
        {
            ShowErr("Select a status.");
            return;
        }

        var table = OpsServicesStore.GetTable(opt.Id);
        _model.CustomerName = name;
        _model.Phone = phone;
        _model.Email = string.IsNullOrWhiteSpace(TxtEmail.Text) ? null : TxtEmail.Text.Trim();
        _model.PartySize = party;
        _model.Date = date;
        _model.Time = time;
        _model.Status = status;
        _model.Notes = (TxtNotes.Text ?? "").Trim();
        _model.TableId = opt.Id;
        _model.FloorName = table != null
            ? OpsServicesStore.NormalizeFloorNamePublic(table.LocationName)
            : _model.FloorName;

        OpsServicesStore.UpsertReservation(_model);
        _close();
    }

    private void ShowErr(string msg)
    {
        TxtValidation.Text = msg;
        TxtValidation.Visibility = Visibility.Visible;
    }
}
