using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public partial class OpsServicesReservationsManagement : UserControl
{
    private readonly Action _close;
    private readonly string _floor;
    private readonly DateOnly _date;
    private readonly OpsReservationListFilter _listFilter;
    private readonly string? _reservationSearchTrimmed;
    private readonly Guid? _focusReservationId;

    private Guid? _pendingDeleteReservationId;
    private OpsReservation? _editorDraft;
    private bool _editorIsNew;
    /// <summary>Table id when the editor opened (always listed in combo even if inactive).</summary>
    private Guid _editorOriginalTableId;
    private int _inlinePartySize = 2;
    private bool _inlineEditorEventsHooked;
    private bool _storeRefreshPosted;
    private bool _reservationsControlUnloaded;

    private sealed class InlineTableOption
    {
        public Guid Id { get; init; }
        public string Label { get; init; } = "";
    }

    private sealed class ReservationTimeOption
    {
        public TimeOnly Time { get; init; }
        public string Display { get; init; } = "";
    }

    private static readonly Lazy<List<ReservationTimeOption>> ReservationTimeSlots = new(() =>
    {
        var list = new List<ReservationTimeOption>();
        for (var m = 0; m < 24 * 60; m += 15)
        {
            var t = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(m));
            list.Add(new ReservationTimeOption
            {
                Time = t,
                Display = t.ToString("t", CultureInfo.CurrentCulture)
            });
        }

        return list;
    });

    public OpsServicesReservationsManagement(
        Action close,
        string floorName,
        DateOnly date,
        OpsReservationListFilter listFilter,
        string? reservationSearchTrimmed,
        Guid? focusReservationId)
    {
        _close = close ?? throw new ArgumentNullException(nameof(close));
        _floor = floorName ?? throw new ArgumentNullException(nameof(floorName));
        _date = date;
        _listFilter = listFilter;
        _reservationSearchTrimmed = reservationSearchTrimmed;
        _focusReservationId = focusReservationId;
        App.OpsTrace("OpsServicesReservationsManagement: before InitializeComponent");
        InitializeComponent();
        App.OpsTrace("OpsServicesReservationsManagement: after InitializeComponent");
        OpsServicesStore.DataChanged += OnStoreChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.OpsTrace("OpsServicesReservationsManagement.OnLoaded: enter");
        var dateLine = _date.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
        TxtContextLine.Text = _focusReservationId is { }
            ? $"{_floor} · {dateLine} · Selected reservation"
            : $"{_floor} · {dateLine}";
        CmbInlineStatus.ItemsSource = Enum.GetValues<OpsReservationStatus>();
        _reservationsControlUnloaded = false;
        if (!_inlineEditorEventsHooked)
        {
            _inlineEditorEventsHooked = true;
            // Text fields use XAML TextChanged; only wire combos here (avoids duplicate handlers).
            CmbInlineTable.SelectionChanged += InlineCombo_SelectionChanged;
            CmbInlineStatus.SelectionChanged += InlineCombo_SelectionChanged;
            CmbInlineTime.SelectionChanged += InlineCombo_SelectionChanged;
        }

        App.OpsTrace("OpsServicesReservationsManagement.OnLoaded: before Refresh");
        Refresh();
        App.OpsTrace("OpsServicesReservationsManagement.OnLoaded: after Refresh");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _reservationsControlUnloaded = true;
        OpsServicesStore.DataChanged -= OnStoreChanged;
    }

    /// <summary>
    /// Defer refresh so we never synchronously re-enter <see cref="Refresh"/> from <see cref="OpsServicesStore"/>
    /// notifications (e.g. save path), which can freeze or overflow the stack when multiple subscribers use
    /// <see cref="Dispatcher.Invoke"/>.
    /// </summary>
    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (_reservationsControlUnloaded || _storeRefreshPosted)
            return;
        _storeRefreshPosted = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _storeRefreshPosted = false;
            if (_reservationsControlUnloaded)
                return;
            Refresh();
        }), DispatcherPriority.DataBind);
    }

    private void InlineEditorField_Changed(object sender, TextChangedEventArgs e) =>
        UpdateInlineEditorValidation();

    private void InlineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateInlineEditorValidation();

    private void Refresh()
    {
        List<OpsReservation> rows;
        if (_focusReservationId is { } focusId)
        {
            var r = OpsServicesStore.GetReservation(focusId);
            var floorOk = r != null
                          && string.Equals(
                              OpsServicesStore.NormalizeFloorNamePublic(r.FloorName),
                              OpsServicesStore.NormalizeFloorNamePublic(_floor),
                              StringComparison.OrdinalIgnoreCase);
            if (r != null
                && r.Date == _date
                && floorOk
                && r.Status is not OpsReservationStatus.Completed
                && r.Status is not OpsReservationStatus.Cancelled)
                rows = new List<OpsReservation> { r };
            else
                rows = new List<OpsReservation>();
        }
        else
        {
            rows = OpsServicesStore.GetReservationsForList(
                    _date,
                    _floor,
                    _listFilter,
                    _reservationSearchTrimmed)
                .Where(r => r.Status is not OpsReservationStatus.Completed
                            and not OpsReservationStatus.Cancelled)
                .ToList();
        }

        var vms = rows
            .Select(r => new OpsReservationListRowVm(r, _pendingDeleteReservationId == r.Id))
            .ToList();
        ReservationsList.ItemsSource = vms;

        var c = rows.Count(r => r.Status == OpsReservationStatus.Confirmed);
        var p = rows.Count(r => r.Status == OpsReservationStatus.Pending);
        var s = rows.Count(r => r.Status == OpsReservationStatus.Seated);
        TxtStatConfirmed.Text = c.ToString(CultureInfo.CurrentCulture);
        TxtStatPending.Text = p.ToString(CultureInfo.CurrentCulture);
        TxtStatSeated.Text = s.ToString(CultureInfo.CurrentCulture);
        TxtStatTotal.Text = rows.Count.ToString(CultureInfo.CurrentCulture);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _close();

    private void AddNewReservation_Click(object sender, RoutedEventArgs e)
    {
        var tables = OpsServicesStore.GetTables()
            .Where(t => t.IsActive && string.Equals(
                OpsServicesStore.NormalizeFloorNamePublic(t.LocationName),
                OpsServicesStore.NormalizeFloorNamePublic(_floor), StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var first = tables.FirstOrDefault();
        if (first == null)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Add an active table for this floor before creating a reservation.",
                "Reservations",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var draft = new OpsReservation
        {
            Id = Guid.NewGuid(),
            TableId = first.Id,
            FloorName = OpsServicesStore.NormalizeFloorNamePublic(_floor),
            Date = _date,
            CustomerName = "",
            Phone = "",
            PartySize = 2,
            Time = new TimeOnly(18, 0),
            Status = OpsReservationStatus.Pending
        };
        BeginInlineEditor(draft, isNew: true);
    }

    private void EditReservation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Guid id)
            return;
        var r = OpsServicesStore.GetReservation(id);
        if (r == null)
            return;
        BeginInlineEditor(CloneReservation(r), isNew: false);
    }

    private void DeleteReservation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Guid id)
            return;
        if (OpsServicesStore.GetReservation(id) == null)
            return;
        _pendingDeleteReservationId = _pendingDeleteReservationId == id ? null : id;
        Refresh();
    }

    private void ConfirmDeleteReservation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Guid id)
            return;
        OpsServicesStore.TryRemoveReservation(id);
        _pendingDeleteReservationId = null;
        HideInlineEditor();
        Refresh();
    }

    private void CancelDeleteReservation_Click(object sender, RoutedEventArgs e)
    {
        _pendingDeleteReservationId = null;
        Refresh();
    }

    private void BeginInlineEditor(OpsReservation model, bool isNew)
    {
        _pendingDeleteReservationId = null;
        Refresh();
        _editorDraft = model;
        _editorOriginalTableId = model.TableId;
        _editorIsNew = isNew;
        TxtInlineEditorTitle.Text = isNew ? "Add New Reservation" : "Edit Reservation";
        TxtInlineEditorSubtitle.Text = isNew ? "Create a new table reservation." : "Update reservation details.";
        BtnInlineSave.Content = isNew ? "Add Reservation" : "Save Changes";
        TxtInlineValidation.Visibility = Visibility.Collapsed;
        LoadInlineFormFromDraft();
        InlineReservationEditorPanel.Visibility = Visibility.Visible;
        ReservationsBodyScrollViewer.ScrollToVerticalOffset(0);
        UpdateInlineEditorValidation();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            TxtInlineCustomerName.Focus();
            Keyboard.Focus(TxtInlineCustomerName);
        }), DispatcherPriority.Loaded);
    }

    private void HideInlineEditor()
    {
        InlineReservationEditorPanel.Visibility = Visibility.Collapsed;
        _editorDraft = null;
    }

    private void BtnInlineCancel_Click(object sender, RoutedEventArgs e) => HideInlineEditor();

    private void BtnInlineSave_Click(object sender, RoutedEventArgs e)
    {
        if (!BtnInlineSave.IsEnabled || _editorDraft == null)
            return;
        UpdateInlineEditorValidation();
        if (!BtnInlineSave.IsEnabled)
            return;

        var name = (TxtInlineCustomerName.Text ?? "").Trim();
        var phone = (TxtInlinePhone.Text ?? "").Trim();
        if (CmbInlineTable.SelectedItem is not InlineTableOption opt)
            return;
        if (CmbInlineTime.SelectedItem is not ReservationTimeOption timeOpt)
            return;
        if (DpInlineDate.SelectedDate is not { } dt)
            return;
        if (CmbInlineStatus.SelectedItem is not OpsReservationStatus status)
            return;

        var date = DateOnly.FromDateTime(dt);
        if (!OpsServicesStore.IsTableEligibleForReservation(
                date,
                _floor,
                _editorIsNew ? null : _editorDraft.Id,
                opt.Id,
                _editorOriginalTableId))
        {
            ShowInlineErr("Selected table is not available for this date (inactive or already reserved).");
            return;
        }

        var table = OpsServicesStore.GetTable(opt.Id);
        _editorDraft.CustomerName = name;
        _editorDraft.Phone = phone;
        _editorDraft.Email = string.IsNullOrWhiteSpace(TxtInlineEmail.Text) ? null : TxtInlineEmail.Text.Trim();
        _editorDraft.PartySize = _inlinePartySize;
        _editorDraft.Date = date;
        _editorDraft.Time = timeOpt.Time;
        _editorDraft.Status = status;
        _editorDraft.Notes = (TxtInlineNotes.Text ?? "").Trim();
        _editorDraft.TableId = opt.Id;
        _editorDraft.FloorName = table != null
            ? OpsServicesStore.NormalizeFloorNamePublic(table.LocationName)
            : _editorDraft.FloorName;

        OpsServicesStore.UpsertReservation(_editorDraft);
        HideInlineEditor();
    }

    private void ShowInlineErr(string msg)
    {
        TxtInlineValidation.Text = msg;
        TxtInlineValidation.Visibility = Visibility.Visible;
    }

    private void LoadInlineFormFromDraft()
    {
        if (_editorDraft == null)
            return;
        TxtInlineCustomerName.Text = _editorDraft.CustomerName;
        TxtInlinePhone.Text = _editorDraft.Phone;
        TxtInlineEmail.Text = _editorDraft.Email ?? "";
        _inlinePartySize = Math.Clamp(_editorDraft.PartySize, 1, 99);
        UpdatePartySizeDisplay();
        DpInlineDate.SelectedDate = _editorDraft.Date.ToDateTime(TimeOnly.MinValue);
        TxtInlineNotes.Text = _editorDraft.Notes;
        CmbInlineStatus.SelectedItem = _editorDraft.Status;

        PopulateInlineTimeCombo();
        PopulateInlineTableCombo();
    }

    private void PopulateInlineTimeCombo()
    {
        var slots = ReservationTimeSlots.Value;
        CmbInlineTime.ItemsSource = slots;
        if (_editorDraft == null)
            return;
        var pick = FindNearestTimeOption(_editorDraft.Time, slots);
        CmbInlineTime.SelectedItem = pick;
    }

    private static ReservationTimeOption FindNearestTimeOption(TimeOnly desired, List<ReservationTimeOption> slots)
    {
        ReservationTimeOption? best = null;
        var bestTicks = long.MaxValue;
        foreach (var s in slots)
        {
            var d = Math.Abs((s.Time - desired).Ticks);
            if (d < bestTicks)
            {
                bestTicks = d;
                best = s;
            }
        }

        return best ?? slots[0];
    }

    private void DpInlineDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_editorDraft == null || InlineReservationEditorPanel.Visibility != Visibility.Visible)
            return;
        PopulateInlineTableCombo();
        UpdateInlineEditorValidation();
    }

    private void PopulateInlineTableCombo()
    {
        if (_editorDraft == null)
            return;
        var date = DpInlineDate.SelectedDate is { } d
            ? DateOnly.FromDateTime(d)
            : _editorDraft.Date;
        var tables = OpsServicesStore
            .GetTablesEligibleForReservation(
                date,
                _floor,
                _editorIsNew ? null : _editorDraft.Id,
                _editorOriginalTableId)
            .Select(t => new InlineTableOption
            {
                Id = t.Id,
                Label = t.IsActive
                    ? $"{t.Name} ({t.SeatCount} seats)"
                    : $"{t.Name} ({t.SeatCount} seats) (inactive)"
            })
            .ToList();
        CmbInlineTable.ItemsSource = tables;
        var preferred = tables.FirstOrDefault(t => t.Id == _editorDraft.TableId)
                        ?? tables.FirstOrDefault(t => t.Id == _editorOriginalTableId);
        CmbInlineTable.SelectedItem = preferred ?? tables.FirstOrDefault();
        if (CmbInlineTable.SelectedItem is InlineTableOption sel)
            _editorDraft.TableId = sel.Id;
        UpdateInlineEditorValidation();
    }

    private void PartySizeDecrement_Click(object sender, RoutedEventArgs e)
    {
        if (_inlinePartySize > 1)
            _inlinePartySize--;
        UpdatePartySizeDisplay();
        UpdateInlineEditorValidation();
    }

    private void PartySizeIncrement_Click(object sender, RoutedEventArgs e)
    {
        if (_inlinePartySize < 99)
            _inlinePartySize++;
        UpdatePartySizeDisplay();
        UpdateInlineEditorValidation();
    }

    private void UpdatePartySizeDisplay() =>
        TxtInlinePartyCount.Text = _inlinePartySize.ToString(CultureInfo.CurrentCulture);

    private void TxtInlinePhone_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox tb)
            return;
        var start = tb.SelectionStart;
        var len = tb.SelectionLength;
        var next = tb.Text.Remove(start, len).Insert(start, e.Text);
        if (!IsAllowedPhoneCharacters(next))
            e.Handled = true;
    }

    private static bool IsAllowedPhoneCharacters(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '+')
            {
                if (i != 0)
                    return false;
                continue;
            }

            if (!char.IsDigit(c) && c != ' ')
                return false;
        }

        return true;
    }

    private static bool IsValidReservationPhone(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(s))
            return false;
        if (!IsAllowedPhoneCharacters(s))
            return false;
        if (s.StartsWith("+", StringComparison.Ordinal) && s.Length == 1)
            return false;
        return s.Any(c => char.IsDigit(c));
    }

    private static bool IsValidOptionalEmail(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(s))
            return true;
        // Avoid UI freezes from huge paste / pathological strings inside System.Net.Mail parsing.
        if (s.Length > 254)
            return false;
        var at = s.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at == s.Length - 1)
            return false;
        try
        {
            _ = new MailAddress(s);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void SetFieldError(TextBlock block, bool ok, string message)
    {
        if (ok)
        {
            block.Visibility = Visibility.Collapsed;
            block.Text = "";
        }
        else
        {
            block.Text = message;
            block.Visibility = Visibility.Visible;
        }
    }

    private void UpdateInlineEditorValidation()
    {
        if (InlineReservationEditorPanel.Visibility != Visibility.Visible)
            return;

        TxtInlineValidation.Visibility = Visibility.Collapsed;

        var nameOk = !string.IsNullOrWhiteSpace(TxtInlineCustomerName.Text);
        SetFieldError(TxtInlineCustomerNameError, nameOk, "Customer name is required.");

        var phoneOk = IsValidReservationPhone(TxtInlinePhone.Text);
        SetFieldError(TxtInlinePhoneError, phoneOk,
            "Enter a phone number. Use + only as the first character; otherwise digits and spaces only.");

        var emailOk = IsValidOptionalEmail(TxtInlineEmail.Text);
        SetFieldError(TxtInlineEmailError, emailOk, "Enter a valid email address or leave this field empty.");

        var partyOk = _inlinePartySize is >= 1 and <= 99;
        SetFieldError(TxtInlinePartyError, partyOk, "Guests must be between 1 and 99.");

        var dateOk = DpInlineDate.SelectedDate.HasValue;
        SetFieldError(TxtInlineDateError, dateOk, "Select a date.");

        var tableOk = CmbInlineTable.SelectedItem is InlineTableOption selTable;
        var date = dateOk && DpInlineDate.SelectedDate is { } dsel
            ? DateOnly.FromDateTime(dsel)
            : (DateOnly?)null;
        var tableEligible = tableOk
                            && date.HasValue
                            && _editorDraft != null
                            && CmbInlineTable.SelectedItem is InlineTableOption topt
                            && OpsServicesStore.IsTableEligibleForReservation(
                                date.Value,
                                _floor,
                                _editorIsNew ? null : _editorDraft.Id,
                                topt.Id,
                                _editorOriginalTableId);
        if (!tableOk)
            SetFieldError(TxtInlineTableError, false, "Select a table.");
        else if (dateOk && _editorDraft != null && !tableEligible)
            SetFieldError(TxtInlineTableError, false,
                "That table is not available on the selected date (inactive or already reserved).");
        else
            SetFieldError(TxtInlineTableError, true, "");

        var timeOk = CmbInlineTime.SelectedItem is ReservationTimeOption;
        SetFieldError(TxtInlineTimeError, timeOk, "Select a time.");

        var statusOk = CmbInlineStatus.SelectedItem is OpsReservationStatus;

        BtnInlineSave.IsEnabled = nameOk && phoneOk && emailOk && partyOk && dateOk && tableOk && timeOk && statusOk
                                  && tableEligible;
    }

    private static OpsReservation CloneReservation(OpsReservation r) =>
        new()
        {
            Id = r.Id,
            TableId = r.TableId,
            FloorName = r.FloorName,
            Date = r.Date,
            CustomerName = r.CustomerName,
            Phone = r.Phone,
            Email = r.Email,
            PartySize = r.PartySize,
            Time = r.Time,
            Status = r.Status,
            Notes = r.Notes,
            Reference = r.Reference
        };
}
