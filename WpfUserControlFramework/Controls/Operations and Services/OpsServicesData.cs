namespace RestaurantPosWpf;

/// <summary>How the shift was created in the Add Shift dialog (for display).</summary>
public enum OpsShiftFrequencyKind
{
    Daily,
    Weekly,
    Monthly
}

public sealed class OpsEmployee
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Role { get; init; } = "";
    public bool IsOnShift { get; init; } = true;
    /// <summary>Hex color (e.g. #2563EB) for schedule blocks and list dot.</summary>
    public string AccentColorHex { get; init; } = "#2563EB";
}

/// <summary>Restaurant table / floor asset (shared across shift scheduling and table management).</summary>
public sealed class OpsFloorTable
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string LocationName { get; set; } = "";
    public int SeatCount { get; set; }
    public string Shape { get; set; } = "Square";
    public bool IsActive { get; set; } = true;
    public Guid? AssignedWaiterId { get; set; }
    public int Zone { get; set; } = 1;
    public int Station { get; set; } = 1;
    public int TurnTimeMinutes { get; set; } = 60;
    public string Status { get; set; } = "Available";
    public string Notes { get; set; } = "";
    public bool Accessible { get; set; }
    public bool VipPriority { get; set; }
    public bool CanMerge { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Operational UI state (Available, Occupied, …).</summary>
    public string OpsStatus { get; set; } = "Available";
    public Guid? OpsServerId { get; set; }
    public DateTime? SeatedAtUtc { get; set; }
    public int? PartySize { get; set; }
}

public sealed class OpsScheduledShift
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public DateOnly Date { get; init; }
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }
    public List<Guid> TableIds { get; init; } = new();
    public OpsShiftFrequencyKind SourceKind { get; init; }
}

/// <summary>Static demo store and conflict checks for Operations and Services.</summary>
public static class OpsServicesStore
{
    public static event EventHandler? DataChanged;

    private static readonly List<OpsEmployee> Employees = new();
    private static readonly List<OpsFloorTable> Tables = new();
    private static readonly List<OpsScheduledShift> Shifts = new();
    private static bool _seeded;

    public static DateTime StartOfWeekMonday(DateTime d)
    {
        var date = d.Date;
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    public static void EnsureSeeded()
    {
        if (_seeded)
            return;
        _seeded = true;
        Seed();
    }

    private static void Seed()
    {
        var today = DateTime.Today;
        var palette = new[]
        {
            "#2563EB", "#16A34A", "#7C3AED", "#DB2777", "#EA580C", "#0D9488", "#CA8A04", "#4F46E5"
        };

        var names = new (string Name, string Role)[]
        {
            ("John Smith", "Server"),
            ("Sarah Johnson", "Server"),
            ("Mike Brown", "Bartender"),
            ("Emily Davis", "Host"),
            ("Alex Lee", "Server"),
            ("Jordan Taylor", "Server"),
            ("Casey Morgan", "Bartender"),
            ("Riley Chen", "Host")
        };

        for (int i = 0; i < names.Length; i++)
        {
            Employees.Add(new OpsEmployee
            {
                Id = Guid.NewGuid(),
                Name = names[i].Name,
                Role = names[i].Role,
                IsOnShift = true,
                AccentColorHex = palette[i % palette.Length]
            });
        }

        void AddTable(string name, string floor, int seats, bool active = true, Guid? waiter = null)
        {
            Tables.Add(new OpsFloorTable
            {
                Id = Guid.NewGuid(),
                Name = name,
                LocationName = floor,
                SeatCount = seats,
                Shape = name.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? "Round" : "Square",
                IsActive = active,
                AssignedWaiterId = waiter,
                Status = "Available",
                OpsStatus = active ? "Available" : "Inactive",
                CreatedUtc = today.AddDays(-120).ToUniversalTime(),
                ModifiedUtc = today.AddDays(-1).ToUniversalTime()
            });
        }

        var john = Employees[0].Id;
        var sarah = Employees[1].Id;
        AddTable("Table 1", "Main Floor", 4, true, john);
        AddTable("Table 2", "Main Floor", 6, true, sarah);
        AddTable("Table 3", "Main Floor", 4, true, null);
        AddTable("Table 4", "Main Floor", 2, true, null);
        AddTable("Table 5", "Main Floor", 4, false, null);
        AddTable("Table 6", "Patio", 8, true, null);
        AddTable("VIP Table", "Patio", 6, true, null);

        if (Tables.Count > 1)
        {
            Tables[1].OpsStatus = "Occupied";
            Tables[1].SeatedAtUtc = DateTime.UtcNow.AddMinutes(-106);
            Tables[1].PartySize = 4;
            Tables[1].OpsServerId = john;
        }

        // Demo shifts: previous week through current week + 6 weeks (8 weeks total)
        var week0 = DateOnly.FromDateTime(StartOfWeekMonday(today.AddDays(-7)));
        for (int w = 0; w < 8; w++)
        {
            var monday = week0.AddDays(w * 7);
            // Scatter shifts across Mon–Sat
            AddDemoShift(Employees[0].Id, monday.AddDays(0), new TimeOnly(9, 0), new TimeOnly(17, 0),
                new[] { Tables[0].Id, Tables[1].Id }, OpsShiftFrequencyKind.Weekly);
            AddDemoShift(Employees[1].Id, monday.AddDays(1), new TimeOnly(10, 0), new TimeOnly(18, 0),
                new[] { Tables[1].Id }, OpsShiftFrequencyKind.Weekly);
            AddDemoShift(Employees[2].Id, monday.AddDays(2), new TimeOnly(12, 0), new TimeOnly(20, 0),
                Array.Empty<Guid>(), OpsShiftFrequencyKind.Daily);
            AddDemoShift(Employees[3].Id, monday.AddDays(3), new TimeOnly(9, 0), new TimeOnly(15, 0),
                new[] { Tables[2].Id, Tables[3].Id }, OpsShiftFrequencyKind.Daily);
            if (w % 2 == 0)
                AddDemoShift(Employees[4].Id, monday.AddDays(4), new TimeOnly(9, 0), new TimeOnly(17, 0),
                    new[] { Tables[5].Id }, OpsShiftFrequencyKind.Monthly);
            AddDemoShift(Employees[5].Id, monday.AddDays(5), new TimeOnly(11, 0), new TimeOnly(19, 0),
                new[] { Tables[0].Id }, OpsShiftFrequencyKind.Weekly);
        }
    }

    private static void AddDemoShift(Guid employeeId, DateOnly date, TimeOnly start, TimeOnly end,
        Guid[] tableIds, OpsShiftFrequencyKind kind)
    {
        Shifts.Add(new OpsScheduledShift
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            Date = date,
            Start = start,
            End = end,
            TableIds = tableIds.ToList(),
            SourceKind = kind
        });
    }

    public static IReadOnlyList<OpsEmployee> GetEmployees() => Employees;

    public static IReadOnlyList<OpsFloorTable> GetTables() => Tables;

    public static OpsEmployee? GetEmployee(Guid id) => Employees.FirstOrDefault(e => e.Id == id);

    public static OpsFloorTable? GetTable(Guid id) => Tables.FirstOrDefault(t => t.Id == id);

    public static IReadOnlyList<OpsScheduledShift> GetShifts() => Shifts;

    /// <summary>Monday of current week through Sunday of (current week + 4 weeks) — 5 weeks.</summary>
    public static (DateOnly Start, DateOnly End) GetDefaultWeeklyRecurrenceRange(DateTime today)
    {
        var monday = DateOnly.FromDateTime(StartOfWeekMonday(today));
        var endSunday = monday.AddDays(7 * 5 - 1);
        return (monday, endSunday);
    }

    /// <summary>True if the employee already has a shift on that date overlapping [start, end).</summary>
    public static bool HasTimeConflict(Guid employeeId, DateOnly date, TimeOnly start, TimeOnly end,
        Guid? excludeShiftId = null)
    {
        foreach (var s in Shifts)
        {
            if (excludeShiftId.HasValue && s.Id == excludeShiftId.Value)
                continue;
            if (s.EmployeeId != employeeId || s.Date != date)
                continue;
            if (start < s.End && end > s.Start)
                return true;
        }

        return false;
    }

    public static string? TryAddShifts(IEnumerable<OpsScheduledShift> newShifts)
    {
        var list = newShifts.ToList();
        foreach (var n in list)
        {
            if (HasTimeConflict(n.EmployeeId, n.Date, n.Start, n.End))
            {
                var emp = GetEmployee(n.EmployeeId)?.Name ?? "This staff member";
                return $"{emp} already has a shift on {n.Date:MMM d, yyyy} that overlaps this time slot. " +
                       "Choose a different time or date.";
            }
        }

        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                var a = list[i];
                var b = list[j];
                if (a.EmployeeId != b.EmployeeId || a.Date != b.Date)
                    continue;
                if (a.Start < b.End && a.End > b.Start)
                {
                    return "This combination creates overlapping shifts on the same day. Adjust days, dates, or times.";
                }
            }
        }

        foreach (var n in list)
            Shifts.Add(n);
        DataChanged?.Invoke(null, EventArgs.Empty);
        return null;
    }

    public static void AddTable(OpsFloorTable table)
    {
        Tables.Add(table);
        DataChanged?.Invoke(null, EventArgs.Empty);
    }

    public static bool RemoveTable(Guid id)
    {
        var idx = Tables.FindIndex(t => t.Id == id);
        if (idx < 0)
            return false;
        Tables.RemoveAt(idx);
        DataChanged?.Invoke(null, EventArgs.Empty);
        return true;
    }

    public static void NotifyDataChanged() => DataChanged?.Invoke(null, EventArgs.Empty);

    public static OpsFloorTable CloneTable(OpsFloorTable t) =>
        new()
        {
            Id = t.Id,
            Name = t.Name,
            LocationName = t.LocationName,
            SeatCount = t.SeatCount,
            Shape = t.Shape,
            IsActive = t.IsActive,
            AssignedWaiterId = t.AssignedWaiterId,
            Zone = t.Zone,
            Station = t.Station,
            TurnTimeMinutes = t.TurnTimeMinutes,
            Status = t.Status,
            Notes = t.Notes,
            Accessible = t.Accessible,
            VipPriority = t.VipPriority,
            CanMerge = t.CanMerge,
            CreatedUtc = t.CreatedUtc,
            ModifiedUtc = t.ModifiedUtc,
            OpsStatus = t.OpsStatus,
            OpsServerId = t.OpsServerId,
            SeatedAtUtc = t.SeatedAtUtc,
            PartySize = t.PartySize
        };

    public static void CopyTable(OpsFloorTable from, OpsFloorTable to)
    {
        to.Name = from.Name;
        to.LocationName = from.LocationName;
        to.SeatCount = from.SeatCount;
        to.Shape = from.Shape;
        to.IsActive = from.IsActive;
        to.AssignedWaiterId = from.AssignedWaiterId;
        to.Zone = from.Zone;
        to.Station = from.Station;
        to.TurnTimeMinutes = from.TurnTimeMinutes;
        to.Status = from.Status;
        to.Notes = from.Notes;
        to.Accessible = from.Accessible;
        to.VipPriority = from.VipPriority;
        to.CanMerge = from.CanMerge;
        to.ModifiedUtc = from.ModifiedUtc;
        to.OpsStatus = from.OpsStatus;
        to.OpsServerId = from.OpsServerId;
        to.SeatedAtUtc = from.SeatedAtUtc;
        to.PartySize = from.PartySize;
    }

    public static string TableNamesSummary(IEnumerable<Guid> ids)
    {
        var parts = ids.Select(id => GetTable(id)?.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }

    public static double TotalHoursForEmployeeInRange(Guid employeeId, DateOnly start, DateOnly end)
    {
        double h = 0;
        foreach (var s in Shifts)
        {
            if (s.EmployeeId != employeeId)
                continue;
            if (s.Date < start || s.Date > end)
                continue;
            var span = s.End.ToTimeSpan() - s.Start.ToTimeSpan();
            if (span.TotalHours < 0)
                span += TimeSpan.FromHours(24);
            h += span.TotalHours;
        }

        return Math.Round(h, 1);
    }
}
