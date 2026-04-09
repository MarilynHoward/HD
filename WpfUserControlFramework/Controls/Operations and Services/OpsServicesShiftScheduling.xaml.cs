using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public partial class OpsServicesShiftScheduling : UserControl
{
    private enum OpsScheduleViewMode
    {
        Day,
        Week,
        Month
    }

    private readonly Action _navigateToTableManagement;
    private readonly Action _openAddShiftDialog;
    private readonly DispatcherTimer _staffSearchDebounce;
    private string _staffSearchQuery = "";
    private DateTime _anchorDate = DateTime.Today;
    private OpsScheduleViewMode _viewMode = OpsScheduleViewMode.Week;

    public OpsServicesShiftScheduling(
        Action navigateToTableManagement,
        Action openAddShiftDialog)
    {
        _navigateToTableManagement = navigateToTableManagement ?? throw new ArgumentNullException(nameof(navigateToTableManagement));
        _openAddShiftDialog = openAddShiftDialog ?? throw new ArgumentNullException(nameof(openAddShiftDialog));
        _staffSearchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _staffSearchDebounce.Tick += StaffSearchDebounce_Tick;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        OpsServicesStore.EnsureSeeded();
        OpsServicesStore.DataChanged += OnStoreChanged;
        foreach (ComboBoxItem item in CmbViewMode.Items)
        {
            if (item.Tag is string t && t == "Week")
            {
                CmbViewMode.SelectedItem = item;
                break;
            }
        }

        HighlightShiftPill(true);
        RebuildAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        OpsServicesStore.DataChanged -= OnStoreChanged;
        _staffSearchDebounce.Stop();
        _staffSearchDebounce.Tick -= StaffSearchDebounce_Tick;
    }

    private void StaffFilterSearchBoxBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (StaffSearchTextBox.IsKeyboardFocusWithin)
                return;
            StaffSearchTextBox.Focus();
            Keyboard.Focus(StaffSearchTextBox);
        }), DispatcherPriority.Input);
    }

    private void StaffSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        _staffSearchDebounce.Stop();
        _staffSearchDebounce.Start();
    }

    private void StaffSearchDebounce_Tick(object? sender, EventArgs e)
    {
        _staffSearchDebounce.Stop();
        _staffSearchQuery = (StaffSearchTextBox.Text ?? string.Empty).Trim();
        RebuildAll();
    }

    /// <summary>
    /// The horizontal schedule viewer does not scroll vertically; forward wheel to the page viewer so the toolbar/filter stay visible.
    /// Shift + wheel scrolls horizontally in month view when needed.
    /// </summary>
    private void ScheduleHorizontalScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer inner)
            return;

        if (Keyboard.Modifiers == ModifierKeys.Shift && inner.ScrollableWidth > 0)
        {
            var delta = e.Delta > 0 ? -48.0 : 48.0;
            inner.ScrollToHorizontalOffset(Math.Max(0, Math.Min(inner.ScrollableWidth, inner.HorizontalOffset + delta)));
            e.Handled = true;
            return;
        }

        if (SchedulePageScrollViewer == null)
            return;

        if (e.Delta > 0)
            SchedulePageScrollViewer.LineUp();
        else
            SchedulePageScrollViewer.LineDown();
        e.Handled = true;
    }

    private void OnStoreChanged(object? sender, EventArgs e) => Dispatcher.Invoke(RebuildAll);

    private void HighlightShiftPill(bool shiftSelected)
    {
        var blue = TryBrush("Brush.PrimaryBlue", "#2563EB");

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

        if (shiftSelected)
        {
            SetActive(PillShiftScheduling);
            SetInactive(PillFloorPlan);
            SetInactive(PillTableManagement);
        }
        else
        {
            SetInactive(PillShiftScheduling);
            SetInactive(PillFloorPlan);
            SetInactive(PillTableManagement);
        }
    }

    private double S(double basePx)
    {
        var state = Application.Current.Resources["UiScaleState"] as UiScaleState;
        return basePx * (state?.FontScale ?? 1.25);
    }

    private Brush TryBrush(string resourceKey, string fallbackHex)
    {
        if (TryFindResource(resourceKey) is Brush b)
            return b;
        return new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(fallbackHex)!);
    }

    private Effect CloneCardShadow()
    {
        if (TryFindResource("OpsScheduleCardShadow") is DropShadowEffect src)
            return (Effect)src.Clone();

        return new DropShadowEffect
        {
            BlurRadius = 8,
            Direction = 270,
            Opacity = 0.06,
            ShadowDepth = 1,
            Color = Colors.Black
        };
    }

    private static string FormatShiftTimeRange(TimeOnly start, TimeOnly end) =>
        $"{start.ToString("HH:mm", CultureInfo.InvariantCulture)} to {end.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    private static string ShiftCountLabel(int n) => n == 1 ? "1 shift" : $"{n} shifts";

    private void RebuildAll()
    {
        UpdateViewModeFromCombo();
        var dates = GetVisibleDates();
        UpdateRangeLabel(dates);
        RebuildScheduleGrid(dates);
        RebuildFooterCounts(dates);
    }

    private void UpdateViewModeFromCombo()
    {
        if (CmbViewMode.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _viewMode = tag switch
            {
                "Day" => OpsScheduleViewMode.Day,
                "Month" => OpsScheduleViewMode.Month,
                _ => OpsScheduleViewMode.Week
            };
        }
    }

    private IReadOnlyList<DateOnly> GetVisibleDates()
    {
        var d = _anchorDate.Date;
        return _viewMode switch
        {
            OpsScheduleViewMode.Day => new[] { DateOnly.FromDateTime(d) },
            OpsScheduleViewMode.Week => Enumerable.Range(0, 7)
                .Select(i => DateOnly.FromDateTime(OpsServicesStore.StartOfWeekMonday(d).AddDays(i)))
                .ToList(),
            OpsScheduleViewMode.Month => GetMonthDates(d),
            _ => Array.Empty<DateOnly>()
        };
    }

    private static IReadOnlyList<DateOnly> GetMonthDates(DateTime anyInMonth)
    {
        var first = new DateOnly(anyInMonth.Year, anyInMonth.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var list = new List<DateOnly>();
        for (var x = first; x <= last; x = x.AddDays(1))
            list.Add(x);
        return list;
    }

    private void UpdateRangeLabel(IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count == 0)
        {
            TxtRangeLabel.Text = "";
            return;
        }

        var first = dates[0];
        var last = dates[^1];
        TxtRangeLabel.Text = _viewMode switch
        {
            OpsScheduleViewMode.Day => first.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture),
            OpsScheduleViewMode.Month => first.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            _ => $"{first:ddd d MMM} – {last:ddd d MMM yyyy}"
        };
    }

    private void RebuildScheduleGrid(IReadOnlyList<DateOnly> dates)
    {
        ScheduleHostGrid.Children.Clear();
        ScheduleHostGrid.RowDefinitions.Clear();
        ScheduleHostGrid.ColumnDefinitions.Clear();

        var totalStaff = OpsServicesStore.GetEmployees().Count;
        var q = _staffSearchQuery.Trim();
        var employees = OpsServicesStore.GetEmployees()
            .Where(e => string.IsNullOrEmpty(q)
                        || e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || e.Role.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name)
            .ToList();
        double staffCol = S(200);
        ScheduleHostGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(staffCol) });
        // Month: enforce readable column mins + min total width so horizontal scroll appears when needed.
        // Week/Day: MinWidth 0 on * columns so all days fit the viewport (no clipping); text wraps in cells.
        var dayColMin = _viewMode == OpsScheduleViewMode.Month ? S(72) : 0d;
        foreach (var _ in dates)
        {
            ScheduleHostGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = dayColMin
            });
        }

        ScheduleHostGrid.MinWidth = _viewMode == OpsScheduleViewMode.Month
            ? staffCol + dates.Count * dayColMin
            : 0;
        ScheduleHostGrid.ClearValue(FrameworkElement.MaxWidthProperty);

        int row = 0;
        ScheduleHostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddCornerCell(row, employees.Count, totalStaff);
        for (int c = 0; c < dates.Count; c++)
            AddHeaderCell(row, c + 1, dates[c]);
        row++;

        foreach (var emp in employees)
        {
            ScheduleHostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddStaffCell(row, 0, emp);
            for (int c = 0; c < dates.Count; c++)
                AddDayCell(row, c + 1, emp, dates[c]);
            row++;
        }

        // Totals row
        ScheduleHostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddTotalsLabelCell(row, 0);
        for (int c = 0; c < dates.Count; c++)
            AddTotalsCountCell(row, c + 1, dates[c]);
    }

    private void AddCornerCell(int row, int visibleCount, int totalCount)
    {
        var label = visibleCount == totalCount
            ? $"Staff ({totalCount})"
            : $"Staff ({visibleCount} of {totalCount})";
        var tb = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = TryBrush("MainForeground", "#111827"),
            FontSize = S(16),
            Margin = new Thickness(S(12), S(10), S(12), S(10)),
            VerticalAlignment = VerticalAlignment.Center
        };
        var chrome = new Border
        {
            Background = TryBrush("Brush.White", "#FFFFFF"),
            BorderBrush = TryBrush("Brush.BorderSoft", "#EEF1F5"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = tb
        };
        Grid.SetRow(chrome, row);
        Grid.SetColumn(chrome, 0);
        ScheduleHostGrid.Children.Add(chrome);
    }

    private void AddHeaderCell(int row, int col, DateOnly date)
    {
        var sp = new StackPanel { Margin = new Thickness(S(6), S(8), S(6), S(8)) };
        sp.Children.Add(new TextBlock
        {
            Text = date.ToString("ddd", CultureInfo.CurrentCulture),
            FontWeight = FontWeights.SemiBold,
            FontSize = S(13),
            Foreground = TryBrush("Brush.TextMuted", "#6B7280"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        sp.Children.Add(new TextBlock
        {
            Text = date.ToString("d MMM", CultureInfo.CurrentCulture),
            FontSize = S(14),
            FontWeight = FontWeights.SemiBold,
            Foreground = TryBrush("MainForeground", "#111827"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, S(2), 0, 0)
        });
        var border = new Border
        {
            Background = TryBrush("Brush.White", "#FFFFFF"),
            BorderBrush = TryBrush("Brush.BorderSoft", "#EEF1F5"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(S(12)),
            Margin = new Thickness(S(4), S(4), S(4), S(4)),
            Child = sp,
            Effect = CloneCardShadow()
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        ScheduleHostGrid.Children.Add(border);
    }

    private void AddStaffCell(int row, int col, OpsEmployee emp)
    {
        var dot = new Ellipse
        {
            Width = S(10),
            Height = S(10),
            Fill = (Brush)new BrushConverter().ConvertFromString(emp.AccentColorHex)!,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, S(8), 0)
        };
        var name = new TextBlock
        {
            Text = emp.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = S(14),
            TextWrapping = TextWrapping.Wrap
        };
        var role = new TextBlock
        {
            Text = emp.Role,
            Foreground = TryBrush("Brush.TextMuted", "#6B7280"),
            FontSize = S(13)
        };
        var datesForHours = GetVisibleDates();
        var hours = datesForHours.Count == 0
            ? 0
            : OpsServicesStore.TotalHoursForEmployeeInRange(emp.Id, datesForHours[0], datesForHours[^1]);
        var hoursTb = new TextBlock
        {
            Text = $"{hours}h",
            FontSize = S(13),
            Foreground = TryBrush("Brush.PrimaryBlue", "#2563EB"),
            HorizontalAlignment = HorizontalAlignment.Right,
            FontWeight = FontWeights.SemiBold
        };
        var nameRow = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(hoursTb, System.Windows.Controls.Dock.Right);
        nameRow.Children.Add(hoursTb);
        var nameStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        nameStack.Children.Add(dot);
        nameStack.Children.Add(name);
        nameRow.Children.Add(nameStack);

        var sp = new StackPanel { Margin = new Thickness(S(10), S(8), S(10), S(8)) };
        sp.Children.Add(nameRow);
        sp.Children.Add(role);

        var border = new Border
        {
            Background = TryBrush("Brush.White", "#FFFFFF"),
            BorderBrush = TryBrush("Brush.BorderSoft", "#EEF1F5"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            CornerRadius = new CornerRadius(S(12)),
            Margin = new Thickness(S(4), S(3), S(4), S(3)),
            Child = sp,
            Effect = CloneCardShadow()
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        ScheduleHostGrid.Children.Add(border);
    }

    private void AddDayCell(int row, int col, OpsEmployee emp, DateOnly date)
    {
        var shifts = OpsServicesStore.GetShifts()
            .Where(s => s.EmployeeId == emp.Id && s.Date == date)
            .OrderBy(s => s.Start)
            .ToList();

        var stack = new StackPanel { Margin = new Thickness(S(6), S(6), S(6), S(6)) };
        foreach (var shift in shifts)
        {
            var accent = (Brush)new BrushConverter().ConvertFromString(emp.AccentColorHex)!;
            var border = new Border
            {
                Background = accent is SolidColorBrush sb
                    ? new SolidColorBrush(Color.FromArgb(36, sb.Color.R, sb.Color.G, sb.Color.B))
                    : accent,
                BorderBrush = accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(S(12)),
                Padding = new Thickness(S(8), S(6), S(8), S(6)),
                Margin = new Thickness(0, 0, 0, S(6))
            };
            var timeTb = new TextBlock
            {
                Text = FormatShiftTimeRange(shift.Start, shift.End),
                FontSize = S(13),
                FontWeight = FontWeights.SemiBold,
                Foreground = TryBrush("MainForeground", "#111827"),
                TextWrapping = TextWrapping.Wrap
            };
            var tablesTb = new TextBlock
            {
                Text = OpsServicesStore.TableNamesSummary(shift.TableIds),
                FontSize = S(12),
                Foreground = TryBrush("DimmedForeground", "#687280"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, S(4), 0, 0)
            };
            var inner = new StackPanel();
            inner.Children.Add(timeTb);
            inner.Children.Add(tablesTb);
            border.Child = inner;
            stack.Children.Add(border);
        }

        var cell = new Border
        {
            Background = TryBrush("Brush.White", "#FFFFFF"),
            BorderBrush = TryBrush("Brush.BorderSoft", "#EEF1F5"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            MinHeight = S(72),
            Child = stack
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        ScheduleHostGrid.Children.Add(cell);
    }

    private void AddTotalsLabelCell(int row, int col)
    {
        var tb = new TextBlock
        {
            Text = "Daily total",
            FontSize = S(13),
            FontWeight = FontWeights.SemiBold,
            Foreground = TryBrush("Brush.TextMuted", "#6B7280"),
            Margin = new Thickness(S(12), S(10), S(12), S(10)),
            VerticalAlignment = VerticalAlignment.Center
        };
        var wrap = new Border
        {
            Background = TryBrush("Brush.SurfaceOffWhite", "#F9FAFB"),
            BorderBrush = TryBrush("Brush.BorderSoft", "#EEF1F5"),
            BorderThickness = new Thickness(0, 1, 1, 0),
            Child = tb
        };
        Grid.SetRow(wrap, row);
        Grid.SetColumn(wrap, col);
        ScheduleHostGrid.Children.Add(wrap);
    }

    private void AddTotalsCountCell(int row, int col, DateOnly date)
    {
        var n = OpsServicesStore.GetShifts().Count(s => s.Date == date);
        var tb = new TextBlock
        {
            Text = ShiftCountLabel(n),
            FontSize = S(13),
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(S(6), S(10), S(6), S(10)),
            Foreground = TryBrush("MainForeground", "#111827")
        };
        var wrap = new Border
        {
            Background = TryBrush("Brush.SurfaceOffWhite", "#F9FAFB"),
            BorderBrush = TryBrush("Brush.BorderSoft", "#EEF1F5"),
            BorderThickness = new Thickness(0, 1, 1, 0),
            Child = tb
        };
        Grid.SetRow(wrap, row);
        Grid.SetColumn(wrap, col);
        ScheduleHostGrid.Children.Add(wrap);
    }

    private void RebuildFooterCounts(IReadOnlyList<DateOnly> dates)
    {
        FooterShiftCountsRow.Children.Clear();
        // Summary strip mirrors grid totals row; keep panel empty or hide
        FooterShiftCountsRow.Visibility = Visibility.Collapsed;

        var shiftsInRange = OpsServicesStore.GetShifts()
            .Where(s => dates.Count > 0 && s.Date >= dates[0] && s.Date <= dates[^1])
            .ToList();
        var totalHours = shiftsInRange.Sum(s =>
        {
            var span = s.End.ToTimeSpan() - s.Start.ToTimeSpan();
            if (span.TotalHours < 0)
                span += TimeSpan.FromHours(24);
            return span.TotalHours;
        });

        TxtStatStaff.Text = $"{OpsServicesStore.GetEmployees().Count} Total Staff";
        TxtStatShifts.Text = $"{shiftsInRange.Count} Scheduled Shifts";
        TxtStatHours.Text = $"{Math.Round(totalHours, 0)}h Total Hours";
    }

    private void BtnPrevRange_Click(object sender, RoutedEventArgs e)
    {
        _anchorDate = _viewMode switch
        {
            OpsScheduleViewMode.Day => _anchorDate.AddDays(-1),
            OpsScheduleViewMode.Week => _anchorDate.AddDays(-7),
            OpsScheduleViewMode.Month => _anchorDate.AddMonths(-1),
            _ => _anchorDate
        };
        RebuildAll();
    }

    private void BtnNextRange_Click(object sender, RoutedEventArgs e)
    {
        _anchorDate = _viewMode switch
        {
            OpsScheduleViewMode.Day => _anchorDate.AddDays(1),
            OpsScheduleViewMode.Week => _anchorDate.AddDays(7),
            OpsScheduleViewMode.Month => _anchorDate.AddMonths(1),
            _ => _anchorDate
        };
        RebuildAll();
    }

    private void BtnToday_Click(object sender, RoutedEventArgs e)
    {
        _anchorDate = DateTime.Today;
        RebuildAll();
    }

    private void CmbViewMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        RebuildAll();
    }

    private void BtnAddShift_Click(object sender, RoutedEventArgs e) => _openAddShiftDialog();

    private void PillShiftScheduling_Click(object sender, RoutedEventArgs e)
    {
        HighlightShiftPill(true);
    }

    private void PillTableManagement_Click(object sender, RoutedEventArgs e) =>
        _navigateToTableManagement();

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(Window.GetWindow(this), "Export schedule is not wired in this demo build.",
            "Export Schedule", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

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

    /// <summary>List card line; resolved from <see cref="AssignedWaiterId"/> (demo store).</summary>
    public string AssignedWaiterDisplay =>
        AssignedWaiterId is { } id ? OpsServicesStore.GetEmployee(id)?.Name ?? "" : "";
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
