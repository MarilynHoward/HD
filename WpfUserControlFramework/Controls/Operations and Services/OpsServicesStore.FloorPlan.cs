using System.Globalization;
using System.Linq;
using System.Windows;

namespace RestaurantPosWpf;

public enum OpsReservationStatus
{
    Pending,
    Confirmed,
    Seated,
    Completed,
    Cancelled
}

/// <summary>Guest reservation for floor plan and reservation list (demo store; replace with backend).</summary>
public sealed class OpsReservation
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }
    /// <summary>Denormalized floor name for filtering (kept in sync with table on save in demo store).</summary>
    public string FloorName { get; set; } = "";
    public DateOnly Date { get; set; }
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public int PartySize { get; set; } = 2;
    public TimeOnly Time { get; set; }
    public OpsReservationStatus Status { get; set; } = OpsReservationStatus.Pending;
    public string Notes { get; set; } = "";
    public string Reference { get; set; } = "";

    public string TimeDisplay =>
        Time.ToString("h:mm tt", CultureInfo.CurrentCulture);

    public string TableLabel(OpsFloorTable? t) =>
        t == null ? "Table" : $"{t.Name} ({t.SeatCount} seats)";

    /// <summary>List card badge; resolves current table name from the store.</summary>
    public string TableDisplayLabel =>
        OpsServicesStore.GetTable(TableId)?.Name is { Length: > 0 } n ? n : "—";

    /// <summary>Line like "Table 3 • 6 seats" for management cards.</summary>
    public string TableSeatsLine =>
        OpsServicesStore.GetTable(TableId) is { } t ? $"{t.Name} • {t.SeatCount} seats" : TableDisplayLabel;

    public string DateAtTimeDisplay =>
        $"{Date:yyyy-MM-dd} at {TimeDisplay}";

    /// <summary>Reservation card: <c>yyyy-MM-dd hh:mm tt</c> (matches client reservation list).</summary>
    public string CardDateTimeLine =>
        $"{Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {Time.ToString("hh:mm tt", CultureInfo.InvariantCulture)}";

    public string DateGuestsSummary => $"{DateAtTimeDisplay}  •  {PartySize} guests";

    public string PartySizeGuestsDisplay => $"{PartySize} guests";

    public string EmailDisplay =>
        string.IsNullOrWhiteSpace(Email) ? "—" : Email!.Trim();

    public string PhoneDisplay =>
        string.IsNullOrWhiteSpace(Phone) ? "—" : Phone.Trim();

    public string PhoneEmailLine
    {
        get
        {
            var p = Phone ?? "";
            var e = string.IsNullOrWhiteSpace(Email) ? "" : Email!.Trim();
            if (string.IsNullOrEmpty(e))
                return p;
            if (string.IsNullOrEmpty(p))
                return e;
            return $"{p}  •  {e}";
        }
    }

    public string StatusBadgeDisplay => Status switch
    {
        OpsReservationStatus.Confirmed => "confirmed",
        OpsReservationStatus.Pending => "pending",
        OpsReservationStatus.Seated => "seated",
        OpsReservationStatus.Completed => "completed",
        OpsReservationStatus.Cancelled => "cancelled",
        _ => Status.ToString().ToLowerInvariant()
    };
}

public enum OpsReservationListFilter
{
    All,
    Reserved,
    Occupied
}

public static partial class OpsServicesStore
{
    private static readonly List<OpsReservation> Reservations = new();
    /// <summary>Canvas positions per (date, floor) and table id. Coordinates are logical (pre-zoom).</summary>
    private static readonly Dictionary<string, Dictionary<Guid, Point>> FloorPlanLayouts = new();

    private static string FloorPlanLayoutKey(DateOnly date, string floor) =>
        $"{date:yyyy-MM-dd}|{NormalizeFloorName(floor)}";

    public static IReadOnlyList<OpsReservation> GetReservations() => Reservations;

    public static OpsReservation? GetReservation(Guid id) =>
        Reservations.FirstOrDefault(r => r.Id == id);

    /// <summary>Reservations for a calendar day on a given floor (by denormalized <see cref="OpsReservation.FloorName"/>).</summary>
    public static IEnumerable<OpsReservation> GetReservationsForFloorAndDate(DateOnly date, string floor)
    {
        var f = NormalizeFloorName(floor);
        return Reservations.Where(r =>
            r.Date == date && string.Equals(NormalizeFloorName(r.FloorName), f, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tables offered when assigning a reservation: <paramref name="alwaysIncludeTableId"/> is always listed
    /// (the guest’s current table, even if inactive). Other entries are active tables on the floor with no
    /// active reservation on <paramref name="date"/> (excluding <paramref name="editingReservationId"/> so the
    /// current booking does not block its own table).
    /// </summary>
    public static IEnumerable<OpsFloorTable> GetTablesEligibleForReservation(
        DateOnly date,
        string floor,
        Guid? editingReservationId,
        Guid alwaysIncludeTableId)
    {
        var fN = NormalizeFloorNamePublic(floor);
        var busyTableIds = new HashSet<Guid>();
        foreach (var r in Reservations)
        {
            if (r.Date != date)
                continue;
            if (r.Status is OpsReservationStatus.Completed or OpsReservationStatus.Cancelled)
                continue;
            if (editingReservationId.HasValue && r.Id == editingReservationId.Value)
                continue;
            busyTableIds.Add(r.TableId);
        }

        return Tables
            .Where(t => string.Equals(NormalizeFloorNamePublic(t.LocationName), fN, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Id == alwaysIncludeTableId || (t.IsActive && !busyTableIds.Contains(t.Id)))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether the table may be saved for this reservation (same rules as <see cref="GetTablesEligibleForReservation"/>).</summary>
    public static bool IsTableEligibleForReservation(
        DateOnly date,
        string floor,
        Guid? editingReservationId,
        Guid tableId,
        Guid alwaysIncludeTableId) =>
        GetTablesEligibleForReservation(date, floor, editingReservationId, alwaysIncludeTableId)
            .Any(t => t.Id == tableId);

    public static IEnumerable<OpsReservation> GetReservationsForList(
        DateOnly date,
        string floor,
        OpsReservationListFilter filter,
        string? searchTrimmed)
    {
        IEnumerable<OpsReservation> q = GetReservationsForFloorAndDate(date, floor);
        q = filter switch
        {
            OpsReservationListFilter.Reserved => q.Where(r =>
                r.Status is OpsReservationStatus.Confirmed or OpsReservationStatus.Pending),
            OpsReservationListFilter.Occupied => q.Where(r => r.Status == OpsReservationStatus.Seated),
            _ => q
        };

        if (string.IsNullOrEmpty(searchTrimmed))
            return q.OrderBy(r => r.Time).ThenBy(r => r.CustomerName);

        var s = searchTrimmed;
        return q.Where(r =>
                (r.CustomerName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (r.Phone ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || (r.Reference ?? "").Contains(s, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Time).ThenBy(r => r.CustomerName);
    }

    /// <summary>
    /// Active reservation for table/day (excludes completed/cancelled). When multiple exist, prefers Seated, then Confirmed/Pending.
    /// </summary>
    public static OpsReservation? GetActiveReservationForTable(DateOnly date, Guid tableId)
    {
        var act = Reservations
            .Where(r => r.Date == date && r.TableId == tableId
                        && r.Status is not OpsReservationStatus.Completed
                        && r.Status is not OpsReservationStatus.Cancelled)
            .ToList();
        if (act.Count == 0)
            return null;
        return act
            .OrderByDescending(r => r.Status switch
            {
                OpsReservationStatus.Seated => 3,
                OpsReservationStatus.Confirmed => 2,
                OpsReservationStatus.Pending => 1,
                _ => 0
            })
            .ThenBy(r => r.Time)
            .First();
    }

    /// <summary>Floor-plan card state: green = available, red = reserved (confirmed/pending), orange = seated.</summary>
    public static OpsFloorPlanTableVisualKind GetFloorPlanTableVisualKind(DateOnly date, Guid tableId)
    {
        var r = GetActiveReservationForTable(date, tableId);
        if (r == null)
            return OpsFloorPlanTableVisualKind.Available;
        return r.Status switch
        {
            OpsReservationStatus.Seated => OpsFloorPlanTableVisualKind.Occupied,
            OpsReservationStatus.Confirmed or OpsReservationStatus.Pending => OpsFloorPlanTableVisualKind.Reserved,
            _ => OpsFloorPlanTableVisualKind.Available
        };
    }

    /// <summary>
    /// All staff allocated to this table on <paramref name="date"/>, as <c>Name (HH:mm–HH:mm)</c> per scheduled shift,
    /// ordered by shift start (so split coverage e.g. lunch + dinner is easy to read). Ignores reservation time so
    /// mismatches (guest after last shift ends) are visible at a glance. If there are no shift rows for this table,
    /// falls back to the table’s assigned waiter display name when set (no time window in that case).
    /// </summary>
    public static string GetPrimaryStaffForTableOnDate(Guid tableId, DateOnly date)
    {
        var segments = Shifts
            .Where(s => s.Date == date && s.TableIds.Contains(tableId))
            .OrderBy(s => s.Start)
            .Select(FormatShiftSegment)
            .Where(x => x != null)
            .Cast<string>()
            .ToList();
        if (segments.Count > 0)
            return string.Join(", ", segments);

        var table = GetTable(tableId);
        return table?.AssignedWaiterDisplay?.Trim() is { Length: > 0 } w ? w : "";
    }

    private static string? FormatShiftSegment(OpsScheduledShift s)
    {
        var name = GetEmployee(s.EmployeeId)?.Name;
        return string.IsNullOrEmpty(name) ? null : $"{name} ({FormatShiftWindow(s.Start, s.End)})";
    }

    private static string FormatShiftWindow(TimeOnly start, TimeOnly end) =>
        $"{start:HH:mm}\u2013{end:HH:mm}";

    public static bool TryGetFloorPlanTablePosition(DateOnly date, string floor, Guid tableId, out double x,
        out double y)
    {
        x = y = 0;
        var key = FloorPlanLayoutKey(date, floor);
        if (!FloorPlanLayouts.TryGetValue(key, out var map))
            return false;
        if (!map.TryGetValue(tableId, out var p))
            return false;
        x = p.X;
        y = p.Y;
        return true;
    }

    public static void SetFloorPlanTablePosition(DateOnly date, string floor, Guid tableId, double x, double y)
    {
        EnsureLoaded();
        var normFloor = NormalizeFloorName(floor);
        var key = FloorPlanLayoutKey(date, normFloor);
        if (!FloorPlanLayouts.TryGetValue(key, out var map))
        {
            map = new Dictionary<Guid, Point>();
            FloorPlanLayouts[key] = map;
        }

        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            // A fresh layoutId is only consumed by the INSERT arm when no row exists yet for
            // this (date, floor, table) tuple; the UPDATE arm keys on the UNIQUE index.
            App.aps.Execute(cn, App.aps.sql.UpsertOpsFloorPlanLayout(
                Guid.NewGuid(), date, normFloor, tableId, x, y,
                App.aps.signedOnUserId, isSeed: false));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] SetFloorPlanTablePosition persist failed: " + ex.Message);
        }

        map[tableId] = new Point(x, y);
        DataChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Ensures every table id has a position; uses a simple grid layout when missing.</summary>
    public static void EnsureDefaultFloorPlanPositions(DateOnly date, string floor, IReadOnlyList<OpsFloorTable> tablesOnFloor)
    {
        var key = FloorPlanLayoutKey(date, floor);
        if (!FloorPlanLayouts.TryGetValue(key, out var map))
        {
            map = new Dictionary<Guid, Point>();
            FloorPlanLayouts[key] = map;
        }

        var colW = 200.0;
        var rowH = 130.0;
        var cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, tablesOnFloor.Count))));
        for (var i = 0; i < tablesOnFloor.Count; i++)
        {
            var t = tablesOnFloor[i];
            if (map.ContainsKey(t.Id))
                continue;
            var col = i % cols;
            var row = i / cols;
            map[t.Id] = new Point(32 + col * colW, 32 + row * rowH);
        }
    }

    public static void UpsertReservation(OpsReservation reservation)
    {
        EnsureLoaded();
        var table = GetTable(reservation.TableId);
        if (table != null)
            reservation.FloorName = NormalizeFloorName(table.LocationName);
        if (reservation.Id == Guid.Empty)
            reservation.Id = Guid.NewGuid();

        var idx = Reservations.FindIndex(r => r.Id == reservation.Id);
        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            var write = ToReservationWrite(reservation);
            var sql = idx >= 0
                ? App.aps.sql.UpdateOpsReservation(write, App.aps.signedOnUserId)
                : App.aps.sql.InsertOpsReservation(write, App.aps.signedOnUserId);
            App.aps.Execute(cn, sql);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] UpsertReservation persist failed: " + ex.Message);
        }

        if (idx >= 0)
            Reservations[idx] = reservation;
        else
            Reservations.Add(reservation);
        DataChanged?.Invoke(null, EventArgs.Empty);
    }

    public static bool TryRemoveReservation(Guid id)
    {
        EnsureLoaded();
        var idx = Reservations.FindIndex(r => r.Id == id);
        if (idx < 0)
            return false;

        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            App.aps.Execute(cn, App.aps.sql.SoftDeleteOpsReservation(id, App.aps.signedOnUserId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] TryRemoveReservation persist failed: " + ex.Message);
        }

        Reservations.RemoveAt(idx);
        DataChanged?.Invoke(null, EventArgs.Empty);
        return true;
    }

    private static Sql.OpsReservationWrite ToReservationWrite(OpsReservation r) =>
        new()
        {
            ReservationId = r.Id,
            TableId = r.TableId,
            FloorName = NormalizeFloorName(r.FloorName),
            Date = r.Date,
            CustomerName = r.CustomerName ?? "",
            Phone = r.Phone ?? "",
            Email = r.Email,
            PartySize = r.PartySize,
            Time = r.Time,
            Status = r.Status.ToString(),
            Notes = r.Notes ?? "",
            Reference = r.Reference ?? "",
            IsSeed = false
        };

    /// <summary>
    /// Loads active reservations into the in-memory cache. Called from
    /// <see cref="LoadFromDbLocked"/> so the floor plan card state and reservation list are in
    /// sync with the database as soon as the store is first touched.
    /// </summary>
    private static void LoadReservations(string cn)
    {
        var dt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectAllOpsReservations(), 60);
        foreach (System.Data.DataRow r in dt.Rows)
        {
            try
            {
                Reservations.Add(OpsStoreRowReader.MapReservation(r));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OpsServicesStore] MapReservation failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Loads saved floor-plan table positions keyed by (date, floor). Missing coordinates are
    /// skipped; <see cref="EnsureDefaultFloorPlanPositions"/> later fills in grid defaults so the
    /// UI always has something to render even on a clean database.
    /// </summary>
    private static void LoadFloorPlanLayouts(string cn)
    {
        var dt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectAllOpsFloorPlanLayouts(), 60);
        foreach (System.Data.DataRow r in dt.Rows)
        {
            try
            {
                var date = OpsStoreRowReader.AsDate(r, "layout_date");
                var floor = OpsStoreRowReader.AsString(r, "floor_name");
                var tableId = OpsStoreRowReader.AsGuid(r, "table_id");
                var x = OpsStoreRowReader.AsDouble(r, "pos_x", 0);
                var y = OpsStoreRowReader.AsDouble(r, "pos_y", 0);
                if (tableId == Guid.Empty)
                    continue;
                var key = FloorPlanLayoutKey(date, floor);
                if (!FloorPlanLayouts.TryGetValue(key, out var map))
                {
                    map = new Dictionary<Guid, Point>();
                    FloorPlanLayouts[key] = map;
                }
                map[tableId] = new Point(x, y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OpsServicesStore] MapFloorPlanLayout failed: " + ex.Message);
            }
        }
    }
}

public enum OpsFloorPlanTableVisualKind
{
    Available,
    Reserved,
    Occupied
}
