using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace RestaurantPosWpf;

public enum StaffAccessRole
{
    Admin,
    Manager,
    Supervisor,
    User,
    System
}

/// <summary>Simulated remote-host sync for files written under <see cref="StaffAccessUserDetails.StaffDocumentsRepositoryRoot"/>.</summary>
public enum StaffFileRemoteSyncStatus
{
    PendingSync,
    Synced
}

/// <summary>Authoritative staff profile for Staff and Access; also feeds Operations staff pickers via <see cref="StaffAccessStore"/>.</summary>
public sealed class StaffUser
{
    public Guid Id { get; init; }

    public int NumericId { get; set; }
    public string UserName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string Surname { get; set; } = "";
    public string CardNumber { get; set; } = "";
    public StaffAccessRole AccessRole { get; set; } = StaffAccessRole.User;
    /// <summary>Operational job label (server, host, …) shown in shift scheduling and floor plan.</summary>
    public string JobTitle { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AccentColorHex { get; set; } = "#2563EB";
    public byte[]? ProfileImageBytes { get; set; }
    public byte[]? IdDocumentPdfBytes { get; set; }
    public string? IdDocumentFileName { get; set; }
    /// <summary>Relative path under <see cref="StaffAccessUserDetails.StaffDocumentsRepositoryRoot"/> for the profile image file, when mirrored on disk.</summary>
    public string? ProfileImageRepositoryRelativePath { get; set; }
    /// <summary>Simulated cloud sync for the ID PDF on disk (<c>docs\{NumericId}_id.pdf</c>).</summary>
    public StaffFileRemoteSyncStatus IdDocumentRemoteSyncStatus { get; set; } = StaffFileRemoteSyncStatus.Synced;
    /// <summary>Simulated cloud sync for the profile image on disk (<c>images\{NumericId}_profile.*</c>).</summary>
    public StaffFileRemoteSyncStatus ProfileImageRemoteSyncStatus { get; set; } = StaffFileRemoteSyncStatus.Synced;
    public bool BiometricEnrolled { get; set; }
    /// <summary>UTC instant of the last successful password change (demo + session saves).</summary>
    public DateTime? LastPasswordChangedUtc { get; set; }
    public bool PasswordChangedInSession { get; set; }

    public string DisplayName
    {
        get
        {
            var parts = new[] { FirstName, MiddleName, Surname }
                .Select(s => (s ?? "").Trim())
                .Where(s => s.Length > 0);
            var n = string.Join(" ", parts);
            return string.IsNullOrEmpty(n) ? UserName : n;
        }
    }
}

public sealed class StaffAccessListItemVm
{
    // Status pills: same soft treatment as OpsServicesReservationsManagement status chips.
    private static readonly Brush ActivePillBg = Freeze(new SolidColorBrush(Color.FromRgb(220, 252, 231)));
    private static readonly Brush ActivePillFg = Freeze(new SolidColorBrush(Color.FromRgb(22, 101, 52)));
    private static readonly Brush ActivePillBorder = Freeze(new SolidColorBrush(Color.FromRgb(134, 239, 172)));
    private static readonly Brush InactivePillBg = Freeze(new SolidColorBrush(Color.FromRgb(254, 226, 226)));
    private static readonly Brush InactivePillFg = Freeze(new SolidColorBrush(Color.FromRgb(153, 27, 27)));
    private static readonly Brush InactivePillBorder = Freeze(new SolidColorBrush(Color.FromRgb(252, 165, 165)));

    public StaffUser User { get; }

    public StaffAccessListItemVm(StaffUser user) => User = user;

    public string UserName => User.UserName;

    /// <summary>List row title when draft has no sign-in name yet.</summary>
    public string ListPrimaryLine =>
        !string.IsNullOrWhiteSpace(User.UserName)
            ? User.UserName
            : (!string.IsNullOrWhiteSpace(User.DisplayName)
                ? User.DisplayName
                : $"New account (ID {User.NumericId.ToString(CultureInfo.CurrentCulture)})");
    public string StatusLabel => User.IsActive ? "Active" : "Inactive";
    public string IdLine => $"ID: {User.NumericId.ToString(CultureInfo.CurrentCulture)}";
    public string RoleBadge => User.AccessRole.ToString();

    /// <summary>Delete is blocked while Operations still references this user (shifts or table server assignment).</summary>
    public bool CanDelete => !StaffAccessStore.IsUserAllocatedToSchedule(User.Id);

    public string DeleteToolTip =>
        CanDelete
            ? "Remove user from list"
            : "This user is linked in Operations (schedule or table assignment).";

    public Brush StatusPillBackground => User.IsActive ? ActivePillBg : InactivePillBg;
    public Brush StatusPillForeground => User.IsActive ? ActivePillFg : InactivePillFg;
    public Brush StatusPillBorderBrush => User.IsActive ? ActivePillBorder : InactivePillBorder;

    public Brush RoleBadgeBackground => User.AccessRole switch
    {
        StaffAccessRole.Admin => Freeze(new SolidColorBrush(Color.FromRgb(252, 231, 243))),
        StaffAccessRole.Manager => Freeze(new SolidColorBrush(Color.FromRgb(219, 234, 254))),
        StaffAccessRole.Supervisor => Freeze(new SolidColorBrush(Color.FromRgb(255, 237, 213))),
        StaffAccessRole.System => Freeze(new SolidColorBrush(Color.FromRgb(238, 242, 255))),
        _ => Freeze(new SolidColorBrush(Color.FromRgb(243, 244, 246)))
    };

    public Brush RoleBadgeForeground => User.AccessRole switch
    {
        StaffAccessRole.Admin => Freeze(new SolidColorBrush(Color.FromRgb(157, 23, 77))),
        StaffAccessRole.Manager => Freeze(new SolidColorBrush(Color.FromRgb(30, 64, 175))),
        StaffAccessRole.Supervisor => Freeze(new SolidColorBrush(Color.FromRgb(194, 65, 12))),
        StaffAccessRole.System => Freeze(new SolidColorBrush(Color.FromRgb(67, 56, 202))),
        _ => Freeze(new SolidColorBrush(Color.FromRgb(55, 65, 81)))
    };

    public Brush RoleBadgeBorderBrush => User.AccessRole switch
    {
        StaffAccessRole.Admin => Freeze(new SolidColorBrush(Color.FromRgb(251, 207, 232))),
        StaffAccessRole.Manager => Freeze(new SolidColorBrush(Color.FromRgb(147, 197, 253))),
        StaffAccessRole.Supervisor => Freeze(new SolidColorBrush(Color.FromRgb(253, 186, 116))),
        StaffAccessRole.System => Freeze(new SolidColorBrush(Color.FromRgb(199, 210, 254))),
        _ => Freeze(new SolidColorBrush(Color.FromRgb(229, 231, 235)))
    };

    private static T Freeze<T>(T brush) where T : Freezable
    {
        brush.Freeze();
        return brush;
    }
}

/// <summary>In-memory demo store for staff; Operations reads employees through <see cref="OpsServicesStore"/> helpers that map from here.</summary>
public static class StaffAccessStore
{
    public static event EventHandler? DataChanged;

    private static readonly List<StaffUser> Users = new();
    private static bool _seeded;

    /// <summary>Stable ids so Operations demo shifts and table assignments stay aligned across runs.</summary>
    private static readonly Guid[] SeedIds =
    {
        Guid.Parse("A1AAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAA01"),
        Guid.Parse("A1AAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAA02"),
        Guid.Parse("A1AAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAA03"),
        Guid.Parse("A1AAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAA04"),
        Guid.Parse("A1AAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAA05"),
        Guid.Parse("A1AAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAA06"),
        Guid.Parse("A1AAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAA07"),
        Guid.Parse("A1AAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAA08")
    };

    private static readonly string[] PaletteHex =
    {
        "#2563EB", "#16A34A", "#7C3AED", "#DB2777", "#EA580C", "#0D9488", "#CA8A04", "#4F46E5"
    };

    public static void EnsureSeeded()
    {
        if (_seeded)
            return;
        _seeded = true;

        var rows = new (string User, string First, string Mid, string Last, int NumId, StaffAccessRole Role, string Job,
            string Card)[]
        {
            ("john.smith", "John", "", "Smith", 1001, StaffAccessRole.User, "Server", "CARD-001"),
            ("sarah.johnson", "Sarah", "", "Johnson", 1002, StaffAccessRole.User, "Server", "CARD-002"),
            ("mike.brown", "Mike", "", "Brown", 1003, StaffAccessRole.Supervisor, "Bartender", "CARD-003"),
            ("emily.davis", "Emily", "", "Davis", 1004, StaffAccessRole.Manager, "Host", "CARD-004"),
            ("alex.lee", "Alex", "", "Lee", 1005, StaffAccessRole.User, "Server", "CARD-005"),
            ("jordan.taylor", "Jordan", "", "Taylor", 1006, StaffAccessRole.User, "Server", "CARD-006"),
            ("casey.morgan", "Casey", "", "Morgan", 1007, StaffAccessRole.Supervisor, "Bartender", "CARD-007"),
            ("riley.chen", "Riley", "", "Chen", 1008, StaffAccessRole.User, "Host", "CARD-008")
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            Users.Add(new StaffUser
            {
                Id = SeedIds[i],
                NumericId = r.NumId,
                UserName = r.User,
                FirstName = r.First,
                MiddleName = r.Mid,
                Surname = r.Last,
                CardNumber = r.Card,
                AccessRole = r.Role,
                JobTitle = r.Job,
                IsActive = true,
                AccentColorHex = PaletteHex[i % PaletteHex.Length],
                LastPasswordChangedUtc = new DateTime(2024, 11, 15, 12, 0, 0, DateTimeKind.Utc).AddDays(-i * 3)
            });
        }

        StaffAccessAuditRepository.InstallDemoEntriesIfNeeded(Users);
    }

    public static IReadOnlyList<StaffUser> GetUsers()
    {
        EnsureSeeded();
        return Users;
    }

    public static StaffUser? GetUser(Guid id)
    {
        EnsureSeeded();
        return Users.FirstOrDefault(u => u.Id == id);
    }

    /// <summary>
    /// True if Operations still references this staff member (cannot delete until cleared).
    /// Always calls <see cref="OpsServicesStore.EnsureSeeded"/> first so demo shifts / tables exist even when
    /// the user has never opened Shift Scheduling (otherwise <c>GetShifts()</c> stayed empty and every delete appeared allowed).
    /// </summary>
    public static bool IsUserAllocatedToSchedule(Guid userId)
    {
        EnsureSeeded();
        OpsServicesStore.EnsureSeeded();
        if (OpsServicesStore.GetShifts().Any(s => s.EmployeeId == userId))
            return true;
        return OpsServicesStore.GetTables().Any(t =>
            t.AssignedWaiterId == userId || t.OpsServerId == userId);
    }

    public static IReadOnlyList<OpsEmployee> GetEmployeesForOperations()
    {
        EnsureSeeded();
        return Users.Select(u => new OpsEmployee
        {
            Id = u.Id,
            Name = u.DisplayName,
            Role = u.JobTitle,
            IsOnShift = u.IsActive,
            AccentColorHex = u.AccentColorHex
        }).ToList();
    }

    public static OpsEmployee? GetOpsEmployee(Guid id)
    {
        EnsureSeeded();
        var u = Users.FirstOrDefault(x => x.Id == id);
        return u == null
            ? null
            : new OpsEmployee
            {
                Id = u.Id,
                Name = u.DisplayName,
                Role = u.JobTitle,
                IsOnShift = u.IsActive,
                AccentColorHex = u.AccentColorHex
            };
    }

    public static StaffUser AddUser(string? userName = null)
    {
        EnsureSeeded();
        var next = Users.Count == 0 ? 1001 : Users.Max(u => u.NumericId) + 1;
        var u = new StaffUser
        {
            Id = Guid.NewGuid(),
            NumericId = next,
            UserName = string.IsNullOrWhiteSpace(userName) ? "" : userName.Trim().ToLowerInvariant(),
            FirstName = "",
            MiddleName = "",
            Surname = "",
            CardNumber = $"CARD-{next.ToString(CultureInfo.InvariantCulture)}",
            AccessRole = StaffAccessRole.User,
            JobTitle = "",
            IsActive = true,
            AccentColorHex = PaletteHex[Users.Count % PaletteHex.Length]
        };
        Users.Add(u);
        Notify();
        return u;
    }

    public static bool TryRemoveUser(Guid id, out string? error)
    {
        EnsureSeeded();
        error = null;
        var idx = Users.FindIndex(u => u.Id == id);
        if (idx < 0)
        {
            error = "User not found.";
            return false;
        }

        if (IsUserAllocatedToSchedule(id))
        {
            error =
                "This user can't be deleted — they are still linked in Operations (scheduled shifts or table assignment).";
            return false;
        }

        Users.RemoveAt(idx);
        Notify();
        return true;
    }

    public static void Notify() => DataChanged?.Invoke(null, EventArgs.Empty);
}

public partial class StaffAccessUserDetails
{
    private readonly List<StaffAccessListItemVm> _rowModels = new();
    private StaffUser? _selected;
    private string _searchQuery = "";
    private bool _childEventsWired;
    private int _staffDetailTabIndex;
    private bool _staffDetailTabSyncInProgress;
    private ScrollViewer? _usersListInnerScrollViewer;
    private Visibility _usersListLastScrollBarVisibility = Visibility.Collapsed;
    private DispatcherTimer? _staffSaveToastTimer;
    private bool _performanceMetricsVisible;
    private bool _staffStoreMetricsHooked;
    private bool _suppressDirty;
    private bool _formDirty;
    private bool _suppressListSelectionHandlers;
    private Guid? _idAwaitingFirstSave;
    /// <summary>Last loaded/saved audit snapshot so saves detect profile/ID uploads vs. committed baseline.</summary>
    private StaffAccessAuditSnapshot? _auditCommittedSnapshot;

    /// <summary>
    /// Sign-in (active) flag at last load or successful save. Used for audit "before" state so it cannot drift
    /// from <see cref="StaffUser.IsActive"/> if anything syncs the model before Save reads the snapshot.
    /// </summary>
    private bool _auditBaselineIsActive = true;
    /// <summary>User id the audit overlay was opened for (so delete success/denial can refresh or close the panel).</summary>
    private Guid? _auditTrailBoundUserId;

    /// <summary>Disk root for staff uploads. ID PDFs → <c>docs\{NumericId}_id.pdf</c>; profile → <c>images\{NumericId}_profile.ext</c>.</summary>
    public static readonly string StaffDocumentsRepositoryRoot = @"D:\Dev\Cursor\HD\Documents";

    private DispatcherTimer? _idDocRemoteSyncTimer;
    private DispatcherTimer? _profileRemoteSyncTimer;

    public StaffAccessUserDetails()
    {
        InitializeComponent();
        Unloaded += StaffAccessUserDetails_OnUnloaded;
    }

    private void StaffAccessUserDetails_OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachUsersListScrollPaddingHook();
        _staffSaveToastTimer?.Stop();
        _staffSaveToastTimer = null;
        StopStaffFileRemoteSyncTimers();
        if (_staffStoreMetricsHooked)
        {
            StaffAccessStore.DataChanged -= StaffAccessStore_OnMetricsDataChanged;
            _staffStoreMetricsHooked = false;
        }
    }

    private enum StaffToastKind
    {
        Success,
        Error
    }

    private void ShowStaffToast(string message, StaffToastKind kind = StaffToastKind.Success)
    {
        StaffSaveToastText.Text = message;
        var primary = TryFindResource("Brush.PrimaryBlue") as Brush ?? Brushes.DodgerBlue;
        var danger = TryFindResource("Brush.DangerRed") as Brush ?? Brushes.Firebrick;
        var body = TryFindResource("MainForeground") as Brush ?? Brushes.Black;
        var dangerText = TryFindResource("Brush.DangerTextStrong") as Brush ?? Brushes.DarkRed;

        StaffSaveToastAccent.Background = kind == StaffToastKind.Success ? primary : danger;
        StaffSaveToastText.Foreground = kind == StaffToastKind.Success ? body : dangerText;

        StaffSaveToast.Visibility = Visibility.Visible;
        if (_staffSaveToastTimer == null)
        {
            _staffSaveToastTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2.6)
            };
            _staffSaveToastTimer.Tick += (_, _) =>
            {
                _staffSaveToastTimer.Stop();
                if (IsLoaded)
                    StaffSaveToast.Visibility = Visibility.Collapsed;
            };
        }

        _staffSaveToastTimer.Interval = kind == StaffToastKind.Error
            ? TimeSpan.FromSeconds(4.2)
            : TimeSpan.FromSeconds(2.6);
        _staffSaveToastTimer.Stop();
        _staffSaveToastTimer.Start();
    }

    private void StaffDetailTabRadio_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_staffDetailTabSyncInProgress)
            return;
        if (sender is not System.Windows.Controls.RadioButton rb || rb.IsChecked != true)
            return;
        var idx = rb.Tag switch
        {
            int i => i,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var j) => j,
            _ => -1
        };
        if (idx is < 0 or > 3)
            return;
        SelectStaffDetailTab(idx);
    }

    private void SelectStaffDetailTab(int index)
    {
        // Tab radios can fire Checked during InitializeComponent before row-1 panes exist; skip until named panes are ready.
        if (StaffPaneBasic == null
            || StaffPaneSecurity == null
            || StaffPaneBio == null
            || StaffPaneDocs == null)
        {
            _staffDetailTabIndex = Math.Clamp(index, 0, 3);
            return;
        }

        _staffDetailTabIndex = Math.Clamp(index, 0, 3);
        StaffPaneBasic.Visibility = _staffDetailTabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        StaffPaneSecurity.Visibility = _staffDetailTabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        StaffPaneBio.Visibility = _staffDetailTabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        StaffPaneDocs.Visibility = _staffDetailTabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        SyncStaffRadiosFromIndex();
    }

    private void SyncStaffRadiosFromIndex()
    {
        if (RadStaffTabBasic == null
            || RadStaffTabSecurity == null
            || RadStaffTabBio == null
            || RadStaffTabDocs == null)
            return;

        _staffDetailTabSyncInProgress = true;
        try
        {
            switch (_staffDetailTabIndex)
            {
                case 0:
                    RadStaffTabBasic.IsChecked = true;
                    break;
                case 1:
                    RadStaffTabSecurity.IsChecked = true;
                    break;
                case 2:
                    RadStaffTabBio.IsChecked = true;
                    break;
                default:
                    RadStaffTabDocs.IsChecked = true;
                    break;
            }
        }
        finally
        {
            _staffDetailTabSyncInProgress = false;
        }
    }

    private void StaffAccessUserDetails_OnLoaded(object sender, RoutedEventArgs e)
    {
        StaffAccessStore.EnsureSeeded();
        SelectStaffDetailTab(0);
        if (!_childEventsWired)
        {
            _childEventsWired = true;
            DocsPanel.IdAttachRequested += (_, _) => AttachStaffIdPdf();
            DocsPanel.IdViewRequested += (_, _) => ViewIdPdf();
            DocsPanel.ProfileAttachRequested += (_, _) => AttachStaffProfileImage();
            RolePicker.RoleChanged += (_, _) =>
            {
                if (_selected != null)
                    _selected.AccessRole = RolePicker.SelectedRole;
                MarkFormDirty();
                ClearStaleBasicDetailFieldErrors();
            };
            SecurityPanel.AddHandler(TextBox.TextChangedEvent,
                new TextChangedEventHandler((_, _) => MarkFormDirty()), handledEventsToo: true);
            SecurityPanel.AddHandler(PasswordBox.PasswordChangedEvent,
                new RoutedEventHandler((_, _) => MarkFormDirty()));
            BioPanel.BiometricStateChanged += (_, _) =>
            {
                MarkFormDirty();
                TryAutoSaveAfterSideEffect();
            };
        }

        if (!_staffStoreMetricsHooked)
        {
            _staffStoreMetricsHooked = true;
            StaffAccessStore.DataChanged += StaffAccessStore_OnMetricsDataChanged;
        }

        RefreshList(selectUserId: StaffAccessStore.GetUsers().FirstOrDefault()?.Id);
    }

    private void StaffAccessStore_OnMetricsDataChanged(object? sender, EventArgs e)
    {
        if (_performanceMetricsVisible)
            Dispatcher.BeginInvoke(new Action(RefreshPerformanceMetrics), DispatcherPriority.Background);
    }

    private void RefreshList(Guid? selectUserId = null)
    {
        if (_idAwaitingFirstSave is { } pid && StaffAccessStore.GetUser(pid) is null)
            _idAwaitingFirstSave = null;

        _rowModels.Clear();
        foreach (var u in StaffAccessStore.GetUsers().OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase))
        {
            if (!UserMatchesSearch(u))
                continue;
            _rowModels.Add(new StaffAccessListItemVm(u));
        }

        UsersList.ItemsSource = null;
        UsersList.ItemsSource = _rowModels;
        Dispatcher.BeginInvoke(new Action(EnsureUsersListScrollPaddingHooked), DispatcherPriority.Loaded);
        RefreshStats();

        StaffAccessListItemVm? pick = null;
        if (selectUserId is { } sid)
            pick = _rowModels.FirstOrDefault(r => r.User.Id == sid);
        pick ??= _rowModels.FirstOrDefault();
        UsersList.SelectedItem = pick;
        if (pick != null)
            LoadUserIntoForm(pick.User);
        else
            ClearForm();
    }

    private bool UserMatchesSearch(StaffUser u)
    {
        if (string.IsNullOrEmpty(_searchQuery))
            return true;
        var q = _searchQuery;
        return u.UserName.Contains(q, StringComparison.OrdinalIgnoreCase)
               || u.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
               || u.NumericId.ToString(CultureInfo.CurrentCulture).Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshStats()
    {
        var all = StaffAccessStore.GetUsers();
        TxtStatTotal.Text = all.Count.ToString(CultureInfo.CurrentCulture);
        TxtStatActive.Text = all.Count(x => x.IsActive).ToString(CultureInfo.CurrentCulture);
        if (_performanceMetricsVisible)
            RefreshPerformanceMetrics();
    }

    private void LoadUserIntoForm(StaffUser u)
    {
        StopStaffFileRemoteSyncTimers();
        _suppressDirty = true;
        try
        {
            _selected = u;
            StaffHeaderTitleArea.Visibility = Visibility.Visible;
            TxtNumericId.Text = u.NumericId.ToString(CultureInfo.CurrentCulture);
            TxtUserName.Text = u.UserName;
            TxtFirstName.Text = u.FirstName;
            TxtMiddleName.Text = u.MiddleName;
            TxtSurname.Text = u.Surname;
            TxtCardNumber.Text = u.CardNumber;
            TxtJobTitle.Text = u.JobTitle;
            TglActiveUser.IsChecked = u.IsActive;
            RolePicker.SelectedRole = u.AccessRole;
            TryHydrateStaffDocumentsFromDisk(u);
            SecurityPanel.LoadFromUser(u);
            BioPanel.LoadFromUser(u);
            DocsPanel.LoadFromUser(u);
            RefreshProfileChrome();
        }
        finally
        {
            _suppressDirty = false;
        }

        _formDirty = false;
        _auditBaselineIsActive = u.IsActive;
        _auditCommittedSnapshot = StaffAccessAuditSnapshot.FromUser(u);
        UpdateAuditNavLockUi();
        ClearAllBasicDetailFieldErrors();
    }

    private void ClearForm()
    {
        StopStaffFileRemoteSyncTimers();
        _suppressDirty = true;
        try
        {
            _selected = null;
            StaffHeaderTitleArea.Visibility = Visibility.Collapsed;
            TxtNumericId.Text = "";
            TxtUserName.Text = "";
            TxtFirstName.Text = "";
            TxtMiddleName.Text = "";
            TxtSurname.Text = "";
            TxtCardNumber.Text = "";
            TxtJobTitle.Text = "";
            TglActiveUser.IsChecked = true;
            RolePicker.SelectedRole = StaffAccessRole.User;
            SecurityPanel.LoadFromUser(null);
            BioPanel.LoadFromUser(null);
            DocsPanel.LoadFromUser(null);
            RefreshProfileChrome();
        }
        finally
        {
            _suppressDirty = false;
        }

        _formDirty = false;
        _auditCommittedSnapshot = null;
        _auditBaselineIsActive = true;
        UpdateAuditNavLockUi();
        ClearAllBasicDetailFieldErrors();
    }

    private bool IsStaffAuditNavLocked() =>
        _formDirty || (_idAwaitingFirstSave is { } pending && _selected?.Id == pending);

    private void MarkFormDirty()
    {
        if (_suppressDirty)
            return;
        _formDirty = true;
        UpdateAuditNavLockUi();
    }

    private void UpdateAuditNavLockUi()
    {
        var locked = IsStaffAuditNavLocked();
        BtnAuditTrail.IsEnabled = _selected != null && !locked;
        BtnAddUser.IsEnabled = !_formDirty || IsAwaitingFirstSaveOnSelection();
        BtnAuditTrail.ToolTip = locked
            ? "Save this user before opening the audit trail or adding another user."
            : "View security and access activity for this account.";
        BtnAddUser.ToolTip = IsAwaitingFirstSaveOnSelection()
            ? "Cancel creating this account and return to the list."
            : !_formDirty
                ? "Create a new staff account."
                : "Save this user before adding another account.";
        SyncAddUserButtonChrome();
    }

    private bool IsAwaitingFirstSaveOnSelection() =>
        _idAwaitingFirstSave is { } pid && _selected?.Id == pid;

    private void SyncAddUserButtonChrome()
    {
        var showCancel = _idAwaitingFirstSave is { } pid && StaffAccessStore.GetUser(pid) != null;
        TxtAddUserButtonLabel.Text = showCancel ? "Cancel" : "Add User";
    }

    private void StaffDetailFields_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        MarkFormDirty();
        ClearStaleBasicDetailFieldErrors();
        if (sender is TextBox tb && (ReferenceEquals(tb, TxtFirstName) || ReferenceEquals(tb, TxtSurname)))
            MaybeSyncDraftUserNameFromNames();
    }

    private static void HideBasicDetailFieldError(TextBlock block)
    {
        block.Visibility = Visibility.Collapsed;
        block.Text = "";
    }

    private static void ShowBasicDetailFieldError(TextBlock block, string message)
    {
        block.Text = message;
        block.Visibility = Visibility.Visible;
    }

    private void ClearAllBasicDetailFieldErrors()
    {
        HideBasicDetailFieldError(LblUserNameError);
        HideBasicDetailFieldError(LblFirstNameError);
        HideBasicDetailFieldError(LblSurnameError);
        HideBasicDetailFieldError(LblRoleError);
        HideBasicDetailFieldError(LblCardNumberError);
    }

    private void ClearStaleBasicDetailFieldErrors()
    {
        if (LblUserNameError.Visibility == Visibility.Visible &&
            !string.IsNullOrWhiteSpace(TxtUserName.Text))
            HideBasicDetailFieldError(LblUserNameError);
        if (LblFirstNameError.Visibility == Visibility.Visible &&
            !string.IsNullOrWhiteSpace(TxtFirstName.Text))
            HideBasicDetailFieldError(LblFirstNameError);
        if (LblSurnameError.Visibility == Visibility.Visible &&
            !string.IsNullOrWhiteSpace(TxtSurname.Text))
            HideBasicDetailFieldError(LblSurnameError);
        if (LblRoleError.Visibility == Visibility.Visible && RolePicker.HasValidRoleSelection)
            HideBasicDetailFieldError(LblRoleError);
        if (LblCardNumberError.Visibility == Visibility.Visible &&
            !string.IsNullOrWhiteSpace(TxtCardNumber.Text))
            HideBasicDetailFieldError(LblCardNumberError);
    }

    /// <summary>Required Basic info fields; inline errors, no modal dialogs.</summary>
    private bool TryValidateRequiredDetailFieldsForSave()
    {
        ClearAllBasicDetailFieldErrors();

        var userName = (TxtUserName.Text ?? "").Trim();
        var first = (TxtFirstName.Text ?? "").Trim();
        var surname = (TxtSurname.Text ?? "").Trim();
        var card = (TxtCardNumber.Text ?? "").Trim();

        if (string.IsNullOrEmpty(first))
        {
            ShowBasicDetailFieldError(LblFirstNameError, "Please enter a first name.");
            FocusDetailTextBox(TxtFirstName);
            return false;
        }

        if (string.IsNullOrEmpty(surname))
        {
            ShowBasicDetailFieldError(LblSurnameError, "Please enter a surname.");
            FocusDetailTextBox(TxtSurname);
            return false;
        }

        if (string.IsNullOrEmpty(userName))
        {
            ShowBasicDetailFieldError(LblUserNameError, "Please enter a user name.");
            FocusDetailTextBox(TxtUserName);
            return false;
        }

        if (!RolePicker.HasValidRoleSelection)
        {
            ShowBasicDetailFieldError(LblRoleError, "Please select a role.");
            RolePicker.FocusRoleCombo();
            return false;
        }

        if (string.IsNullOrEmpty(card))
        {
            ShowBasicDetailFieldError(LblCardNumberError, "Please enter a card number.");
            FocusDetailTextBox(TxtCardNumber);
            return false;
        }

        return true;
    }

    private void FocusDetailTextBox(TextBox tb) =>
        Dispatcher.BeginInvoke(new Action(() => tb.Focus()), DispatcherPriority.Input);

    private void TglActiveUser_OnToggle(object sender, RoutedEventArgs e) => MarkFormDirty();

    private void RefreshProfileChrome()
    {
        if (_selected == null)
            return;

        var has = _selected.ProfileImageBytes is { Length: > 0 };
        ProfilePlaceholder.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        ProfileImageHost.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        if (!has)
        {
            ProfileImage.Source = null;
            ProfileImage.Clip = null;
            return;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new MemoryStream(_selected.ProfileImageBytes!);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            ProfileImage.Source = bmp;
            Dispatcher.BeginInvoke(new Action(ApplyProfileImageRoundClip), DispatcherPriority.Loaded);
        }
        catch
        {
            ProfileImage.Source = null;
            ProfileImage.Clip = null;
            ProfilePlaceholder.Visibility = Visibility.Visible;
            ProfileImageHost.Visibility = Visibility.Collapsed;
        }
    }

    private void ProfileImageHost_OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyProfileImageRoundClip();

    private void ProfileImage_OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyProfileImageRoundClip();

    /// <summary>
    /// Border.CornerRadius does not clip children in WPF; clip the photo to the same rounded-rect radius as form fields
    /// (<see cref="CornerRadiusFromHeightConverter"/> with desired 8), not a full circle.
    /// </summary>
    private void ApplyProfileImageRoundClip()
    {
        if (ProfileImageHost.Visibility != Visibility.Visible)
        {
            ProfileImage.Clip = null;
            return;
        }

        var w = ProfileImage.ActualWidth;
        var h = ProfileImage.ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        var r = AvatarCornerRadius(Math.Min(w, h));
        ProfileImage.Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
    }

    /// <summary>Matches <see cref="CornerRadiusFromHeightConverter"/> (desired 8, min 3, max half minus 1).</summary>
    private static double AvatarCornerRadius(double boxSize)
    {
        const double desired = 8.0;
        if (boxSize <= 0.0)
            return desired;
        var max = Math.Max(0.0, boxSize / 2.0 - 1.0);
        return Math.Max(3.0, Math.Min(desired, max));
    }

    /// <summary>
    /// Matches ProcurementPOrders list gutter: reserve <see cref="SystemParameters.VerticalScrollBarWidth"/> on the
    /// right when the scrollbar is hidden so row width and left/right insets stay symmetric when it appears.
    /// </summary>
    private void EnsureUsersListScrollPaddingHooked()
    {
        DetachUsersListScrollPaddingHook();
        _usersListInnerScrollViewer = FindVisualChild<ScrollViewer>(UsersList);
        if (_usersListInnerScrollViewer == null)
            return;

        _usersListLastScrollBarVisibility = _usersListInnerScrollViewer.ComputedVerticalScrollBarVisibility;
        _usersListInnerScrollViewer.LayoutUpdated += UsersListInnerScrollViewer_LayoutUpdated;
        ApplyUsersListSymmetricPadding();
    }

    private void DetachUsersListScrollPaddingHook()
    {
        if (_usersListInnerScrollViewer != null)
            _usersListInnerScrollViewer.LayoutUpdated -= UsersListInnerScrollViewer_LayoutUpdated;
        _usersListInnerScrollViewer = null;
    }

    private void UsersListInnerScrollViewer_LayoutUpdated(object? sender, EventArgs e)
    {
        if (_usersListInnerScrollViewer == null)
            return;

        var v = _usersListInnerScrollViewer.ComputedVerticalScrollBarVisibility;
        if (v == _usersListLastScrollBarVisibility)
            return;

        _usersListLastScrollBarVisibility = v;
        ApplyUsersListSymmetricPadding();
    }

    private void ApplyUsersListSymmetricPadding()
    {
        if (_usersListInnerScrollViewer == null)
            return;

        var scale = UiScaleRead.ReadScaleOrDefault(
            TryFindResource("UiScaleState") as object);

        const double baseH = 4.0;
        const double baseBottom = 12.0;
        var top = baseH * scale;
        var rightBase = baseH * scale;
        var bottom = baseBottom * scale;

        var sbw = SystemParameters.VerticalScrollBarWidth;
        var scrollbarVisible = _usersListInnerScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible;
        // Left always includes the scrollbar gutter so it matches the *visual* right inset
        // (right padding + scrollbar width when the bar is visible; right padding + reserved gutter when hidden).
        var left = rightBase + sbw;
        var right = scrollbarVisible ? rightBase : rightBase + sbw;

        UsersList.Padding = new Thickness(left, top, right, bottom);
    }

    private void UsersList_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListBox listBox)
            return;

        var innerSv = FindVisualChild<ScrollViewer>(listBox);
        if (innerSv == null)
            return;

        var innerHasScroll = innerSv.ScrollableHeight > 0.001;
        var scrollingUp = e.Delta > 0;
        var scrollingDown = e.Delta < 0;
        var atTop = innerSv.VerticalOffset <= 0;
        var atBottom = innerSv.VerticalOffset >= innerSv.ScrollableHeight - 0.001;

        if (!innerHasScroll || (scrollingUp && atTop) || (scrollingDown && atBottom))
            TryBubbleWheelToAncestorScrollViewer(listBox, innerSv, e);
    }

    private static void TryBubbleWheelToAncestorScrollViewer(System.Windows.Controls.ListBox listBox, ScrollViewer innerSv,
        MouseWheelEventArgs e)
    {
        for (var walk = VisualTreeHelper.GetParent(listBox) as DependencyObject;
             walk != null;
             walk = VisualTreeHelper.GetParent(walk))
        {
            if (walk is ScrollViewer outer && !ReferenceEquals(outer, innerSv))
            {
                if (e.Delta > 0)
                    outer.LineUp();
                else
                    outer.LineDown();
                e.Handled = true;
                return;
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
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

    private void UsersList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressListSelectionHandlers)
            return;

        var picked = UsersList.SelectedItem as StaffAccessListItemVm;
        if (IsStaffAuditNavLocked() && picked != null && _selected != null && picked.User.Id != _selected.Id)
        {
            _suppressListSelectionHandlers = true;
            try
            {
                var back = _rowModels.FirstOrDefault(r => r.User.Id == _selected.Id);
                if (back != null)
                    UsersList.SelectedItem = back;
            }
            finally
            {
                _suppressListSelectionHandlers = false;
            }

            ShowStaffToast("Save this user before switching to another account.", StaffToastKind.Error);
            return;
        }

        if (picked != null)
            LoadUserIntoForm(picked.User);
        else
            ClearForm();
    }

    private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = (SearchTextBox.Text ?? "").Trim();
        var keep = _selected?.Id;
        RefreshList(keep);
    }

    private void SearchBoxBorder_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src && SearchTextBox.IsAncestorOf(src))
            return;

        e.Handled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (SearchTextBox.IsKeyboardFocusWithin)
                return;
            SearchTextBox.Focus();
            Keyboard.Focus(SearchTextBox);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void SearchTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (SearchFocusOuter != null)
            SearchFocusOuter.Visibility = Visibility.Visible;
        if (SearchFocusInner != null)
            SearchFocusInner.Visibility = Visibility.Visible;
        if (SearchBoxBorder != null)
            SearchBoxBorder.BorderBrush = Brushes.Transparent;
    }

    private void SearchTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (SearchFocusOuter != null)
            SearchFocusOuter.Visibility = Visibility.Collapsed;
        if (SearchFocusInner != null)
            SearchFocusInner.Visibility = Visibility.Collapsed;
        if (SearchBoxBorder != null)
            SearchBoxBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#CCC")!;
    }

    private void BtnAddUser_OnClick(object sender, RoutedEventArgs e)
    {
        if (_idAwaitingFirstSave is { } pendingId && StaffAccessStore.GetUser(pendingId) != null)
        {
            CancelPendingNewUser();
            return;
        }

        var u = StaffAccessStore.AddUser(null);
        _idAwaitingFirstSave = u.Id;
        _searchQuery = "";
        SearchTextBox.Text = "";
        RefreshList(u.Id);
        // "User details" header area: always start on Basic info (not Security / Biometric / Documents).
        SelectStaffDetailTab(0);
        UpdateAuditNavLockUi();
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                TxtFirstName.Focus();
                Keyboard.Focus(TxtFirstName);
            }),
            DispatcherPriority.Input);
    }

    private void CancelPendingNewUser()
    {
        if (_idAwaitingFirstSave is not { } pid)
            return;
        if (!StaffAccessStore.TryRemoveUser(pid, out var err))
        {
            ShowStaffToast(err ?? "Could not cancel — this user is still linked in Operations.", StaffToastKind.Error);
            return;
        }

        if (_auditTrailBoundUserId == pid)
            CloseStaffAuditOverlay();

        _idAwaitingFirstSave = null;
        var firstId = StaffAccessStore.GetUsers().FirstOrDefault()?.Id;
        RefreshList(firstId);
        UpdateAuditNavLockUi();
    }

    /// <summary>While creating a user, derive sign-in name from first + surname (Basic info).</summary>
    private void MaybeSyncDraftUserNameFromNames()
    {
        if (!IsAwaitingFirstSaveOnSelection())
            return;
        var first = (TxtFirstName.Text ?? "").Trim();
        var surname = (TxtSurname.Text ?? "").Trim();
        _suppressDirty = true;
        try
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(surname))
                TxtUserName.Text = "";
            else
                TxtUserName.Text = $"{first}.{surname}".ToLowerInvariant();
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    private void BtnDeleteUser_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid id)
            return;

        var victim = StaffAccessStore.GetUser(id);
        var overlayVisible = StaffAuditOverlay.Visibility == Visibility.Visible;

        if (!StaffAccessStore.TryRemoveUser(id, out var err))
        {
            StaffAccessAuditRepository.AppendUserDeleteDenied(id, err ?? "Could not remove user.");
            ShowStaffToast(err ?? "Could not remove user.", StaffToastKind.Error);
            if (overlayVisible && _auditTrailBoundUserId == id && _selected?.Id == id)
                StaffAuditTrailPanel.Bind(_selected);
            return;
        }

        if (victim != null)
            StaffAccessAuditRepository.AppendUserDeleteSucceeded(id, victim);

        if (overlayVisible && _auditTrailBoundUserId == id)
            CloseStaffAuditOverlay();

        RefreshList(StaffAccessStore.GetUsers().FirstOrDefault()?.Id);
        ShowStaffToast("User removed.", StaffToastKind.Success);
    }

    private static string TrimField(string? raw) => (raw ?? "").Trim();

    /// <summary>Trim + first letter uppercase, remaining letters lowercase (per-field sentence case).</summary>
    private static string TrimAndCapitalizeFirstRest(string? raw)
    {
        var t = TrimField(raw);
        if (t.Length == 0)
            return "";
        if (t.Length == 1)
            return char.ToUpperInvariant(t[0]).ToString();
        return char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant();
    }

    private void BtnSave_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
        {
            MessageBox.Show(Window.GetWindow(this), "Select a user first.", "Save changes", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!TryValidateRequiredDetailFieldsForSave())
        {
            RadStaffTabBasic.IsChecked = true;
            return;
        }

        if (!SecurityPanel.TryValidatePasswordsForSave())
        {
            RadStaffTabSecurity.IsChecked = true;
            return;
        }

        CommitStaffUserChangesToStore();
    }

    /// <summary>Runs the same persist path as Save after ID/profile attach or biometric enrollment (when validation passes).</summary>
    private void TryAutoSaveAfterSideEffect()
    {
        if (_selected == null)
            return;
        if (!TryValidateRequiredDetailFieldsForSave())
        {
            RadStaffTabBasic.IsChecked = true;
            return;
        }

        if (!SecurityPanel.TryValidatePasswordsForSave())
        {
            RadStaffTabSecurity.IsChecked = true;
            return;
        }

        CommitStaffUserChangesToStore();
    }

    private void CommitStaffUserChangesToStore()
    {
        if (_selected == null)
            return;

        var rawBeforeAudit = _auditCommittedSnapshot ?? StaffAccessAuditSnapshot.FromUser(_selected);
        var beforeAudit = rawBeforeAudit with { IsActive = _auditBaselineIsActive };
        var passwordChanged = SecurityPanel.HasPendingPasswordChange();

        _selected.UserName = TrimField(TxtUserName.Text).ToLowerInvariant();
        _selected.FirstName = TrimAndCapitalizeFirstRest(TxtFirstName.Text);
        _selected.MiddleName = TrimAndCapitalizeFirstRest(TxtMiddleName.Text);
        _selected.Surname = TrimAndCapitalizeFirstRest(TxtSurname.Text);
        _selected.CardNumber = TrimField(TxtCardNumber.Text);
        _selected.JobTitle = TrimField(TxtJobTitle.Text);
        _selected.IsActive = TglActiveUser.IsChecked == true;
        _selected.AccessRole = RolePicker.SelectedRole;
        SecurityPanel.ApplyToUser(_selected);
        var afterAudit = StaffAccessAuditSnapshot.FromUser(_selected);
        StaffAccessAuditRepository.AppendProfileSaveIfChanged(_selected.Id, beforeAudit, afterAudit, passwordChanged);
        _auditBaselineIsActive = _selected.IsActive;
        StaffAccessStore.Notify();
        if (_idAwaitingFirstSave == _selected.Id)
            _idAwaitingFirstSave = null;
        var id = _selected.Id;
        RefreshList(id);
        if (StaffAuditOverlay.Visibility == Visibility.Visible)
            StaffAuditTrailPanel.Bind(_selected);
        ShowStaffToast("Changes saved.");
    }

    private void BtnAuditTrail_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null || IsStaffAuditNavLocked())
            return;
        OpenStaffAuditOverlay();
    }

    private void OpenStaffAuditOverlay()
    {
        if (_selected == null)
            return;
        _auditTrailBoundUserId = _selected.Id;
        StaffAuditTrailPanel.Bind(_selected);
        StaffAuditOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            StaffAuditOverlay.Focus();
            Keyboard.Focus(StaffAuditOverlay);
        }), DispatcherPriority.Input);
    }

    private void StaffAuditOverlay_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && StaffAuditOverlay.Visibility == Visibility.Visible)
        {
            CloseStaffAuditOverlay();
            e.Handled = true;
        }
    }

    private void CloseStaffAuditOverlay()
    {
        StaffAuditOverlay.Visibility = Visibility.Collapsed;
        _auditTrailBoundUserId = null;
    }

    private void StaffAuditTrailPanel_OnCloseRequested(object? sender, EventArgs e) => CloseStaffAuditOverlay();

    private void StaffAuditScrim_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CloseStaffAuditOverlay();
        e.Handled = true;
    }

    private void StaffHeaderAvatarHost_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_selected == null)
            return;
        RadStaffTabDocs.IsChecked = true;
        e.Handled = true;
    }

    private static string StaffDocsDirectory() => Path.Combine(StaffDocumentsRepositoryRoot, "docs");

    private static string StaffImagesDirectory() => Path.Combine(StaffDocumentsRepositoryRoot, "images");

    private static void EnsureStaffRepositoryLayout()
    {
        Directory.CreateDirectory(StaffDocumentsRepositoryRoot);
        Directory.CreateDirectory(StaffDocsDirectory());
        Directory.CreateDirectory(StaffImagesDirectory());
    }

    private static string GetCanonicalIdPdfFullPath(int numericId) =>
        Path.Combine(StaffDocsDirectory(), $"{numericId.ToString(CultureInfo.InvariantCulture)}_id.pdf");

    private static string? FindProfileImageFullPath(int numericId)
    {
        var dir = StaffImagesDirectory();
        if (!Directory.Exists(dir))
            return null;
        var prefix = $"{numericId.ToString(CultureInfo.InvariantCulture)}_profile.";
        foreach (var f in Directory.GetFiles(dir))
        {
            if (Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return f;
        }

        return null;
    }

    private static void DeleteExistingProfileImagesForUser(int numericId)
    {
        var dir = StaffImagesDirectory();
        if (!Directory.Exists(dir))
            return;
        var prefix = $"{numericId.ToString(CultureInfo.InvariantCulture)}_profile.";
        foreach (var f in Directory.GetFiles(dir))
        {
            if (!Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                File.Delete(f);
            }
            catch
            {
                // best effort
            }
        }
    }

    private static void TryHydrateStaffDocumentsFromDisk(StaffUser u)
    {
        try
        {
            EnsureStaffRepositoryLayout();
            var idFull = GetCanonicalIdPdfFullPath(u.NumericId);
            if (File.Exists(idFull))
            {
                u.IdDocumentPdfBytes = File.ReadAllBytes(idFull);
                u.IdDocumentFileName = Path.GetRelativePath(StaffDocumentsRepositoryRoot, idFull);
                u.IdDocumentRemoteSyncStatus = StaffFileRemoteSyncStatus.Synced;
            }

            var profFull = FindProfileImageFullPath(u.NumericId);
            if (profFull != null && File.Exists(profFull))
            {
                u.ProfileImageBytes = File.ReadAllBytes(profFull);
                u.ProfileImageRepositoryRelativePath = Path.GetRelativePath(StaffDocumentsRepositoryRoot, profFull);
                u.ProfileImageRemoteSyncStatus = StaffFileRemoteSyncStatus.Synced;
            }
        }
        catch
        {
            // keep in-memory model
        }
    }

    private void StopIdDocRemoteSyncTimer()
    {
        if (_idDocRemoteSyncTimer == null)
            return;
        _idDocRemoteSyncTimer.Stop();
        _idDocRemoteSyncTimer.Tick -= IdDocRemoteSyncTimer_OnTick;
        _idDocRemoteSyncTimer = null;
    }

    private void StopProfileRemoteSyncTimer()
    {
        if (_profileRemoteSyncTimer == null)
            return;
        _profileRemoteSyncTimer.Stop();
        _profileRemoteSyncTimer.Tick -= ProfileRemoteSyncTimer_OnTick;
        _profileRemoteSyncTimer = null;
    }

    private void StopStaffFileRemoteSyncTimers()
    {
        StopIdDocRemoteSyncTimer();
        StopProfileRemoteSyncTimer();
    }

    private void ArmIdDocumentRemoteSyncSimulation()
    {
        if (_selected == null)
            return;
        _selected.IdDocumentRemoteSyncStatus = StaffFileRemoteSyncStatus.PendingSync;
        DocsPanel.LoadFromUser(_selected);
        StopIdDocRemoteSyncTimer();
        _idDocRemoteSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.3) };
        _idDocRemoteSyncTimer.Tick += IdDocRemoteSyncTimer_OnTick;
        _idDocRemoteSyncTimer.Start();
    }

    private void IdDocRemoteSyncTimer_OnTick(object? sender, EventArgs e)
    {
        if (_idDocRemoteSyncTimer != null)
        {
            _idDocRemoteSyncTimer.Stop();
            _idDocRemoteSyncTimer.Tick -= IdDocRemoteSyncTimer_OnTick;
            _idDocRemoteSyncTimer = null;
        }

        if (_selected == null)
            return;
        _selected.IdDocumentRemoteSyncStatus = StaffFileRemoteSyncStatus.Synced;
        DocsPanel.LoadFromUser(_selected);
    }

    private void ArmProfileImageRemoteSyncSimulation()
    {
        if (_selected == null)
            return;
        _selected.ProfileImageRemoteSyncStatus = StaffFileRemoteSyncStatus.PendingSync;
        DocsPanel.LoadFromUser(_selected);
        StopProfileRemoteSyncTimer();
        _profileRemoteSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.3) };
        _profileRemoteSyncTimer.Tick += ProfileRemoteSyncTimer_OnTick;
        _profileRemoteSyncTimer.Start();
    }

    private void ProfileRemoteSyncTimer_OnTick(object? sender, EventArgs e)
    {
        if (_profileRemoteSyncTimer != null)
        {
            _profileRemoteSyncTimer.Stop();
            _profileRemoteSyncTimer.Tick -= ProfileRemoteSyncTimer_OnTick;
            _profileRemoteSyncTimer = null;
        }

        if (_selected == null)
            return;
        _selected.ProfileImageRemoteSyncStatus = StaffFileRemoteSyncStatus.Synced;
        DocsPanel.LoadFromUser(_selected);
    }

    private void AttachStaffIdPdf()
    {
        if (_selected == null)
            return;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "PDF (*.pdf)|*.pdf" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true)
            return;
        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            if (bytes.Length < 5 || bytes[0] != (byte)'%' || bytes[1] != (byte)'P' || bytes[2] != (byte)'D' ||
                bytes[3] != (byte)'F')
            {
                MessageBox.Show(Window.GetWindow(this), "The selected file does not look like a PDF.", "ID document",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EnsureStaffRepositoryLayout();
            var dest = GetCanonicalIdPdfFullPath(_selected.NumericId);
            File.WriteAllBytes(dest, bytes);
            _selected.IdDocumentPdfBytes = bytes;
            _selected.IdDocumentFileName = Path.GetRelativePath(StaffDocumentsRepositoryRoot, dest);
            DocsPanel.LoadFromUser(_selected);
            MarkFormDirty();
            ArmIdDocumentRemoteSyncSimulation();
            TryAutoSaveAfterSideEffect();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "ID document", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AttachStaffProfileImage()
    {
        if (_selected == null)
            return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true)
            return;
        try
        {
            var ext = Path.GetExtension(dlg.FileName);
            if (string.IsNullOrEmpty(ext) || ext.Length > 10)
            {
                MessageBox.Show(Window.GetWindow(this), "Please choose a file with a valid image extension.",
                    "Profile image", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ext = ext.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".bmp", ".gif"
            };
            if (!allowed.Contains(ext))
            {
                MessageBox.Show(Window.GetWindow(this), "Only PNG, JPG, JPEG, BMP, or GIF images are allowed.",
                    "Profile image", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var probeBytes = File.ReadAllBytes(dlg.FileName);
            using var ms = new MemoryStream(probeBytes);
            var probe = new BitmapImage();
            probe.BeginInit();
            probe.StreamSource = ms;
            probe.CacheOption = BitmapCacheOption.OnLoad;
            probe.EndInit();
            probe.Freeze();
            _ = probe.PixelWidth;

            EnsureStaffRepositoryLayout();
            DeleteExistingProfileImagesForUser(_selected.NumericId);
            var destName = $"{_selected.NumericId.ToString(CultureInfo.InvariantCulture)}_profile{ext}";
            var dest = Path.Combine(StaffImagesDirectory(), destName);
            File.Copy(dlg.FileName, dest, overwrite: true);
            _selected.ProfileImageBytes = File.ReadAllBytes(dest);
            _selected.ProfileImageRepositoryRelativePath = Path.GetRelativePath(StaffDocumentsRepositoryRoot, dest);
            RefreshProfileChrome();
            DocsPanel.LoadFromUser(_selected);
            MarkFormDirty();
            ArmProfileImageRemoteSyncSimulation();
            TryAutoSaveAfterSideEffect();
        }
        catch
        {
            MessageBox.Show(Window.GetWindow(this), "That file is not a valid image.", "Profile image",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ViewIdPdf()
    {
        if (_selected == null)
            return;
        try
        {
            var disk = GetCanonicalIdPdfFullPath(_selected.NumericId);
            if (File.Exists(disk))
            {
                Process.Start(new ProcessStartInfo(disk) { UseShellExecute = true });
                return;
            }

            if (_selected.IdDocumentPdfBytes is not { Length: > 0 } data)
                return;
            var path = Path.Combine(Path.GetTempPath(), $"staff-id-{_selected.Id:N}.pdf");
            File.WriteAllBytes(path, data);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "View document", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static SolidColorBrush PerfHexBrush(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        b.Freeze();
        return b;
    }

    private void BtnTogglePerformanceMetrics_OnClick(object sender, RoutedEventArgs e)
    {
        _performanceMetricsVisible = !_performanceMetricsVisible;
        PerfMetricsScrollViewer.Visibility = _performanceMetricsVisible ? Visibility.Visible : Visibility.Collapsed;
        TxtTogglePerformanceMetrics.Text = _performanceMetricsVisible
            ? "Hide Performance Metrics"
            : "Show Performance Metrics";
        if (_performanceMetricsVisible)
            Dispatcher.BeginInvoke(new Action(RefreshPerformanceMetrics), DispatcherPriority.Loaded);
    }

    private void PerfWeeklyChartCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_performanceMetricsVisible && e.NewSize.Width > 1 && e.NewSize.Height > 1)
            RedrawWeeklyActivityChart();
    }

    private void RefreshPerformanceMetrics()
    {
        StaffAccessStore.EnsureSeeded();
        var users = StaffAccessStore.GetUsers();
        var total = users.Count;
        var active = users.Count(u => u.IsActive);
        var bio = users.Count(u => u.BiometricEnrolled);
        TxtPerfKpiTotal.Text = total.ToString(CultureInfo.CurrentCulture);
        TxtPerfKpiActive.Text = active.ToString(CultureInfo.CurrentCulture);
        var activePct = total == 0 ? 0 : (int)Math.Round(100.0 * active / total);
        TxtPerfKpiActiveSub.Text = $"{activePct.ToString(CultureInfo.CurrentCulture)}% active";
        TxtPerfKpiBio.Text = bio.ToString(CultureInfo.CurrentCulture);
        var bioPct = total == 0 ? 0 : (int)Math.Round(100.0 * bio / total);
        TxtPerfKpiBioSub.Text = $"{bioPct.ToString(CultureInfo.CurrentCulture)}% secured";

        UpdateAvgLoginTimeKpi(users);

        TxtPerfStatusActiveCount.Text = active.ToString(CultureInfo.CurrentCulture);
        TxtPerfStatusInactiveCount.Text = (total - active).ToString(CultureInfo.CurrentCulture);
        TxtPerfSecurityBioCount.Text = bio.ToString(CultureInfo.CurrentCulture);
        TxtPerfSecurityPwdCount.Text = (total - bio).ToString(CultureInfo.CurrentCulture);

        var inactive = total - active;
        var pwdOnly = total - bio;
        SetPerfBarFraction(PerfStatusActiveFillHost, PerfStatusActiveFill, total == 0 ? 0 : active / (double)total);
        SetPerfBarFraction(PerfStatusInactiveFillHost, PerfStatusInactiveFill, total == 0 ? 0 : inactive / (double)total);
        SetPerfBarFraction(PerfSecurityBioFillHost, PerfSecurityBioFill, total == 0 ? 0 : bio / (double)total);
        SetPerfBarFraction(PerfSecurityPwdFillHost, PerfSecurityPwdFill, total == 0 ? 0 : pwdOnly / (double)total);

        DrawRoleDonutAndLegend(users);
        DrawBinaryDonut(PerfStatusDonutCanvas, active, inactive, "#14B8A6", "#CBD5E1", "Active", "Inactive");
        DrawBinaryDonut(PerfSecurityDonutCanvas, bio, pwdOnly, "#5B5FED", "#CBD5E1", "Biometric", "Password Only");

        Dispatcher.BeginInvoke(new Action(RedrawWeeklyActivityChart), DispatcherPriority.Loaded);
    }

    private void UpdateAvgLoginTimeKpi(IReadOnlyList<StaffUser> users)
    {
        // Aggregate all "Successful login" audit entries across every user and
        // compute the average time-of-day (local). This keeps the KPI tied to
        // real data — if no logins have been recorded yet we show a placeholder.
        long totalMinutes = 0;
        int count = 0;
        foreach (var u in users)
        {
            var entries = StaffAccessAuditRepository.GetBySubjectUserNewestFirst(u.Id);
            foreach (var e in entries)
            {
                if (!string.Equals(e.Title, "Successful login", StringComparison.Ordinal))
                    continue;
                var local = e.OccurredAtUtc.Kind == DateTimeKind.Utc
                    ? e.OccurredAtUtc.ToLocalTime()
                    : DateTime.SpecifyKind(e.OccurredAtUtc, DateTimeKind.Utc).ToLocalTime();
                totalMinutes += local.Hour * 60 + local.Minute;
                count++;
            }
        }

        if (count == 0)
        {
            TxtPerfKpiAvgLogin.Text = "—";
            TxtPerfKpiAvgLoginSub.Text = "no logins yet";
            return;
        }

        var avg = (int)Math.Round(totalMinutes / (double)count);
        var h24 = Math.Max(0, Math.Min(23, avg / 60));
        var m = Math.Max(0, Math.Min(59, avg % 60));
        var meridiem = h24 < 12 ? "AM" : "PM";
        var h12 = h24 % 12;
        if (h12 == 0) h12 = 12;

        TxtPerfKpiAvgLogin.Text = string.Format(CultureInfo.CurrentCulture, "{0}:{1:00}", h12, m);
        TxtPerfKpiAvgLoginSub.Text = count == 1
            ? $"{meridiem} · {count} login"
            : $"{meridiem} · {count} logins";
    }

    private static void SetPerfBarFraction(Border trackHost, Border fill, double fraction)
    {
        void Apply()
        {
            trackHost.UpdateLayout();
            var w = Math.Max(0, trackHost.ActualWidth * Math.Min(1, Math.Max(0, fraction)));
            fill.Width = w;
        }

        Apply();
        if (trackHost.ActualWidth < 0.5)
            trackHost.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.Loaded);
    }

    private static readonly (StaffAccessRole Role, string ColorHex)[] PerfRoleOrder =
    {
        (StaffAccessRole.Admin, "#9333EA"),
        (StaffAccessRole.Manager, "#3B82F6"),
        (StaffAccessRole.Supervisor, "#F97316"),
        (StaffAccessRole.User, "#64748B"),
        (StaffAccessRole.System, "#EC4899")
    };

    private void DrawRoleDonutAndLegend(IReadOnlyList<StaffUser> users)
    {
        PerfRoleDonutCanvas.Children.Clear();
        var counts = PerfRoleOrder.Select(r => users.Count(u => u.AccessRole == r.Role)).ToArray();
        var sum = counts.Sum();
        var center = new Point(PerfRoleDonutCanvas.Width / 2, PerfRoleDonutCanvas.Height / 2);
        var rOut = Math.Min(PerfRoleDonutCanvas.Width, PerfRoleDonutCanvas.Height) / 2 - 4;
        var rIn = rOut * 0.55;
        if (sum == 0)
        {
            PerfRoleDonutCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = PerfRingSlice(center, rOut, rIn, 0, 359.99),
                Fill = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                Stroke = Brushes.White,
                StrokeThickness = 1
            });
        }
        else
        {
            var start = 0.0;
            for (var i = 0; i < counts.Length; i++)
            {
                var c = counts[i];
                if (c <= 0)
                    continue;
                var sweep = 360.0 * c / sum;
                var slice = new System.Windows.Shapes.Path
                {
                    Data = PerfRingSlice(center, rOut, rIn, start, sweep),
                    Fill = PerfHexBrush(PerfRoleOrder[i].ColorHex),
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                slice.ToolTip = BuildChartToolTip(
                    PerfRoleOrder[i].Role.ToString(),
                    PerfRoleOrder[i].ColorHex,
                    c.ToString(CultureInfo.CurrentCulture));
                ApplyChartToolTipPlacement(slice, PlacementMode.Mouse);
                PerfRoleDonutCanvas.Children.Add(slice);
                start += sweep;
            }
        }

        PerfRoleLegendPanel.Children.Clear();
        var fg = TryFindResource("MainForeground") as Brush ?? Brushes.Black;
        for (var i = 0; i < PerfRoleOrder.Length; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = PerfHexBrush(PerfRoleOrder[i].ColorHex),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(dot, 0);
            var label = new TextBlock
            {
                Text = PerfRoleOrder[i].Role.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = fg,
                FontSize = 13
            };
            Grid.SetColumn(label, 1);
            var val = new TextBlock
            {
                Text = counts[i].ToString(CultureInfo.CurrentCulture),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Foreground = fg,
                FontSize = 13
            };
            Grid.SetColumn(val, 2);
            row.Children.Add(dot);
            row.Children.Add(label);
            row.Children.Add(val);
            PerfRoleLegendPanel.Children.Add(row);
        }
    }

    private void DrawBinaryDonut(Canvas canvas, int a, int b, string colorA, string colorB, string labelA, string labelB)
    {
        canvas.Children.Clear();
        var center = new Point(canvas.Width / 2, canvas.Height / 2);
        var rOut = Math.Min(canvas.Width, canvas.Height) / 2 - 2;
        var rIn = rOut * 0.55;
        var sum = a + b;
        if (sum == 0)
        {
            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = PerfRingSlice(center, rOut, rIn, 0, 359.99),
                Fill = new SolidColorBrush(Color.FromRgb(229, 231, 235))
            });
            return;
        }

        void AddFull(string color, string label, int count)
        {
            var p = new System.Windows.Shapes.Path
            {
                Data = PerfRingSlice(center, rOut, rIn, 0, 359.99),
                Fill = PerfHexBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            p.ToolTip = BuildChartToolTip(label, color, count.ToString(CultureInfo.CurrentCulture));
            ApplyChartToolTipPlacement(p, PlacementMode.Mouse);
            canvas.Children.Add(p);
        }

        if (a <= 0)
        {
            AddFull(colorB, labelB, b);
            return;
        }

        if (b <= 0)
        {
            AddFull(colorA, labelA, a);
            return;
        }

        var sweepA = 360.0 * a / sum;
        var pathA = new System.Windows.Shapes.Path
        {
            Data = PerfRingSlice(center, rOut, rIn, 0, sweepA),
            Fill = PerfHexBrush(colorA),
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        pathA.ToolTip = BuildChartToolTip(labelA, colorA, a.ToString(CultureInfo.CurrentCulture));
        ApplyChartToolTipPlacement(pathA, PlacementMode.Mouse);
        canvas.Children.Add(pathA);

        var pathB = new System.Windows.Shapes.Path
        {
            Data = PerfRingSlice(center, rOut, rIn, sweepA, 360 - sweepA),
            Fill = PerfHexBrush(colorB),
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        pathB.ToolTip = BuildChartToolTip(labelB, colorB, b.ToString(CultureInfo.CurrentCulture));
        ApplyChartToolTipPlacement(pathB, PlacementMode.Mouse);
        canvas.Children.Add(pathB);
    }

    /// <summary>
    /// Styled tooltip used by every chart slice / per-day hit area. Matches the client mock:
    /// rounded white card, coloured heading, value line below.
    /// </summary>
    private System.Windows.Controls.ToolTip BuildChartToolTip(string heading, string headingColorHex, string valueLine)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = heading,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = PerfHexBrush(headingColorHex)
        });
        stack.Children.Add(new TextBlock
        {
            Text = valueLine,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = TryFindResource("MainForeground") as Brush ?? Brushes.Black
        });

        return new System.Windows.Controls.ToolTip
        {
            Content = stack,
            Style = TryFindResource("StaffAccessChartToolTipStyle") as Style
        };
    }

    /// <summary>
    /// Attaches a tooltip with predictable placement. Donut slices use mouse-following placement (standard for
    /// polar segments); line-chart day bands use mouse placement so the card sits near the pointer rather than
    /// anchoring to the top of a full-height hit box (which looked "floating" above the chart).
    /// </summary>
    private static void ApplyChartToolTipPlacement(FrameworkElement owner, PlacementMode mode,
        double horizontalOffset = 0, double verticalOffset = -10)
    {
        // Style sets ToolTip.Placement="Top"; sync the instance so mouse-relative charts and donut
        // slices do not stay top-anchored (which hid donut tips and floated weekly tips above the plot).
        if (owner.ToolTip is System.Windows.Controls.ToolTip t)
        {
            t.Placement = mode;
            t.HorizontalOffset = horizontalOffset;
            t.VerticalOffset = verticalOffset;
        }

        ToolTipService.SetPlacement(owner, mode);
        ToolTipService.SetHorizontalOffset(owner, horizontalOffset);
        ToolTipService.SetVerticalOffset(owner, verticalOffset);
        ToolTipService.SetInitialShowDelay(owner, 100);
        ToolTipService.SetBetweenShowDelay(owner, 80);
        ToolTipService.SetShowDuration(owner, 60_000);
    }

    /// <summary>Builds a tooltip for a single day in the weekly line chart with both series values.</summary>
    private System.Windows.Controls.ToolTip BuildWeeklyDayToolTip(string day, double logins, double active)
    {
        var stack = new StackPanel { MinWidth = 120 };
        stack.Children.Add(new TextBlock
        {
            Text = day,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = TryFindResource("MainForeground") as Brush ?? Brushes.Black,
            Margin = new Thickness(0, 0, 0, 4)
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"Total Logins : {logins.ToString("0", CultureInfo.CurrentCulture)}",
            FontSize = 12,
            Foreground = PerfHexBrush("#5B5FED")
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"Active Users : {active.ToString("0", CultureInfo.CurrentCulture)}",
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = PerfHexBrush("#2DD4BF")
        });

        return new System.Windows.Controls.ToolTip
        {
            Content = stack,
            Style = TryFindResource("StaffAccessChartToolTipStyle") as Style
        };
    }

    private static Point PerfPolarDeg(Point c, double r, double deg)
    {
        var rad = (deg - 90) * Math.PI / 180.0;
        return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
    }

    private static Geometry PerfRingSlice(Point c, double rOut, double rIn, double startDeg, double sweepDeg)
    {
        if (sweepDeg <= 0.01)
            return Geometry.Empty;
        var large = sweepDeg > 180;
        var fig = new PathFigure
        {
            IsClosed = true,
            StartPoint = PerfPolarDeg(c, rOut, startDeg)
        };
        fig.Segments.Add(new ArcSegment
        {
            Point = PerfPolarDeg(c, rOut, startDeg + sweepDeg),
            Size = new Size(rOut, rOut),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = large
        });
        fig.Segments.Add(new LineSegment(PerfPolarDeg(c, rIn, startDeg + sweepDeg), true));
        fig.Segments.Add(new ArcSegment
        {
            Point = PerfPolarDeg(c, rIn, startDeg),
            Size = new Size(rIn, rIn),
            SweepDirection = SweepDirection.Counterclockwise,
            IsLargeArc = large
        });
        return new PathGeometry(new[] { fig });
    }

    private static readonly double[] WeeklyDemoLogins = { 32, 38, 42, 48, 65, 52, 28 };
    private static readonly double[] WeeklyDemoActive = { 28, 32, 36, 40, 50, 44, 22 };

    private void RedrawWeeklyActivityChart()
    {
        var canvas = PerfWeeklyChartCanvas;
        canvas.Children.Clear();
        var w = canvas.ActualWidth;
        var h = canvas.ActualHeight;
        if (w < 8 || h < 8)
            return;

        // Plot margins. No space reserved at the bottom for a legend — the legend
        // lives outside this canvas now so it can't overlap the day labels.
        const double left = 36;
        const double top = 12;
        const double right = 16;
        const double bottomAxis = 22; // room for Mon..Sun labels only
        var plotW = w - left - right;
        var plotH = h - top - bottomAxis;
        if (plotW < 8 || plotH < 8)
            return;
        const double yMax = 80;

        void Add(System.Windows.Shapes.Shape s)
        {
            canvas.Children.Add(s);
        }

        var gridBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));
        for (var g = 0; g <= 4; g++)
        {
            var yv = g * 20.0;
            var py = top + plotH - plotH * (yv / yMax);
            Add(new System.Windows.Shapes.Line
            {
                X1 = left,
                Y1 = py,
                X2 = left + plotW,
                Y2 = py,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 }
            });
            var yLabel = new TextBlock
            {
                Text = yv.ToString(CultureInfo.InvariantCulture),
                Foreground = TryFindResource("DimmedForeground") as Brush ?? Brushes.Gray,
                FontSize = 11
            };
            yLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(yLabel, 4);
            Canvas.SetTop(yLabel, py - yLabel.DesiredSize.Height / 2);
            canvas.Children.Add(yLabel);
        }

        var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var n = WeeklyDemoLogins.Length;
        for (var i = 0; i < n; i++)
        {
            var px = left + plotW * (i + 0.5) / n;
            var dayTb = new TextBlock
            {
                Text = days[i],
                Foreground = TryFindResource("DimmedForeground") as Brush ?? Brushes.Gray,
                FontSize = 11
            };
            dayTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(dayTb, px - dayTb.DesiredSize.Width / 2);
            Canvas.SetTop(dayTb, top + plotH + 6);
            canvas.Children.Add(dayTb);
        }

        Point[] ToPoly(double[] series)
        {
            var pts = new Point[n];
            for (var i = 0; i < n; i++)
            {
                var px = left + plotW * (i + 0.5) / n;
                var py = top + plotH - plotH * (Math.Min(yMax, series[i]) / yMax);
                pts[i] = new Point(px, py);
            }

            return pts;
        }

        var loginPts = ToPoly(WeeklyDemoLogins);
        var activePts = ToPoly(WeeklyDemoActive);

        void AddLineSeries(Point[] pts, string colorHex, double thickness)
        {
            var stroke = PerfHexBrush(colorHex);
            for (var i = 0; i < pts.Length - 1; i++)
            {
                Add(new System.Windows.Shapes.Line
                {
                    X1 = pts[i].X,
                    Y1 = pts[i].Y,
                    X2 = pts[i + 1].X,
                    Y2 = pts[i + 1].Y,
                    Stroke = stroke,
                    StrokeThickness = thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                });
            }

            foreach (var p in pts)
            {
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.White,
                    Stroke = stroke,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(dot, p.X - 4);
                Canvas.SetTop(dot, p.Y - 4);
                canvas.Children.Add(dot);
            }
        }

        AddLineSeries(loginPts, "#5B5FED", 2.5);
        AddLineSeries(activePts, "#2DD4BF", 2.5);

        // Per-day transparent hit areas. Use mouse-relative placement so the tooltip sits near the pointer
        // (and thus near the markers) instead of anchoring to Placement=Top on a full-height box, which
        // pinned the card to the top of the plot.
        var colW = plotW / n;
        for (var i = 0; i < n; i++)
        {
            var hit = new System.Windows.Shapes.Rectangle
            {
                Width = colW,
                Height = plotH,
                Fill = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            hit.ToolTip = BuildWeeklyDayToolTip(days[i], WeeklyDemoLogins[i], WeeklyDemoActive[i]);
            ApplyChartToolTipPlacement(hit, PlacementMode.Mouse, 0, -12);
            Canvas.SetLeft(hit, left + i * colW);
            Canvas.SetTop(hit, top);
            canvas.Children.Add(hit);
        }
    }
}


/// <summary>UI tint for audit rows (maps to row/icon colors in the trail).</summary>
public enum StaffAccessAuditEventTone
{
    Blue,
    Teal,
    Grey,
    Purple,
    Green
}

/// <summary>Origin of a row: packaged demo vs. real saves in this session.</summary>
public enum StaffAccessAuditSource
{
    Demo,
    Recorded
}

/// <summary>
/// Same icon set as the Staff and Access user detail tabs (<see cref="StaffAccessUserDetails"/> strip).
/// Profile image changes use <see cref="BasicInfo"/> (profile lives on that tab).
/// </summary>
public enum StaffAccessAuditTabGlyph
{
    BasicInfo,
    Security,
    Biometric,
    Documents
}

/// <summary>
/// Persisted audit fact for one staff user. Replace <see cref="StaffAccessAuditRepository"/> backing store
/// with a database table when wiring real persistence (same shape: subject user, UTC time, title, body).
/// </summary>
public sealed class StaffAccessAuditEntry
{
    public Guid Id { get; init; }
    /// <summary>Staff user this event applies to.</summary>
    public Guid SubjectUserId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public StaffAccessAuditEventTone Tone { get; init; }
    public string Device { get; init; } = "Windows Desktop";
    public string Ip { get; init; } = "192.168.1.45";
    public StaffAccessAuditSource Source { get; init; }
    /// <summary>Tab strip glyphs to show for this row (empty → default activity icon in UI).</summary>
    public IReadOnlyList<StaffAccessAuditTabGlyph> TabGlyphs { get; init; } = [];
    /// <summary>When true, UI shows a denied/blocked badge instead of success (e.g. delete blocked by Operations linkage).</summary>
    public bool IsDeniedOutcome { get; init; }
    /// <summary>True when this recorded profile-save row included a password update (counts toward Security Events headline).</summary>
    public bool IncludesPasswordChange { get; init; }
    /// <summary>True when this recorded profile-save row included an access role change (counts toward Security Events headline).</summary>
    public bool IncludesAccessRoleChange { get; init; }
}

public readonly record struct StaffAccessAuditSnapshot(
    bool IsActive,
    string UserName,
    string FirstName,
    string MiddleName,
    string Surname,
    string CardNumber,
    string JobTitle,
    StaffAccessRole AccessRole,
    bool BiometricEnrolled,
    int ProfileImageLength,
    uint ProfileImageHash,
    int IdDocumentLength,
    uint IdDocumentHash,
    string? IdDocumentFileName)
{
    public static StaffAccessAuditSnapshot FromUser(StaffUser u)
    {
        var (pLen, pHash) = StaffAccessAuditBinaryDigest.Of(u.ProfileImageBytes);
        var (dLen, dHash) = StaffAccessAuditBinaryDigest.Of(u.IdDocumentPdfBytes);
        return new StaffAccessAuditSnapshot(
            u.IsActive,
            (u.UserName ?? "").Trim(),
            (u.FirstName ?? "").Trim(),
            (u.MiddleName ?? "").Trim(),
            (u.Surname ?? "").Trim(),
            (u.CardNumber ?? "").Trim(),
            (u.JobTitle ?? "").Trim(),
            u.AccessRole,
            u.BiometricEnrolled,
            pLen,
            pHash,
            dLen,
            dHash,
            string.IsNullOrWhiteSpace(u.IdDocumentFileName) ? null : u.IdDocumentFileName.Trim());
    }
}

/// <summary>Stable length + FNV-1a hash for comparing binary profile / ID payloads without storing bytes in the snapshot.</summary>
internal static class StaffAccessAuditBinaryDigest
{
    public static (int Length, uint Hash) Of(byte[]? data)
    {
        if (data is not { Length: > 0 })
            return (0, 0u);
        uint h = 2166136261u;
        foreach (var b in data)
            h = unchecked((h ^ b) * 16777619u);
        if (h == 0)
            h = 1;
        return (data.Length, h);
    }
}

/// <summary>
/// Read/write store for <see cref="StaffAccessAuditEntry"/>. In-memory for the demo app; swap for EF/SQL
/// by implementing the same operations against your audit table.
/// </summary>
public static class StaffAccessAuditRepository
{
    private static readonly object Gate = new();
    private static readonly List<StaffAccessAuditEntry> All = new();
    private static bool _demoInstalled;

    public static void InstallDemoEntriesIfNeeded(IReadOnlyList<StaffUser> staffUsers)
    {
        lock (Gate)
        {
            if (_demoInstalled)
                return;
            _demoInstalled = true;
            foreach (var u in staffUsers)
            {
                foreach (var e in StaffAccessAuditSeed.BuildDemoEntries(u))
                    All.Add(e);
            }
        }
    }

    public static void Append(StaffAccessAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (Gate)
        {
            All.Add(entry);
        }
    }

    /// <summary>Records a remove-user attempt that was blocked (e.g. still linked in Operations).</summary>
    public static void AppendUserDeleteDenied(Guid subjectUserId, string reason)
    {
        var line = StaffAccessAuditTextSanitizer.OneLine(reason, 400);
        Append(new StaffAccessAuditEntry
        {
            Id = Guid.NewGuid(),
            SubjectUserId = subjectUserId,
            OccurredAtUtc = DateTime.UtcNow,
            Title = "Delete request denied",
            Description = string.IsNullOrEmpty(line)
                ? "A remove-user action was blocked by business rules."
                : "A remove-user action was blocked by business rules.\n• " + line,
            Tone = StaffAccessAuditEventTone.Grey,
            Source = StaffAccessAuditSource.Recorded,
            TabGlyphs = [],
            IsDeniedOutcome = true
        });
    }

    /// <summary>Records a successful staff account removal (after <see cref="StaffAccessStore.TryRemoveUser"/>).</summary>
    public static void AppendUserDeleteSucceeded(Guid subjectUserId, StaffUser removedUser)
    {
        var name = StaffAccessAuditTextSanitizer.OneLine(removedUser.DisplayName, 120);
        var un = StaffAccessAuditTextSanitizer.OneLine(removedUser.UserName, 120);
        var desc =
            $"Staff account was removed.\n• User name: \"{un}\" (ID {removedUser.NumericId.ToString(CultureInfo.CurrentCulture)}).\n• Display name: \"{name}\".";
        Append(new StaffAccessAuditEntry
        {
            Id = Guid.NewGuid(),
            SubjectUserId = subjectUserId,
            OccurredAtUtc = DateTime.UtcNow,
            Title = "User deleted",
            Description = desc,
            Tone = StaffAccessAuditEventTone.Teal,
            Source = StaffAccessAuditSource.Recorded,
            TabGlyphs = [],
            IsDeniedOutcome = false
        });
    }

    /// <summary>All audit rows for the user, newest first (UI binding).</summary>
    public static IReadOnlyList<StaffAccessAuditEntry> GetBySubjectUserNewestFirst(Guid subjectUserId)
    {
        lock (Gate)
        {
            return All.Where(e => e.SubjectUserId == subjectUserId)
                .OrderByDescending(e => e.OccurredAtUtc)
                .ToArray();
        }
    }

    /// <summary>Records one profile-save audit when at least one tracked field changed.</summary>
    public static void AppendProfileSaveIfChanged(
        Guid subjectUserId,
        StaffAccessAuditSnapshot before,
        StaffAccessAuditSnapshot after,
        bool passwordChanged)
    {
        var sb = new StringBuilder();
        var basicLines = new List<string>();
        if (before.IsActive != after.IsActive)
            basicLines.Add($"Sign-in status: {(after.IsActive ? "Active" : "Inactive")}");
        if (!string.Equals(before.UserName, after.UserName, StringComparison.OrdinalIgnoreCase))
            basicLines.Add($"User name: {Quote(after.UserName)}");
        if (!string.Equals(before.FirstName, after.FirstName, StringComparison.Ordinal))
            basicLines.Add($"First name: {Quote(after.FirstName)}");
        if (!string.Equals(before.MiddleName, after.MiddleName, StringComparison.Ordinal))
            basicLines.Add($"Middle name: {Quote(after.MiddleName)}");
        if (!string.Equals(before.Surname, after.Surname, StringComparison.Ordinal))
            basicLines.Add($"Surname: {Quote(after.Surname)}");
        if (!string.Equals(before.CardNumber, after.CardNumber, StringComparison.Ordinal))
            basicLines.Add($"Card number: {Quote(after.CardNumber)}");
        if (!string.Equals(before.JobTitle, after.JobTitle, StringComparison.Ordinal))
            basicLines.Add($"Job title (operations): {Quote(after.JobTitle)}");

        var accessRoleChanged = before.AccessRole != after.AccessRole;

        if (basicLines.Count > 0)
        {
            sb.AppendLine("Basic information:");
            foreach (var line in basicLines)
                sb.AppendLine("• " + line);
        }

        var securityLines = new List<string>();
        if (passwordChanged)
            securityLines.Add("Password was updated.");
        if (accessRoleChanged)
            securityLines.Add($"Access role was updated: {after.AccessRole}.");

        if (securityLines.Count > 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("Security:");
            foreach (var line in securityLines)
                sb.AppendLine("• " + line);
        }

        if (before.BiometricEnrolled != after.BiometricEnrolled)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("Biometric:");
            sb.AppendLine(after.BiometricEnrolled
                ? "• Biometric enrollment was updated: enrolled (fingerprint template on file)."
                : "• Biometric enrollment was updated: not enrolled (template removed).");
        }

        var docChanged = before.IdDocumentLength != after.IdDocumentLength ||
                         before.IdDocumentHash != after.IdDocumentHash;
        if (docChanged && (after.IdDocumentLength > 0 || before.IdDocumentLength > 0))
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("Documents:");
            if (after.IdDocumentLength > 0)
            {
                var name = string.IsNullOrWhiteSpace(after.IdDocumentFileName)
                    ? "ID document (PDF)"
                    : Quote(after.IdDocumentFileName!);
                var verb = before.IdDocumentLength > 0 ? "replaced" : "uploaded";
                sb.AppendLine($"• Identity document (PDF) was {verb}: {name}.");
            }
            else if (before.IdDocumentLength > 0)
            {
                sb.AppendLine("• Identity document (PDF) was removed.");
            }
        }

        var profileChanged = before.ProfileImageLength != after.ProfileImageLength ||
                             before.ProfileImageHash != after.ProfileImageHash;
        if (profileChanged)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("Profile:");
            if (after.ProfileImageLength > 0)
                sb.AppendLine("• Profile image was updated or replaced.");
            else if (before.ProfileImageLength > 0)
                sb.AppendLine("• Profile image was removed.");
        }

        if (sb.Length == 0)
            return;

        var description = sb.ToString().TrimEnd();

        var hasBasic = basicLines.Count > 0;
        var hasSecurity = securityLines.Count > 0;
        var hasBio = before.BiometricEnrolled != after.BiometricEnrolled;
        var hasDoc = docChanged;
        var hasProfile = profileChanged;

        var onlyPassword = passwordChanged && !hasBasic && !hasBio && !hasDoc && !hasProfile &&
                           securityLines.Count == 1;
        var onlyAccessRole = accessRoleChanged && !passwordChanged && !hasBasic && !hasBio && !hasDoc && !hasProfile;
        var onlyActive = before.IsActive != after.IsActive && basicLines.Count == 1 &&
                         basicLines[0].StartsWith("Sign-in status", StringComparison.Ordinal) &&
                         !hasSecurity && !hasBio && !hasDoc && !hasProfile;

        string title;
        StaffAccessAuditEventTone tone;
        if (onlyPassword)
        {
            title = "Password changed";
            tone = StaffAccessAuditEventTone.Purple;
        }
        else if (onlyAccessRole)
        {
            title = "Access role updated";
            tone = StaffAccessAuditEventTone.Purple;
        }
        else if (onlyActive)
        {
            title = "Sign-in status updated";
            tone = StaffAccessAuditEventTone.Teal;
        }
        else if (hasBasic && !hasSecurity && !hasBio && !hasDoc && !hasProfile)
        {
            title = "Basic information updated";
            tone = StaffAccessAuditEventTone.Blue;
        }
        else if (passwordChanged)
        {
            title = "User profile updated";
            tone = StaffAccessAuditEventTone.Purple;
        }
        else if (before.IsActive != after.IsActive)
        {
            title = "User profile updated";
            tone = StaffAccessAuditEventTone.Teal;
        }
        else
        {
            title = "User profile updated";
            tone = StaffAccessAuditEventTone.Blue;
        }

        var tabWant = new HashSet<StaffAccessAuditTabGlyph>();
        if (basicLines.Count > 0)
            tabWant.Add(StaffAccessAuditTabGlyph.BasicInfo);
        if (profileChanged)
            tabWant.Add(StaffAccessAuditTabGlyph.BasicInfo);
        if (securityLines.Count > 0)
            tabWant.Add(StaffAccessAuditTabGlyph.Security);
        if (before.BiometricEnrolled != after.BiometricEnrolled)
            tabWant.Add(StaffAccessAuditTabGlyph.Biometric);
        if (docChanged && (after.IdDocumentLength > 0 || before.IdDocumentLength > 0))
            tabWant.Add(StaffAccessAuditTabGlyph.Documents);

        var tabOrder = new List<StaffAccessAuditTabGlyph>();
        foreach (var g in new[]
                 {
                     StaffAccessAuditTabGlyph.BasicInfo, StaffAccessAuditTabGlyph.Security,
                     StaffAccessAuditTabGlyph.Biometric, StaffAccessAuditTabGlyph.Documents
                 })
        {
            if (tabWant.Contains(g))
                tabOrder.Add(g);
        }

        Append(new StaffAccessAuditEntry
        {
            Id = Guid.NewGuid(),
            SubjectUserId = subjectUserId,
            OccurredAtUtc = DateTime.UtcNow,
            Title = title,
            Description = description,
            Tone = tone,
            Source = StaffAccessAuditSource.Recorded,
            TabGlyphs = tabOrder,
            IncludesPasswordChange = passwordChanged,
            IncludesAccessRoleChange = accessRoleChanged
        });
    }

    private static string Quote(string? value)
    {
        var s = StaffAccessAuditTextSanitizer.OneLine(value);
        if (string.IsNullOrEmpty(s))
            return "\"\"";
        return "\"" + s.Replace("\"", "'", StringComparison.Ordinal) + "\"";
    }
}

file static class StaffAccessAuditTextSanitizer
{
    public static string OneLine(string? value, int maxLen = 240)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var s = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (s.Length > maxLen)
            s = s[..maxLen] + "…";
        return s;
    }
}

/// <summary>Initial demo rows inserted into <see cref="StaffAccessAuditRepository"/> once.</summary>
file static class StaffAccessAuditSeed
{
    public static IEnumerable<StaffAccessAuditEntry> BuildDemoEntries(StaffUser user)
    {
        if (user.NumericId == 1001)
        {
            yield return Entry(user.Id, "User profile updated",
                "Modified first name and contact information.",
                StaffAccessAuditEventTone.Blue,
                new DateTime(2025, 4, 10, 14, 32, 18, DateTimeKind.Utc));
            yield return Entry(user.Id, "Successful login",
                "Authentication via username and password.",
                StaffAccessAuditEventTone.Teal,
                new DateTime(2025, 4, 10, 9, 15, 42, DateTimeKind.Utc));
            yield return Entry(user.Id, "Session logout",
                "User initiated logout.",
                StaffAccessAuditEventTone.Grey,
                new DateTime(2025, 4, 10, 17, 58, 3, DateTimeKind.Utc));
            yield return Entry(user.Id, "Password changed",
                "Password updated successfully.",
                StaffAccessAuditEventTone.Purple,
                new DateTime(2025, 4, 9, 11, 4, 55, DateTimeKind.Utc));
            yield break;
        }

        var h = user.Id.GetHashCode();
        yield return Entry(user.Id, "Profile viewed",
            "Account details page opened.",
            StaffAccessAuditEventTone.Blue,
            DemoDateTimeUtc(h, 0));
        yield return Entry(user.Id, "Successful login",
            "Authentication completed.",
            StaffAccessAuditEventTone.Teal,
            DemoDateTimeUtc(h, 1));
    }

    private static StaffAccessAuditEntry Entry(
        Guid subjectUserId,
        string title,
        string description,
        StaffAccessAuditEventTone tone,
        DateTime occurredAtUtc,
        IReadOnlyList<StaffAccessAuditTabGlyph>? tabGlyphs = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            SubjectUserId = subjectUserId,
            OccurredAtUtc = occurredAtUtc,
            Title = title,
            Description = description,
            Tone = tone,
            Source = StaffAccessAuditSource.Demo,
            TabGlyphs = tabGlyphs ?? InferDemoTabGlyphs(title)
        };

    private static IReadOnlyList<StaffAccessAuditTabGlyph> InferDemoTabGlyphs(string title)
    {
        if (title.Contains("Password", StringComparison.OrdinalIgnoreCase))
            return [StaffAccessAuditTabGlyph.Security];
        if (title.Contains("Access role", StringComparison.OrdinalIgnoreCase))
            return [StaffAccessAuditTabGlyph.Security];
        if (title.Contains("profile", StringComparison.OrdinalIgnoreCase))
            return [StaffAccessAuditTabGlyph.BasicInfo];
        return [];
    }

    private static DateTime DemoDateTimeUtc(int seed, int slot)
    {
        var day = 8 + (Math.Abs(seed >> (4 + slot * 2)) % 20);
        var hh = 8 + (Math.Abs(seed >> (8 + slot)) % 10);
        var mm = Math.Abs(seed >> (12 + slot)) % 60;
        var ss = Math.Abs(seed >> (16 + slot)) % 60;
        return new DateTime(2025, 4, day, hh, mm, ss, DateTimeKind.Utc);
    }
}

public sealed class StaffAccessAuditSummaryVm
{
    public int TotalLogins { get; init; }
    public int SecurityEvents { get; init; }
    public int FailedAttempts { get; init; }
    public int TotalActions { get; init; }
    public IReadOnlyList<StaffAccessAuditEntryVm> Entries { get; init; } = [];
}

public sealed class StaffAccessAuditEntryVm
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Timestamp { get; init; } = "";
    public string Device { get; init; } = "";
    public string Ip { get; init; } = "";
    public string StatusLabel { get; init; } = "Success";
    public bool ShowSuccessOutcomeBadge { get; init; } = true;
    public bool ShowDeniedOutcomeBadge { get; init; }
    public Brush RowBackground { get; init; } = Brushes.White;
    public Brush IconForeground { get; init; } = Brushes.Gray;
    /// <summary>When true, row icon area shows the same Lucide tab strip glyphs as Staff user details.</summary>
    public bool ShowAnyTabGlyph { get; init; }
    public bool ShowDefaultActivityGlyph { get; init; }
    public bool ShowStaffTabBasicGlyph { get; init; }
    public bool ShowStaffTabSecurityGlyph { get; init; }
    public bool ShowStaffTabBiometricGlyph { get; init; }
    public bool ShowStaffTabDocumentsGlyph { get; init; }
}

/// <summary>Builds the audit trail view-model from the repository + headline demo stats.</summary>
public static class StaffAccessAuditPresentation
{
    public static StaffAccessAuditSummaryVm BuildSummary(StaffUser user)
    {
        StaffAccessStore.EnsureSeeded();
        var entries = StaffAccessAuditRepository.GetBySubjectUserNewestFirst(user.Id);
        var seedStats = GetHeadlineStats(user);
        var recorded = entries.Where(e => e.Source == StaffAccessAuditSource.Recorded).ToList();
        var journalSecurityAdds = recorded.Count(r =>
            r.IncludesPasswordChange
            || r.IncludesAccessRoleChange
            || r.Description.Contains("Password was updated", StringComparison.OrdinalIgnoreCase)
            || r.Description.Contains("Access role was updated", StringComparison.OrdinalIgnoreCase)
            || r.Description.Contains("Access role:", StringComparison.OrdinalIgnoreCase)
            || (r.Source == StaffAccessAuditSource.Recorded
                && r.Title.Equals("Password changed", StringComparison.OrdinalIgnoreCase))
            || (r.Source == StaffAccessAuditSource.Recorded
                && r.Title.Equals("Access role updated", StringComparison.OrdinalIgnoreCase)));

        return new StaffAccessAuditSummaryVm
        {
            TotalLogins = seedStats.TotalLogins,
            SecurityEvents = seedStats.SecurityEvents + journalSecurityAdds,
            FailedAttempts = seedStats.FailedAttempts,
            TotalActions = seedStats.TotalActions + recorded.Count,
            Entries = entries.Select(ToVm).ToList()
        };
    }

    private sealed class HeadlineStats
    {
        public int TotalLogins { get; init; }
        public int SecurityEvents { get; init; }
        public int FailedAttempts { get; init; }
        public int TotalActions { get; init; }
    }

    private static HeadlineStats GetHeadlineStats(StaffUser user)
    {
        if (user.NumericId == 1001)
        {
            return new HeadlineStats
            {
                TotalLogins = 2,
                SecurityEvents = 3,
                FailedAttempts = 2,
                TotalActions = 12
            };
        }

        var h = user.Id.GetHashCode();
        return new HeadlineStats
        {
            TotalLogins = 1 + (Math.Abs(h) % 5),
            SecurityEvents = 1 + (Math.Abs(h >> 3) % 6),
            FailedAttempts = Math.Abs(h >> 7) % 4,
            TotalActions = 5 + (Math.Abs(h >> 11) % 20)
        };
    }

    private static StaffAccessAuditEntryVm ToVm(StaffAccessAuditEntry r)
    {
        var (row, _, iconFg) = ToneColors(r.Tone);
        var local = r.OccurredAtUtc.Kind == DateTimeKind.Utc
            ? r.OccurredAtUtc.ToLocalTime()
            : DateTime.SpecifyKind(r.OccurredAtUtc, DateTimeKind.Utc).ToLocalTime();
        var glyphs = r.TabGlyphs;
        var set = new HashSet<StaffAccessAuditTabGlyph>(glyphs);
        var anyTab = set.Count > 0;
        var denied = r.IsDeniedOutcome;
        return new StaffAccessAuditEntryVm
        {
            Title = r.Title,
            Description = r.Description,
            Timestamp = local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
            Device = r.Device,
            Ip = r.Ip,
            StatusLabel = denied ? "Denied" : "Success",
            ShowSuccessOutcomeBadge = !denied,
            ShowDeniedOutcomeBadge = denied,
            RowBackground = Hex(row),
            IconForeground = Hex(iconFg),
            ShowAnyTabGlyph = anyTab,
            ShowDefaultActivityGlyph = !anyTab,
            ShowStaffTabBasicGlyph = set.Contains(StaffAccessAuditTabGlyph.BasicInfo),
            ShowStaffTabSecurityGlyph = set.Contains(StaffAccessAuditTabGlyph.Security),
            ShowStaffTabBiometricGlyph = set.Contains(StaffAccessAuditTabGlyph.Biometric),
            ShowStaffTabDocumentsGlyph = set.Contains(StaffAccessAuditTabGlyph.Documents)
        };
    }

    private static (string row, string iconBg, string iconFg) ToneColors(StaffAccessAuditEventTone tone) =>
        tone switch
        {
            StaffAccessAuditEventTone.Teal => ("#F0FDFA", "#CCFBF1", "#0D9488"),
            StaffAccessAuditEventTone.Purple => ("#FAF5FF", "#EDE9FE", "#7C3AED"),
            StaffAccessAuditEventTone.Grey => ("#F9FAFB", "#E5E7EB", "#6B7280"),
            StaffAccessAuditEventTone.Green => ("#F0FDF4", "#DCFCE7", "#16A34A"),
            _ => ("#EFF6FF", "#DBEAFE", "#2563EB")
        };

    private static SolidColorBrush Hex(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        b.Freeze();
        return b;
    }
}

