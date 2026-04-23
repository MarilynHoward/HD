using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private readonly Action _navigateToFloorPlan;
    private readonly Action _openAddShiftDialog;
    private readonly DispatcherTimer _staffSearchDebounce;
    private string _staffSearchQuery = "";
    private DateTime _anchorDate = DateTime.Today;
    private OpsScheduleViewMode _viewMode = OpsScheduleViewMode.Week;
    private HwndSource? _monthScrollHwndSource;
    /// <summary>Month only: horizontal scroll for day columns; staff column stays in the sibling grid cell.</summary>
    private ScrollViewer? _monthDaysHorizontalScroll;
    private bool _storeRefreshPosted;
    private bool _shiftSchedulingUnloaded;

    public OpsServicesShiftScheduling(
        Action navigateToTableManagement,
        Action navigateToFloorPlan,
        Action openAddShiftDialog)
    {
        _navigateToTableManagement = navigateToTableManagement ?? throw new ArgumentNullException(nameof(navigateToTableManagement));
        _navigateToFloorPlan = navigateToFloorPlan ?? throw new ArgumentNullException(nameof(navigateToFloorPlan));
        _openAddShiftDialog = openAddShiftDialog ?? throw new ArgumentNullException(nameof(openAddShiftDialog));
        _staffSearchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _staffSearchDebounce.Tick += StaffSearchDebounce_Tick;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _shiftSchedulingUnloaded = false;
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
        Dispatcher.BeginInvoke(new Action(AttachMonthScrollHwndHook), DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _shiftSchedulingUnloaded = true;
        DetachMonthScrollHwndHook();
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
    /// Month: Shift or Ctrl + wheel pans horizontally; plain wheel scrolls the vertical schedule.
    /// </summary>
    private void MonthScheduleClipBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewMode != OpsScheduleViewMode.Month)
            return;

        var sv = _monthDaysHorizontalScroll;
        var wantHorizontal = sv is { ScrollableWidth: > 0.5 }
                             && (Keyboard.Modifiers == ModifierKeys.Shift || Keyboard.Modifiers == ModifierKeys.Control);
        if (wantHorizontal && sv != null)
        {
            var delta = e.Delta > 0 ? -S(48) : S(48);
            var next = Math.Max(0, Math.Min(sv.ScrollableWidth, sv.HorizontalOffset + delta));
            sv.ScrollToHorizontalOffset(next);
            UpdateMonthPanButtons();
            e.Handled = true;
            return;
        }

        if (e.Delta > 0)
            ScheduleMonthInnerVerticalScroll.LineUp();
        else
            ScheduleMonthInnerVerticalScroll.LineDown();
        e.Handled = true;
    }

    private void BtnMonthScrollDaysLeft_Click(object sender, RoutedEventArgs e) =>
        NudgeMonthHorizontalScrollByPages(-1);

    private void BtnMonthScrollDaysRight_Click(object sender, RoutedEventArgs e) =>
        NudgeMonthHorizontalScrollByPages(1);

    private void NudgeMonthHorizontalScrollByPages(int pageDir)
    {
        if (_monthDaysHorizontalScroll is not { ScrollableWidth: > 0.5 } sv)
            return;
        var step = Math.Max(S(48), sv.ViewportWidth * 0.85);
        var next = Math.Max(0, Math.Min(sv.ScrollableWidth, sv.HorizontalOffset + pageDir * step));
        sv.ScrollToHorizontalOffset(next);
        UpdateMonthPanButtons();
    }

    private void UpdateMonthPanButtons()
    {
        if (!IsLoaded)
            return;
        var month = _viewMode == OpsScheduleViewMode.Month;
        BtnMonthScrollDaysLeft.Visibility = month ? Visibility.Visible : Visibility.Collapsed;
        BtnMonthScrollDaysRight.Visibility = month ? Visibility.Visible : Visibility.Collapsed;
        if (!month)
            return;
        var sv = _monthDaysHorizontalScroll;
        if (sv == null)
        {
            BtnMonthScrollDaysLeft.IsEnabled = false;
            BtnMonthScrollDaysRight.IsEnabled = false;
            return;
        }

        var canPan = sv.ScrollableWidth > 0.5;
        BtnMonthScrollDaysLeft.IsEnabled = canPan && sv.HorizontalOffset > 0.01;
        BtnMonthScrollDaysRight.IsEnabled = canPan && sv.HorizontalOffset < sv.ScrollableWidth - 0.01;
    }

    private void AttachMonthScrollHwndHook()
    {
        if (_monthScrollHwndSource != null)
            return;
        if (PresentationSource.FromVisual(this) is not HwndSource src)
            return;
        src.AddHook(MonthScrollWndProc);
        _monthScrollHwndSource = src;
    }

    private void DetachMonthScrollHwndHook()
    {
        if (_monthScrollHwndSource == null)
            return;
        _monthScrollHwndSource.RemoveHook(MonthScrollWndProc);
        _monthScrollHwndSource = null;
    }

    private IntPtr MonthScrollWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEHWHEEL = 0x020E;
        if (msg != WM_MOUSEHWHEEL)
            return IntPtr.Zero;
        if (_viewMode != OpsScheduleViewMode.Month)
            return IntPtr.Zero;
        if (_monthDaysHorizontalScroll is not { ScrollableWidth: > 0.5 } sv)
            return IntPtr.Zero;

        var xy = unchecked((uint)lParam.ToInt64());
        var x = unchecked((short)(xy & 0xFFFF));
        var y = unchecked((short)((xy >> 16) & 0xFFFF));
        var screen = new System.Windows.Point(x, y);
        try
        {
            var local = ScheduleMonthHostGrid.PointFromScreen(screen);
            if (double.IsNaN(local.X) || local.X < -4 || local.Y < -4
                || local.X > ScheduleMonthHostGrid.ActualWidth + 4 || local.Y > ScheduleMonthHostGrid.ActualHeight + 4)
                return IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }

        var delta = unchecked((short)(((uint)wParam.ToInt64() >> 16) & 0xFFFF));
        var step = S(48) * (Math.Max(120, Math.Abs((int)delta)) / 120.0);
        var deltaMove = delta > 0 ? -step : step;
        var next = Math.Max(0, Math.Min(sv.ScrollableWidth, sv.HorizontalOffset + deltaMove));
        sv.ScrollToHorizontalOffset(next);
        UpdateMonthPanButtons();
        handled = true;
        return IntPtr.Zero;
    }

    private void MonthScheduleClipBorder_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateMonthPanButtons();

    private void ScheduleMonthInnerVerticalScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewMode != OpsScheduleViewMode.Month)
            return;
        UpdateMonthPanButtons();
    }

    /// <summary>
    /// Week/Day: wheel scrolls the vertical body (date headers stay outside that viewer). Shift + wheel pans horizontally.
    /// </summary>
    private void ScheduleHorizontalScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer inner)
            return;

        if (Keyboard.Modifiers == ModifierKeys.Shift && inner.ScrollableWidth > 0)
        {
            var step = S(48);
            var delta = e.Delta > 0 ? -step : step;
            inner.ScrollToHorizontalOffset(Math.Max(0, Math.Min(inner.ScrollableWidth, inner.HorizontalOffset + delta)));
            e.Handled = true;
            return;
        }

        if (_viewMode != OpsScheduleViewMode.Month && ScheduleWeekBodyScrollViewer != null)
        {
            var body = ScheduleWeekBodyScrollViewer;
            if (e.Delta > 0)
                body.LineUp();
            else
                body.LineDown();
            e.Handled = true;
        }
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (_shiftSchedulingUnloaded || _storeRefreshPosted)
            return;
        _storeRefreshPosted = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _storeRefreshPosted = false;
            if (_shiftSchedulingUnloaded)
                return;
            RebuildAll();
        }), DispatcherPriority.DataBind);
    }

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

    private static void ApplyWeekDayColumnDefinitions(Grid grid, double staffCol, int dateCount)
    {
        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(staffCol) });
        for (var i = 0; i < dateCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 0
            });
        }
    }

    /// <summary>Week/day schedule: host the week layout in the chrome inner area.</summary>
    private void EnsureScheduleInnerHostWeekLayout()
    {
        ScheduleInnerHost.Children.Clear();
        ScheduleInnerHost.Children.Add(ScheduleWeekLayoutRoot);
    }

    /// <summary>Month schedule: host the generated month grid in the chrome inner area.</summary>
    private void EnsureScheduleInnerHostMonthLayout(Grid monthRoot)
    {
        ScheduleInnerHost.Children.Clear();
        ScheduleInnerHost.Children.Add(monthRoot);
    }

    /// <summary>Moves the staff filter search into a corner-slot border (below Staff (n) in the header card).</summary>
    private void AttachStaffFilterSearchToCornerSlot(Border slot)
    {
        if (ReferenceEquals(slot.Child, StaffFilterSearchBoxBorder))
            return;
        if (StaffFilterSearchBoxBorder.Parent is Border oldHost)
            oldHost.Child = null;
        StaffFilterSearchBoxBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        StaffFilterSearchBoxBorder.Margin = new Thickness(0);
        slot.Child = StaffFilterSearchBoxBorder;
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

    private static string FormatShiftStartTime(TimeOnly t) =>
        t.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string FormatShiftEndLine(TimeOnly end) =>
        $"to {end.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    /// <summary>Simple clock glyph (white strokes) for compact shift cards.</summary>
    private static FrameworkElement CreateShiftClockIcon(double iconSize, double trailingGap)
    {
        const double c = 24;
        var box = new Viewbox
        {
            Width = iconSize,
            Height = iconSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, trailingGap, 0)
        };
        var canvas = new Canvas { Width = c, Height = c };
        var stroke = Brushes.White;
        var face = new Ellipse
        {
            Width = 18,
            Height = 18,
            Stroke = stroke,
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(face, 3);
        Canvas.SetTop(face, 3);
        canvas.Children.Add(face);
        canvas.Children.Add(new Line
        {
            X1 = 12,
            Y1 = 12,
            X2 = 12,
            Y2 = 7.5,
            Stroke = stroke,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        canvas.Children.Add(new Line
        {
            X1 = 12,
            Y1 = 12,
            X2 = 16.5,
            Y2 = 12,
            Stroke = stroke,
            StrokeThickness = 1.75,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        box.Child = canvas;
        return box;
    }

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
        var totalStaff = OpsServicesStore.GetEmployees().Count;
        var q = _staffSearchQuery.Trim();
        var employees = OpsServicesStore.GetEmployees()
            .Where(e => string.IsNullOrEmpty(q)
                        || e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || e.Role.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name)
            .ToList();
        var staffCol = S(200);

        if (_viewMode == OpsScheduleViewMode.Month)
        {
            ScheduleHostGrid.Children.Clear();
            ScheduleHostGrid.RowDefinitions.Clear();
            ScheduleHostGrid.ColumnDefinitions.Clear();

            var monthDayColW = S(72);
            var daysOnlyW = dates.Count * monthDayColW;

            var monthRoot = new Grid();
            Grid.SetIsSharedSizeScope(monthRoot, true);
            monthRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(staffCol) });
            monthRoot.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = S(120)
            });

            var leftGrid = new Grid();
            var daysGrid = new Grid
            {
                Width = daysOnlyW,
                MinWidth = daysOnlyW,
                HorizontalAlignment = HorizontalAlignment.Left,
                SnapsToDevicePixels = true
            };
            foreach (var _ in dates)
            {
                daysGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(monthDayColW)
                });
            }

            var totalRows = 1 + employees.Count + 1;
            for (var r = 0; r < totalRows; r++)
            {
                var sg = "OpSchedRow" + r;
                leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, SharedSizeGroup = sg });
                daysGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, SharedSizeGroup = sg });
            }

            var row = 0;
            AddCornerCell(leftGrid, row, employees.Count, totalStaff);
            for (var c = 0; c < dates.Count; c++)
                AddHeaderCell(daysGrid, row, c, dates[c]);
            row++;

            foreach (var emp in employees)
            {
                AddStaffCell(leftGrid, row, 0, emp);
                for (var c = 0; c < dates.Count; c++)
                    AddDayCell(daysGrid, row, c, emp, dates[c]);
                row++;
            }

            AddTotalsLabelCell(leftGrid, row, 0);
            for (var c = 0; c < dates.Count; c++)
                AddTotalsCountCell(daysGrid, row, c, dates[c]);

            var daysScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false,
                FocusVisualStyle = null,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = daysGrid
            };
            daysScroll.ScrollChanged += (_, _) => UpdateMonthPanButtons();
            _monthDaysHorizontalScroll = daysScroll;

            Grid.SetColumn(leftGrid, 0);
            Grid.SetColumn(daysScroll, 1);
            monthRoot.Children.Add(leftGrid);
            monthRoot.Children.Add(daysScroll);

            ScheduleGridChromeBorder.ClearValue(FrameworkElement.WidthProperty);
            ScheduleGridChromeBorder.ClearValue(FrameworkElement.MinWidthProperty);
            ScheduleGridChromeBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            EnsureScheduleInnerHostMonthLayout(monthRoot);

            SyncScheduleScrollHostForViewMode();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _monthDaysHorizontalScroll?.ScrollToHorizontalOffset(0);
                MonthScheduleClipBorder.UpdateLayout();
                UpdateMonthPanButtons();
            }), DispatcherPriority.Loaded);

            UpdateMonthPanButtons();
            return;
        }

        _monthDaysHorizontalScroll = null;

        EnsureScheduleInnerHostWeekLayout();

        ApplyWeekDayColumnDefinitions(ScheduleWeekHeaderGrid, staffCol, dates.Count);
        ApplyWeekDayColumnDefinitions(ScheduleHostGrid, staffCol, dates.Count);

        ScheduleWeekHeaderGrid.Children.Clear();

        const int headerRow = 0;
        AddCornerCell(ScheduleWeekHeaderGrid, headerRow, employees.Count, totalStaff);
        for (var c = 0; c < dates.Count; c++)
            AddHeaderCell(ScheduleWeekHeaderGrid, headerRow, c + 1, dates[c]);

        ScheduleHostGrid.Children.Clear();
        ScheduleHostGrid.RowDefinitions.Clear();
        ScheduleHostGrid.ClearValue(FrameworkElement.WidthProperty);
        ScheduleHostGrid.MinWidth = 0;
        ScheduleHostGrid.ClearValue(FrameworkElement.HorizontalAlignmentProperty);
        ScheduleHostGrid.ClearValue(FrameworkElement.MaxWidthProperty);

        ScheduleGridChromeBorder.ClearValue(FrameworkElement.WidthProperty);
        ScheduleGridChromeBorder.ClearValue(FrameworkElement.MinWidthProperty);
        ScheduleGridChromeBorder.ClearValue(FrameworkElement.MaxWidthProperty);
        ScheduleGridChromeBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        ScheduleHorizontalScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        ScheduleHScrollExtentGrid.ClearValue(FrameworkElement.MinWidthProperty);
        ScheduleHScrollExtentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);

        var rowW = 0;
        foreach (var emp in employees)
        {
            ScheduleHostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddStaffCell(ScheduleHostGrid, rowW, 0, emp);
            for (var c = 0; c < dates.Count; c++)
                AddDayCell(ScheduleHostGrid, rowW, c + 1, emp, dates[c]);
            rowW++;
        }

        ScheduleHostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddTotalsLabelCell(ScheduleHostGrid, rowW, 0);
        for (var c = 0; c < dates.Count; c++)
            AddTotalsCountCell(ScheduleHostGrid, rowW, c + 1, dates[c]);

        ScheduleWeekBodyScrollViewer.ScrollToVerticalOffset(0);

        SyncScheduleScrollHostForViewMode();
        UpdateMonthPanButtons();
    }

    /// <summary>
    /// Month: outer vertical ScrollViewer + inner horizontal ScrollViewer for day columns. Week/Day: page-vertical + inner-horizontal stack.
    /// </summary>
    private void SyncScheduleScrollHostForViewMode()
    {
        if (_viewMode == OpsScheduleViewMode.Month)
        {
            if (ScheduleHScrollExtentGrid.Children.Contains(ScheduleGridChromeBorder))
                ScheduleHScrollExtentGrid.Children.Remove(ScheduleGridChromeBorder);
            ScheduleMonthInnerVerticalScroll.Content = ScheduleGridChromeBorder;

            ScheduleWeekDayHostGrid.Visibility = Visibility.Collapsed;
            ScheduleMonthHostGrid.Visibility = Visibility.Visible;
        }
        else
        {
            ScheduleMonthInnerVerticalScroll.Content = null;
            if (!ScheduleHScrollExtentGrid.Children.Contains(ScheduleGridChromeBorder))
            {
                ScheduleHScrollExtentGrid.Children.Add(ScheduleGridChromeBorder);
                Grid.SetColumn(ScheduleGridChromeBorder, 0);
            }

            ScheduleMonthHostGrid.Visibility = Visibility.Collapsed;
            ScheduleWeekDayHostGrid.Visibility = Visibility.Visible;
        }
    }

    private void AddCornerCell(Grid target, int row, int visibleCount, int totalCount)
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
            Margin = new Thickness(S(10), S(8), S(10), S(2)),
            VerticalAlignment = VerticalAlignment.Top
        };
        var searchSlot = new Border
        {
            Margin = new Thickness(S(10), 0, S(10), S(8)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = S(52)
        };
        AttachStaffFilterSearchToCornerSlot(searchSlot);

        var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
        stack.Children.Add(tb);
        stack.Children.Add(searchSlot);

        var chrome = new Border
        {
            Background = TryBrush("Brush.White", "#FFFFFF"),
            BorderBrush = TryBrush("Brush.BorderSoft", "#EEF1F5"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            CornerRadius = new CornerRadius(S(12)),
            Margin = new Thickness(S(4), S(3), S(4), S(3)),
            Child = stack,
            Effect = CloneCardShadow(),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(chrome, row);
        Grid.SetColumn(chrome, 0);
        target.Children.Add(chrome);
    }

    private void AddHeaderCell(Grid target, int row, int col, DateOnly date)
    {
        var sp = new StackPanel
        {
            Margin = new Thickness(S(6), S(8), S(6), S(8)),
            VerticalAlignment = VerticalAlignment.Center
        };
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
            Text = date.Day.ToString(CultureInfo.CurrentCulture),
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
            Effect = CloneCardShadow(),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        target.Children.Add(border);
    }

    private void AddStaffCell(Grid target, int row, int col, OpsEmployee emp)
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
        target.Children.Add(border);
    }

    private void AddDayCell(Grid target, int row, int col, OpsEmployee emp, DateOnly date)
    {
        var shifts = OpsServicesStore.GetShifts()
            .Where(s => s.EmployeeId == emp.Id && s.Date == date)
            .OrderBy(s => s.Start)
            .ToList();

        var stack = new StackPanel { Margin = new Thickness(S(6), S(6), S(6), S(6)) };
        foreach (var shift in shifts)
        {
            var accent = (Brush)new BrushConverter().ConvertFromString(emp.AccentColorHex)!;
            var cardBg = accent is SolidColorBrush sb
                ? new SolidColorBrush(Color.FromArgb(255, sb.Color.R, sb.Color.G, sb.Color.B))
                : accent;
            var border = new Border
            {
                Background = cardBg,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(S(8)),
                Padding = new Thickness(S(10), S(8), S(10), S(8)),
                Margin = new Thickness(0, 0, 0, S(6))
            };
            var white = Brushes.White;
            var iconSize = S(14);
            var startRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            startRow.Children.Add(CreateShiftClockIcon(iconSize, S(6)));
            startRow.Children.Add(new TextBlock
            {
                Text = FormatShiftStartTime(shift.Start),
                FontSize = S(14),
                FontWeight = FontWeights.SemiBold,
                Foreground = white,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            });
            var toLine = new TextBlock
            {
                Text = FormatShiftEndLine(shift.End),
                FontSize = S(13),
                FontWeight = FontWeights.Normal,
                Foreground = white,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, S(2), 0, 0)
            };
            var inner = new StackPanel();
            inner.Children.Add(startRow);
            inner.Children.Add(toLine);
            var tablesSummary = OpsServicesStore.TableNamesSummary(shift.TableIds);
            if (!string.IsNullOrWhiteSpace(tablesSummary))
            {
                inner.Children.Add(new TextBlock
                {
                    Text = tablesSummary,
                    FontSize = S(12),
                    FontWeight = FontWeights.Normal,
                    Foreground = white,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, S(2), 0, 0)
                });
            }

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
        target.Children.Add(cell);
    }

    private void AddTotalsLabelCell(Grid target, int row, int col)
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
        target.Children.Add(wrap);
    }

    private void AddTotalsCountCell(Grid target, int row, int col, DateOnly date)
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
        target.Children.Add(wrap);
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

        TxtStatStaff.Text = OpsServicesStore.GetEmployees().Count.ToString(CultureInfo.CurrentCulture);
        TxtStatShifts.Text = shiftsInRange.Count.ToString(CultureInfo.CurrentCulture);
        TxtStatHours.Text = $"{Math.Round(totalHours, 0).ToString(CultureInfo.CurrentCulture)}h";
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

    private void PillFloorPlan_Click(object sender, RoutedEventArgs e) =>
        _navigateToFloorPlan();

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
    /// <summary>Maps to <c>public.users.user_id</c>.</summary>
    public int Id { get; init; }
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
    /// <summary>Maps to <c>public.users.user_id</c> or <c>null</c> when unassigned.</summary>
    public int? AssignedWaiterId { get; set; }
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
    /// <summary>Server assigned at seating time; maps to <c>public.users.user_id</c>.</summary>
    public int? OpsServerId { get; set; }
    public DateTime? SeatedAtUtc { get; set; }
    public int? PartySize { get; set; }

    /// <summary>List card line; resolved from <see cref="AssignedWaiterId"/> (demo store).</summary>
    public string AssignedWaiterDisplay =>
        AssignedWaiterId is { } id ? OpsServicesStore.GetEmployee(id)?.Name ?? "" : "";
}

public sealed class OpsScheduledShift
{
    public Guid Id { get; init; }
    /// <summary>Maps to <c>public.users.user_id</c>.</summary>
    public int EmployeeId { get; init; }
    public DateOnly Date { get; init; }
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }
    public List<Guid> TableIds { get; init; } = new();
    public OpsShiftFrequencyKind SourceKind { get; init; }
}

/// <summary>
/// PostgreSQL-backed store for Operations and Services. Mirrors the <see cref="StaffAccessStore"/>
/// pattern: reads go through <c>App.aps.pda + App.aps.sql</c>, writes are composed via
/// <see cref="Sql"/> and executed through <c>App.aps.Execute</c>, and the in-memory lists are a
/// cache reloaded after writes or on explicit <see cref="ReloadFromDb"/>. Demo rows (is_seed=TRUE)
/// are reseeded at startup via <see cref="AppStatus.ReseedDummyDataIfEnabled"/> when
/// <c>SeedDummyDataOnStartup</c> is <c>true</c> in App.config.
/// </summary>
public static partial class OpsServicesStore
{
    public static event EventHandler? DataChanged;

    private static readonly object SyncRoot = new();
    private static readonly List<OpsFloorTable> Tables = new();
    private static readonly List<OpsScheduledShift> Shifts = new();
    /// <summary>Distinct floor names for filters, combos, and manage-floors UI (includes floors with zero tables).</summary>
    private static readonly List<string> CanonicalFloors = new();
    /// <summary>ops_floors.floor_id per canonical floor name; populated on load so renames/deletes can target the row.</summary>
    private static readonly Dictionary<string, int> FloorIdByName = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    static OpsServicesStore()
    {
        StaffAccessStore.DataChanged += (_, _) => DataChanged?.Invoke(null, EventArgs.Empty);
    }

    public static DateTime StartOfWeekMonday(DateTime d)
    {
        var date = d.Date;
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>Kept for API compatibility; triggers an initial load from the database when the cache is empty.</summary>
    public static void EnsureSeeded() => EnsureLoaded();

    /// <summary>Force a re-read from the database (e.g. after external changes). Raises <see cref="DataChanged"/>.</summary>
    public static void ReloadFromDb()
    {
        lock (SyncRoot)
        {
            _loaded = false;
            LoadFromDbLocked();
        }
        DataChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;
        lock (SyncRoot)
        {
            if (_loaded)
                return;
            LoadFromDbLocked();
        }
    }

    /// <summary>
    /// Replace the in-memory cache with a fresh snapshot of the Operations and Services tables.
    /// Safe to call re-entrantly: always resets <c>_loaded</c> on exit so a later failure to
    /// connect does not lock the store into a half-populated state. Any unexpected DB errors are
    /// logged and swallowed — the UI degrades to an empty schedule rather than crashing.
    /// </summary>
    private static void LoadFromDbLocked()
    {
        StaffAccessStore.EnsureSeeded();
        Tables.Clear();
        Shifts.Clear();
        CanonicalFloors.Clear();
        FloorIdByName.Clear();
        Reservations.Clear();
        FloorPlanLayouts.Clear();

        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            if (!PosDataAccess.CheckBranchConnection(cn))
            {
                _loaded = true;
                return;
            }

            LoadFloors(cn);
            LoadTables(cn);
            LoadShiftsWithTables(cn);
            LoadReservations(cn);
            LoadFloorPlanLayouts(cn);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] LoadFromDb failed: " + ex.Message);
        }

        _loaded = true;
    }

    private static void LoadFloors(string cn)
    {
        var dt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectAllOpsFloors(), 60);
        foreach (System.Data.DataRow r in dt.Rows)
        {
            var id = Convert.ToInt32(r["floor_id"], CultureInfo.InvariantCulture);
            var name = OpsStoreRowReader.AsString(r, "name");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var n = NormalizeFloorName(name);
            if (!CanonicalFloors.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase)))
                CanonicalFloors.Add(n);
            if (!FloorIdByName.ContainsKey(n))
                FloorIdByName[n] = id;
        }
        SortCanonicalFloors();
    }

    private static void LoadTables(string cn)
    {
        var dt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectAllOpsTables(), 60);
        foreach (System.Data.DataRow r in dt.Rows)
        {
            try
            {
                Tables.Add(OpsStoreRowReader.MapTable(r));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OpsServicesStore] MapTable failed: " + ex.Message);
            }
        }

        // Backfill canonical floor list with any floor names tables reference but that are not yet
        // in ops_floors. This keeps existing data usable after upgrades where floors were never
        // explicitly inserted (all floor logic previously lived in memory).
        foreach (var t in Tables)
        {
            var n = NormalizeFloorName(t.LocationName);
            if (!CanonicalFloors.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase)))
                CanonicalFloors.Add(n);
        }
        SortCanonicalFloors();
    }

    private static void LoadShiftsWithTables(string cn)
    {
        var shiftsDt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectAllOpsShifts(), 60);
        var linksDt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectAllOpsShiftTables(), 60);

        var linksByShift = new Dictionary<Guid, List<Guid>>();
        foreach (System.Data.DataRow r in linksDt.Rows)
        {
            var shiftId = OpsStoreRowReader.AsGuid(r, "shift_id");
            var tableId = OpsStoreRowReader.AsGuid(r, "table_id");
            if (shiftId == Guid.Empty || tableId == Guid.Empty)
                continue;
            if (!linksByShift.TryGetValue(shiftId, out var list))
            {
                list = new List<Guid>();
                linksByShift[shiftId] = list;
            }
            list.Add(tableId);
        }

        foreach (System.Data.DataRow r in shiftsDt.Rows)
        {
            try
            {
                var shift = OpsStoreRowReader.MapShift(r);
                if (linksByShift.TryGetValue(shift.Id, out var ids))
                    shift.TableIds.AddRange(ids);
                Shifts.Add(shift);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OpsServicesStore] MapShift failed: " + ex.Message);
            }
        }
    }

    public static IReadOnlyList<OpsEmployee> GetEmployees() => StaffAccessStore.GetEmployeesForOperations();

    public static IReadOnlyList<OpsFloorTable> GetTables() => Tables;

    public static OpsEmployee? GetEmployee(int id) => StaffAccessStore.GetOpsEmployee(id);

    public static OpsFloorTable? GetTable(Guid id) => Tables.FirstOrDefault(t => t.Id == id);

    public static IReadOnlyList<OpsScheduledShift> GetShifts() => Shifts;

    /// <summary>Shifts that reference this table in their assigned tables list (blocks hard delete).</summary>
    public static int GetBookedShiftCountForTable(Guid tableId) =>
        Shifts.Count(s => s.TableIds.Contains(tableId));

    /// <summary>Monday of current week through Sunday of (current week + 4 weeks) — 5 weeks.</summary>
    public static (DateOnly Start, DateOnly End) GetDefaultWeeklyRecurrenceRange(DateTime today)
    {
        var monday = DateOnly.FromDateTime(StartOfWeekMonday(today));
        var endSunday = monday.AddDays(7 * 5 - 1);
        return (monday, endSunday);
    }

    /// <summary>True if the employee already has a shift on that date overlapping [start, end).</summary>
    public static bool HasTimeConflict(int employeeId, DateOnly date, TimeOnly start, TimeOnly end,
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
        EnsureLoaded();
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

        // Persist each shift + its table links inside a single batched execute so the schedule
        // grid re-renders with everything visible; on failure nothing is added to the cache.
        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            var b = new System.Text.StringBuilder();
            foreach (var n in list)
            {
                b.Append(App.aps.sql.InsertOpsShift(
                    n.Id, n.EmployeeId, n.Date, n.Start, n.End,
                    n.SourceKind.ToString(), App.aps.signedOnUserId, isSeed: false));
                b.Append(' ');
                foreach (var tid in n.TableIds)
                {
                    b.Append(App.aps.sql.InsertOpsShiftTableLink(n.Id, tid, isSeed: false));
                    b.Append(' ');
                }
            }
            if (b.Length > 0)
                App.aps.Execute(cn, b.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] TryAddShifts persist failed: " + ex.Message);
        }

        foreach (var n in list)
            Shifts.Add(n);
        DataChanged?.Invoke(null, EventArgs.Empty);
        return null;
    }

    public static void AddTable(OpsFloorTable table)
    {
        EnsureLoaded();
        try
        {
            RegisterFloorName(table.LocationName);
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            App.aps.Execute(cn, App.aps.sql.InsertOpsTable(ToTableWrite(table), App.aps.signedOnUserId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] AddTable persist failed: " + ex.Message);
        }

        Tables.Add(table);
        DataChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Persist an in-place edit to a table (Table Management's Save button). The caller has
    /// already mutated <paramref name="table"/> in the cache; we issue the UPDATE and raise
    /// <see cref="DataChanged"/> so dependent views refresh.
    /// </summary>
    public static void SaveTable(OpsFloorTable table)
    {
        EnsureLoaded();
        try
        {
            RegisterFloorName(table.LocationName);
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            App.aps.Execute(cn, App.aps.sql.UpdateOpsTable(ToTableWrite(table), App.aps.signedOnUserId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] SaveTable persist failed: " + ex.Message);
        }

        DataChanged?.Invoke(null, EventArgs.Empty);
    }

    private static Sql.OpsTableWrite ToTableWrite(OpsFloorTable t) =>
        new()
        {
            TableId = t.Id,
            Name = t.Name,
            LocationName = NormalizeFloorName(t.LocationName),
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
            PartySize = t.PartySize,
            IsSeed = false
        };

    private static string NormalizeFloorName(string? s)
    {
        var t = (s ?? "").Trim();
        return string.IsNullOrEmpty(t) ? "Main Floor" : t;
    }

    /// <summary>Public wrapper for floor name normalization (used by floor plan and reservation dialog).</summary>
    public static string NormalizeFloorNamePublic(string? s) => NormalizeFloorName(s);

    private static void SortCanonicalFloors() =>
        CanonicalFloors.Sort(StringComparer.OrdinalIgnoreCase);

    private static void RebuildCanonicalFloorsFromTables()
    {
        CanonicalFloors.Clear();
        foreach (var t in Tables)
            RegisterFloorName(t.LocationName);
    }

    /// <summary>
    /// Registers a floor name from table data (empty becomes Main Floor). When the name is new,
    /// we also insert a row in <c>public.ops_floors</c> so renames/deletes have something to
    /// target later — the rule-of-thumb is "every name the UI can show must exist in ops_floors".
    /// </summary>
    public static void RegisterFloorName(string? locationName)
    {
        var n = NormalizeFloorName(locationName);
        if (CanonicalFloors.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase)))
            return;

        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            var nextId = QueryNextFloorId(cn);
            App.aps.Execute(cn, App.aps.sql.InsertOpsFloor(nextId, n, App.aps.signedOnUserId, isSeed: false));
            FloorIdByName[n] = nextId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] RegisterFloorName persist failed: " + ex.Message);
        }

        CanonicalFloors.Add(n);
        SortCanonicalFloors();
    }

    private static int QueryNextFloorId(string cn)
    {
        var dt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectNextOpsFloorId(), 15);
        if (dt.Rows.Count == 0)
            return 10001;
        var raw = dt.Rows[0][0];
        return raw == null || raw == DBNull.Value
            ? 10001
            : Convert.ToInt32(raw, CultureInfo.InvariantCulture);
    }

    private static void RemoveCanonicalFloorIfPresent(string name)
    {
        var n = NormalizeFloorName(name);
        for (var i = 0; i < CanonicalFloors.Count; i++)
        {
            if (!string.Equals(CanonicalFloors[i], n, StringComparison.OrdinalIgnoreCase))
                continue;
            var removed = CanonicalFloors[i];
            CanonicalFloors.RemoveAt(i);
            FloorIdByName.Remove(removed);
            return;
        }
    }

    /// <summary>Sorted floor names for filters and combos (includes zero-table floors).</summary>
    public static IReadOnlyList<string> GetDistinctFloorNamesForFilter()
    {
        foreach (var t in Tables)
            RegisterFloorName(t.LocationName);
        return CanonicalFloors.ToList();
    }

    /// <summary>Each canonical floor with live table count.</summary>
    public static IReadOnlyList<(string Name, int TableCount)> GetFloorSummaries()
    {
        foreach (var t in Tables)
            RegisterFloorName(t.LocationName);
        return CanonicalFloors
            .Select(f => (f, Tables.Count(t =>
                string.Equals(NormalizeFloorName(t.LocationName), f, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    public static bool TryAddFloor(string rawName, out string? error)
    {
        EnsureLoaded();
        error = null;
        var trimmed = (rawName ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            error = "Enter a floor name.";
            return false;
        }

        if (CanonicalFloors.Exists(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            error = "A floor with that name already exists.";
            return false;
        }

        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            var nextId = QueryNextFloorId(cn);
            App.aps.Execute(cn, App.aps.sql.InsertOpsFloor(nextId, trimmed, App.aps.signedOnUserId, isSeed: false));
            FloorIdByName[trimmed] = nextId;
        }
        catch (Exception ex)
        {
            error = "Could not save the floor: " + ex.Message;
            return false;
        }

        CanonicalFloors.Add(trimmed);
        SortCanonicalFloors();
        DataChanged?.Invoke(null, EventArgs.Empty);
        return true;
    }

    public static bool TryDeleteFloor(string name, out string? error)
    {
        EnsureLoaded();
        error = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Floor name is required.";
            return false;
        }

        // Match the canonical list entry (same key GetFloorSummaries uses per row) so counts stay aligned.
        var trimmed = name.Trim();
        var canonicalEntry = CanonicalFloors.FirstOrDefault(f =>
            string.Equals(f, trimmed, StringComparison.OrdinalIgnoreCase));
        if (canonicalEntry == null)
        {
            error = "That floor is not in the list anymore.";
            return false;
        }

        var count = Tables.Count(t =>
            string.Equals(NormalizeFloorName(t.LocationName), canonicalEntry,
                StringComparison.OrdinalIgnoreCase));
        if (count > 0)
        {
            error = $"This floor still has {count} table(s). Move or delete tables first.";
            return false;
        }

        if (FloorIdByName.TryGetValue(canonicalEntry, out var floorId))
        {
            try
            {
                var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
                App.aps.Execute(cn, App.aps.sql.SoftDeleteOpsFloor(floorId, App.aps.signedOnUserId));
            }
            catch (Exception ex)
            {
                error = "Could not delete the floor: " + ex.Message;
                return false;
            }
        }

        RemoveCanonicalFloorIfPresent(canonicalEntry);
        DataChanged?.Invoke(null, EventArgs.Empty);
        return true;
    }

    public static bool TryRenameFloor(string oldName, string newName, out string? error)
    {
        EnsureLoaded();
        error = null;
        var trimmedNew = (newName ?? "").Trim();
        if (string.IsNullOrEmpty(trimmedNew))
        {
            error = "Enter a floor name.";
            return false;
        }

        var o = NormalizeFloorName(oldName);
        if (string.Equals(o, trimmedNew, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!CanonicalFloors.Exists(x => string.Equals(x, o, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Floor not found.";
            return false;
        }

        if (CanonicalFloors.Exists(x => string.Equals(x, trimmedNew, StringComparison.OrdinalIgnoreCase)))
        {
            error = "A floor with that name already exists.";
            return false;
        }

        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            var b = new System.Text.StringBuilder();
            if (FloorIdByName.TryGetValue(o, out var floorId))
            {
                b.Append(App.aps.sql.RenameOpsFloor(floorId, trimmedNew, App.aps.signedOnUserId));
                b.Append(' ');
            }
            // Cascade the denormalized name into tables, reservations, and layouts so filters
            // and historical positions pick up the new label without a second reseed.
            b.Append(App.aps.sql.RenameFloorNameInOpsTables(o, trimmedNew, App.aps.signedOnUserId));
            b.Append(' ');
            b.Append(App.aps.sql.RenameFloorNameInOpsReservations(o, trimmedNew, App.aps.signedOnUserId));
            b.Append(' ');
            b.Append(App.aps.sql.RenameFloorNameInOpsLayouts(o, trimmedNew, App.aps.signedOnUserId));
            App.aps.Execute(cn, b.ToString());
        }
        catch (Exception ex)
        {
            error = "Could not rename the floor: " + ex.Message;
            return false;
        }

        foreach (var t in Tables)
        {
            if (string.Equals(NormalizeFloorName(t.LocationName), o, StringComparison.OrdinalIgnoreCase))
                t.LocationName = trimmedNew;
        }
        foreach (var r in Reservations)
        {
            if (string.Equals(NormalizeFloorName(r.FloorName), o, StringComparison.OrdinalIgnoreCase))
                r.FloorName = trimmedNew;
        }

        var idx = CanonicalFloors.FindIndex(x => string.Equals(x, o, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            CanonicalFloors[idx] = trimmedNew;
        if (FloorIdByName.Remove(o, out var id))
            FloorIdByName[trimmedNew] = id;
        SortCanonicalFloors();
        DataChanged?.Invoke(null, EventArgs.Empty);
        return true;
    }

    public static bool RemoveTable(Guid id)
    {
        EnsureLoaded();
        var idx = Tables.FindIndex(t => t.Id == id);
        if (idx < 0)
            return false;

        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            App.aps.Execute(cn, App.aps.sql.SoftDeleteOpsTable(id, App.aps.signedOnUserId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[OpsServicesStore] RemoveTable persist failed: " + ex.Message);
        }

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

    public static double TotalHoursForEmployeeInRange(int employeeId, DateOnly start, DateOnly end)
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

/// <summary>
/// Row-to-model mappers for the Operations and Services store. Lives alongside
/// <see cref="OpsServicesStore"/> because it is the only consumer; isolated in its own static
/// type so the partial-class file split stays readable. Mirrors the driver-tolerant patterns in
/// <c>StaffAccessStore</c> (string forms for booleans, missing-column tolerance for forwards
/// compatibility with older DBs).
/// </summary>
internal static class OpsStoreRowReader
{
    public static string AsString(System.Data.DataRow r, string column)
    {
        if (!r.Table.Columns.Contains(column))
            return "";
        var v = r[column];
        return v == DBNull.Value || v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
    }

    public static int AsInt(System.Data.DataRow r, string column, int fallback)
    {
        if (!r.Table.Columns.Contains(column))
            return fallback;
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return fallback;
        if (v is int i)
            return i;
        return int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    public static int? AsNullableInt(System.Data.DataRow r, string column)
    {
        if (!r.Table.Columns.Contains(column))
            return null;
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return null;
        if (v is int i)
            return i;
        return int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var parsed) ? parsed : (int?)null;
    }

    public static double AsDouble(System.Data.DataRow r, string column, double fallback)
    {
        if (!r.Table.Columns.Contains(column))
            return fallback;
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return fallback;
        return double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    /// <summary>
    /// Tolerant boolean reader: PostgreSQL's ODBC driver emits "0"/"1", "t"/"f", "true"/"false",
    /// or real .NET bool depending on version. Same pattern as <c>StaffAccessStore.AsBool</c>.
    /// </summary>
    public static bool AsBool(System.Data.DataRow r, string column, bool fallback)
    {
        if (!r.Table.Columns.Contains(column))
            return fallback;
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return fallback;
        if (v is bool b)
            return b;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (s.Length == 0)
            return fallback;
        if (bool.TryParse(s, out var parsed))
            return parsed;
        if (s == "1" || s.Equals("t", StringComparison.OrdinalIgnoreCase)
                     || s.Equals("y", StringComparison.OrdinalIgnoreCase))
            return true;
        if (s == "0" || s.Equals("f", StringComparison.OrdinalIgnoreCase)
                     || s.Equals("n", StringComparison.OrdinalIgnoreCase))
            return false;
        return fallback;
    }

    public static DateTime AsUtc(System.Data.DataRow r, string column, DateTime fallback)
    {
        if (!r.Table.Columns.Contains(column))
            return fallback;
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return fallback;
        var dt = Convert.ToDateTime(v, CultureInfo.InvariantCulture);
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    public static DateTime? AsNullableUtc(System.Data.DataRow r, string column)
    {
        if (!r.Table.Columns.Contains(column))
            return null;
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return null;
        var dt = Convert.ToDateTime(v, CultureInfo.InvariantCulture);
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    public static DateOnly AsDate(System.Data.DataRow r, string column)
    {
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return DateOnly.FromDateTime(DateTime.Today);
        if (v is DateTime dt)
            return DateOnly.FromDateTime(dt);
        return DateOnly.Parse(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
            CultureInfo.InvariantCulture);
    }

    public static TimeOnly AsTime(System.Data.DataRow r, string column)
    {
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return new TimeOnly(0, 0);
        if (v is TimeSpan ts)
            return TimeOnly.FromTimeSpan(ts);
        if (v is DateTime dt)
            return TimeOnly.FromDateTime(dt);
        var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        return TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : new TimeOnly(0, 0);
    }

    public static Guid AsGuid(System.Data.DataRow r, string column)
    {
        if (!r.Table.Columns.Contains(column))
            return Guid.Empty;
        var v = r[column];
        if (v == DBNull.Value || v == null)
            return Guid.Empty;
        if (v is Guid g)
            return g;
        return Guid.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out var parsed)
            ? parsed
            : Guid.Empty;
    }

    public static OpsFloorTable MapTable(System.Data.DataRow r) =>
        new()
        {
            Id = AsGuid(r, "table_id"),
            Name = AsString(r, "name"),
            LocationName = AsString(r, "location_name"),
            SeatCount = AsInt(r, "seat_count", 4),
            Shape = AsString(r, "shape"),
            IsActive = AsBool(r, "is_active", true),
            AssignedWaiterId = AsNullableInt(r, "assigned_waiter_id"),
            Zone = AsInt(r, "zone", 1),
            Station = AsInt(r, "station", 1),
            TurnTimeMinutes = AsInt(r, "turn_time_minutes", 60),
            Status = AsString(r, "status"),
            Notes = AsString(r, "notes"),
            Accessible = AsBool(r, "accessible", false),
            VipPriority = AsBool(r, "vip_priority", false),
            CanMerge = AsBool(r, "can_merge", true),
            CreatedUtc = AsUtc(r, "created_ts", DateTime.UtcNow),
            ModifiedUtc = AsUtc(r, "modified_ts", DateTime.UtcNow),
            OpsStatus = AsString(r, "ops_status"),
            OpsServerId = AsNullableInt(r, "ops_server_id"),
            SeatedAtUtc = AsNullableUtc(r, "seated_at_ts"),
            PartySize = AsNullableInt(r, "party_size")
        };

    public static OpsScheduledShift MapShift(System.Data.DataRow r)
    {
        var kindRaw = AsString(r, "source_kind");
        var kind = Enum.TryParse<OpsShiftFrequencyKind>(kindRaw, ignoreCase: true, out var parsed)
            ? parsed
            : OpsShiftFrequencyKind.Daily;
        return new OpsScheduledShift
        {
            Id = AsGuid(r, "shift_id"),
            EmployeeId = AsInt(r, "employee_id", 0),
            Date = AsDate(r, "shift_date"),
            Start = AsTime(r, "start_time"),
            End = AsTime(r, "end_time"),
            SourceKind = kind
        };
    }

    public static OpsReservation MapReservation(System.Data.DataRow r)
    {
        var statusRaw = AsString(r, "status");
        var status = Enum.TryParse<OpsReservationStatus>(statusRaw, ignoreCase: true, out var parsed)
            ? parsed
            : OpsReservationStatus.Pending;
        return new OpsReservation
        {
            Id = AsGuid(r, "reservation_id"),
            TableId = AsGuid(r, "table_id"),
            FloorName = AsString(r, "floor_name"),
            Date = AsDate(r, "res_date"),
            CustomerName = AsString(r, "customer_name"),
            Phone = AsString(r, "phone"),
            Email = AsString(r, "email") is { Length: > 0 } eNonEmpty ? eNonEmpty : null,
            PartySize = AsInt(r, "party_size", 2),
            Time = AsTime(r, "res_time"),
            Status = status,
            Notes = AsString(r, "notes"),
            Reference = AsString(r, "reference")
        };
    }
}
