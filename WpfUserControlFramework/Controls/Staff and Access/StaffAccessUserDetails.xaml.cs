using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
    public bool BiometricEnrolled { get; set; }
    public string SecurityQuestion { get; set; } = "";
    public bool RequirePasswordChangeOnNextSignIn { get; set; }
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
                AccentColorHex = PaletteHex[i % PaletteHex.Length]
            });
        }
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
            UserName = string.IsNullOrWhiteSpace(userName) ? $"user.{next}" : userName.Trim(),
            FirstName = "New",
            MiddleName = "",
            Surname = "User",
            CardNumber = "",
            AccessRole = StaffAccessRole.User,
            JobTitle = "Server",
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
            DocsPanel.UploadRequested += (_, _) => PickIdPdf(false);
            DocsPanel.ViewRequested += (_, _) => ViewIdPdf();
            DocsPanel.ReuploadRequested += (_, _) => PickIdPdf(true);
            RolePicker.RoleChanged += (_, _) =>
            {
                if (_selected != null)
                    _selected.AccessRole = RolePicker.SelectedRole;
            };
        }

        RefreshList(selectUserId: StaffAccessStore.GetUsers().FirstOrDefault()?.Id);
    }

    private void RefreshList(Guid? selectUserId = null)
    {
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
    }

    private void LoadUserIntoForm(StaffUser u)
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
        SecurityPanel.LoadFromUser(u);
        BioPanel.LoadFromUser(u);
        DocsPanel.LoadFromUser(u);
        RefreshProfileChrome();
    }

    private void ClearForm()
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

    private void RefreshProfileChrome()
    {
        if (_selected == null)
            return;

        var has = _selected.ProfileImageBytes is { Length: > 0 };
        ProfilePlaceholder.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        ProfileImageHost.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        BtnViewProfileImage.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        BtnReuploadProfileImage.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
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
        if (UsersList.SelectedItem is StaffAccessListItemVm vm)
            LoadUserIntoForm(vm.User);
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
        var u = StaffAccessStore.AddUser(null);
        _searchQuery = "";
        SearchTextBox.Text = "";
        RefreshList(u.Id);
    }

    private void BtnDeleteUser_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid id)
            return;
        if (!StaffAccessStore.TryRemoveUser(id, out var err))
        {
            ShowStaffToast(err ?? "Could not remove user.", StaffToastKind.Error);
            return;
        }

        RefreshList(StaffAccessStore.GetUsers().FirstOrDefault()?.Id);
        ShowStaffToast("User removed.", StaffToastKind.Success);
    }

    private void BtnSave_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
        {
            MessageBox.Show(Window.GetWindow(this), "Select a user first.", "Save changes", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var pwdErr = SecurityPanel.ValidatePasswords();
        if (pwdErr != null)
        {
            MessageBox.Show(Window.GetWindow(this), pwdErr, "Security", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _selected.UserName = (TxtUserName.Text ?? "").Trim();
        _selected.FirstName = (TxtFirstName.Text ?? "").Trim();
        _selected.MiddleName = (TxtMiddleName.Text ?? "").Trim();
        _selected.Surname = (TxtSurname.Text ?? "").Trim();
        _selected.CardNumber = (TxtCardNumber.Text ?? "").Trim();
        _selected.JobTitle = (TxtJobTitle.Text ?? "").Trim();
        _selected.IsActive = TglActiveUser.IsChecked == true;
        _selected.AccessRole = RolePicker.SelectedRole;
        SecurityPanel.ApplyToUser(_selected);
        StaffAccessStore.Notify();
        var id = _selected.Id;
        RefreshList(id);
        ShowStaffToast("Changes saved.");
    }

    private void BtnAuditTrail_OnClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(Window.GetWindow(this), "Audit trail is not wired in this demo build.", "Audit trail",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnUploadProfile_OnClick(object sender, RoutedEventArgs e)
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
            var bytes = File.ReadAllBytes(dlg.FileName);
            using var ms = new MemoryStream(bytes);
            var probe = new BitmapImage();
            probe.BeginInit();
            probe.StreamSource = ms;
            probe.CacheOption = BitmapCacheOption.OnLoad;
            probe.EndInit();
            probe.Freeze();
            _selected.ProfileImageBytes = bytes;
            RefreshProfileChrome();
        }
        catch
        {
            MessageBox.Show(Window.GetWindow(this), "That file is not a valid image.", "Profile image",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnViewProfileImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected?.ProfileImageBytes is not { Length: > 0 } data)
            return;
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"staff-profile-{_selected.Id:N}.png");
            File.WriteAllBytes(path, data);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "View image", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void PickIdPdf(bool reupload)
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

            _selected.IdDocumentPdfBytes = bytes;
            _selected.IdDocumentFileName = Path.GetFileName(dlg.FileName);
            DocsPanel.LoadFromUser(_selected);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "ID document", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ViewIdPdf()
    {
        if (_selected?.IdDocumentPdfBytes is not { Length: > 0 } data)
            return;
        try
        {
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
}
