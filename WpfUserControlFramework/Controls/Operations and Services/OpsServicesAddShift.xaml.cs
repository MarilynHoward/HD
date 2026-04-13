using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ShapesPath = System.Windows.Shapes.Path;

namespace RestaurantPosWpf;

public partial class OpsServicesAddShift : UserControl
{
    private enum AddShiftFreq
    {
        Daily,
        Weekly,
        Monthly
    }

    private readonly Action _closeDialog;
    private readonly HashSet<Guid> _selectedTableIds = new();
    private readonly HashSet<DayOfWeek> _selectedDaysOfWeek = new();
    /// <summary>Monthly mode: canonical selection (calendar may drop cross-month highlights when DisplayDate changes).</summary>
    private readonly HashSet<DateOnly> _monthlyPickedDates = new();
    private bool _monthlyCalendarSyncing;
    private AddShiftFreq _freq = AddShiftFreq.Daily;

    public OpsServicesAddShift(Action closeDialog)
    {
        _closeDialog = closeDialog ?? throw new ArgumentNullException(nameof(closeDialog));
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void TimeFields_TextChanged(object sender, TextChangedEventArgs e) => UpdateTimeRangeError();

    private void UpdateTimeRangeError()
    {
        TxtTimeRangeError.Visibility = Visibility.Collapsed;
        if (!TryParseTime(TxtStartTime.Text, out var startT) || !TryParseTime(TxtEndTime.Text, out var endT))
        {
            RefreshAddShiftAvailability();
            return;
        }

        if (endT <= startT)
            TxtTimeRangeError.Visibility = Visibility.Visible;
        RefreshAddShiftAvailability();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        OpsServicesStore.EnsureSeeded();
        CmbEmployee.ItemsSource = OpsServicesStore.GetEmployees().OrderBy(x => x.Name).ToList();
        if (CmbEmployee.Items.Count > 0)
            CmbEmployee.SelectedIndex = 0;

        DpDaily.SelectedDate = DateTime.Today;
        CalMonthly.SelectionMode = CalendarSelectionMode.MultipleRange;
        _monthlyPickedDates.Clear();
        CalMonthly.SelectedDates.Clear();
        CalMonthly.DisplayDate = DateTime.Today;

        UpdateDailySelectedLabel();
        RefreshWeeklyRangeLabel();
        UpdateWeeklyDaysSummary();
        RebuildTablePicker();
        UpdateTableCountBadge();
        SetFrequency(AddShiftFreq.Daily);

        TxtStartTime.TextChanged += TimeFields_TextChanged;
        TxtEndTime.TextChanged += TimeFields_TextChanged;
        UpdateTimeRangeError();
        RefreshAddShiftAvailability();
    }

    private void CmbEmployee_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshAddShiftAvailability();

    /// <summary>Enables + Add Shift only when required fields and business rules pass (matches AddShift_Click guards).</summary>
    private void RefreshAddShiftAvailability()
    {
        BtnAddShift.IsEnabled = CanSubmitAddShift();
    }

    private bool CanSubmitAddShift()
    {
        if (CmbEmployee.SelectedItem is not OpsEmployee)
            return false;
        if (!TryParseTime(TxtStartTime.Text, out var startT) || !TryParseTime(TxtEndTime.Text, out var endT))
            return false;
        if (endT <= startT)
            return false;
        if (TxtTimeRangeError.Visibility == Visibility.Visible)
            return false;
        if (_selectedTableIds.Count == 0)
            return false;

        return _freq switch
        {
            AddShiftFreq.Daily => DpDaily.SelectedDate != null,
            AddShiftFreq.Weekly => _selectedDaysOfWeek.Count > 0,
            AddShiftFreq.Monthly => _monthlyPickedDates.Count > 0,
            _ => false
        };
    }

    private double S(double basePx)
    {
        var state = Application.Current.Resources["UiScaleState"] as UiScaleState;
        return basePx * (state?.FontScale ?? 1.25);
    }

    private void SetFrequency(AddShiftFreq f)
    {
        _freq = f;
        PanelDaily.Visibility = f == AddShiftFreq.Daily ? Visibility.Visible : Visibility.Collapsed;
        PanelWeekly.Visibility = f == AddShiftFreq.Weekly ? Visibility.Visible : Visibility.Collapsed;
        PanelMonthly.Visibility = f == AddShiftFreq.Monthly ? Visibility.Visible : Visibility.Collapsed;

        TxtTableSubtitle.Text = f switch
        {
            AddShiftFreq.Weekly => "Tables for all recurring days",
            AddShiftFreq.Monthly => "Tables for all selected dates",
            _ => "Select which tables this employee will be responsible for"
        };

        HighlightFreqButton(BtnFreqDaily, f == AddShiftFreq.Daily);
        HighlightFreqButton(BtnFreqWeekly, f == AddShiftFreq.Weekly);
        HighlightFreqButton(BtnFreqMonthly, f == AddShiftFreq.Monthly);
        RefreshAddShiftAvailability();
    }

    /// <summary>Selected segment: white pill on shared gray track; unselected: transparent on track with muted label/icon.</summary>
    private void HighlightFreqButton(Button b, bool on)
    {
        if (on)
        {
            b.Background = TryThemeBrush("Brush.White", "#FFFFFF");
            b.BorderBrush = TryThemeBrush("Brush.BorderSoft", "#EEF1F5");
            b.BorderThickness = new Thickness(1);
            b.FontWeight = FontWeights.SemiBold;
            b.Foreground = TryThemeBrush("MainForeground", "#111827");
        }
        else
        {
            b.Background = Brushes.Transparent;
            b.BorderBrush = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            b.FontWeight = FontWeights.Normal;
            b.Foreground = TryThemeBrush("DimmedForeground", "#687280");
        }
    }

    private void BtnFreqDaily_Click(object sender, RoutedEventArgs e) => SetFrequency(AddShiftFreq.Daily);

    private void BtnFreqWeekly_Click(object sender, RoutedEventArgs e) => SetFrequency(AddShiftFreq.Weekly);

    private void BtnFreqMonthly_Click(object sender, RoutedEventArgs e) => SetFrequency(AddShiftFreq.Monthly);

    private void DpDaily_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateDailySelectedLabel();
        RefreshAddShiftAvailability();
    }

    private void UpdateDailySelectedLabel()
    {
        if (DpDaily.SelectedDate is { } dt)
            TxtDailySelected.Text = $"{dt:dddd, MMMM d, yyyy}";
        else
            TxtDailySelected.Text = "Choose a date.";
    }

    private void DayToggle_Click(object sender, RoutedEventArgs e)
    {
        _selectedDaysOfWeek.Clear();
        foreach (var (btn, dow) in DayToggles())
        {
            if (btn.IsChecked == true)
                _selectedDaysOfWeek.Add(dow);
        }

        UpdateWeeklyDaysSummary();
        RefreshAddShiftAvailability();
    }

    private IEnumerable<(ToggleButton Btn, DayOfWeek Dow)> DayToggles()
    {
        yield return (TogMon, DayOfWeek.Monday);
        yield return (TogTue, DayOfWeek.Tuesday);
        yield return (TogWed, DayOfWeek.Wednesday);
        yield return (TogThu, DayOfWeek.Thursday);
        yield return (TogFri, DayOfWeek.Friday);
        yield return (TogSat, DayOfWeek.Saturday);
        yield return (TogSun, DayOfWeek.Sunday);
    }

    private void UpdateWeeklyDaysSummary()
    {
        if (_selectedDaysOfWeek.Count == 0)
        {
            TxtWeeklyDaysSummary.Text = "No days selected.";
            return;
        }

        var ordered = _selectedDaysOfWeek.OrderBy(d => (d - DayOfWeek.Monday + 7) % 7).ToList();
        var names = ordered.Select(d => d switch
        {
            DayOfWeek.Monday => "Mon",
            DayOfWeek.Tuesday => "Tue",
            DayOfWeek.Wednesday => "Wed",
            DayOfWeek.Thursday => "Thu",
            DayOfWeek.Friday => "Fri",
            DayOfWeek.Saturday => "Sat",
            _ => "Sun"
        });
        TxtWeeklyDaysSummary.Text =
            $"Selected {_selectedDaysOfWeek.Count} day(s): {string.Join(", ", names)}";
    }

    private void RefreshWeeklyRangeLabel()
    {
        var (start, end) = OpsServicesStore.GetDefaultWeeklyRecurrenceRange(DateTime.Today);
        TxtWeeklyRange.Text =
            $"Repeats from {start:MMM d, yyyy} through {end:MMM d, yyyy} (current week plus four additional weeks).";
    }

    private void CalMonthly_OnSelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_monthlyCalendarSyncing)
            return;
        if (MonthlyCalendarMatchesPickedDates())
        {
            RefreshAddShiftAvailability();
            return;
        }

        ApplyMonthlyPickedDatesToCalendar();
        RebuildMonthlyChips();
    }

    private void CalMonthly_DisplayDateChanged(object? sender, CalendarDateChangedEventArgs e)
    {
        if (_monthlyCalendarSyncing)
            return;
        ApplyMonthlyPickedDatesToCalendar();
        RefreshAddShiftAvailability();
    }

    /// <summary>
    /// Monthly mode: each day click toggles selection in <see cref="_monthlyPickedDates"/> then re-applies to the calendar.
    /// </summary>
    private void CalMonthly_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Calendar cal)
            return;

        var dayBtn = FindAncestor<CalendarDayButton>(e.OriginalSource as DependencyObject);
        if (dayBtn == null)
            return;

        if (!TryGetCalendarDayDate(dayBtn, out var clicked))
            return;

        if (dayBtn.IsBlackedOut)
            return;

        // Ignore grey "other month" cells so selection matches visible month intent
        if (dayBtn.IsInactive)
            return;

        clicked = clicked.Date;
        var day = DateOnly.FromDateTime(clicked);
        if (!_monthlyPickedDates.Add(day))
            _monthlyPickedDates.Remove(day);

        ApplyMonthlyPickedDatesToCalendar();
        RebuildMonthlyChips();
        e.Handled = true;
    }

    private bool MonthlyCalendarMatchesPickedDates()
    {
        if (CalMonthly.SelectedDates.Count != _monthlyPickedDates.Count)
            return false;
        foreach (var d in _monthlyPickedDates)
        {
            if (!CalMonthly.SelectedDates.Contains(d.ToDateTime(TimeOnly.MinValue)))
                return false;
        }

        return true;
    }

    private void ApplyMonthlyPickedDatesToCalendar()
    {
        _monthlyCalendarSyncing = true;
        try
        {
            CalMonthly.SelectedDates.Clear();
            foreach (var d in _monthlyPickedDates.OrderBy(x => x))
                CalMonthly.SelectedDates.Add(d.ToDateTime(TimeOnly.MinValue));
        }
        finally
        {
            _monthlyCalendarSyncing = false;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T match)
                return match;
            d = VisualTreeHelper.GetParent(d);
        }

        return null;
    }

    private static bool TryGetCalendarDayDate(CalendarDayButton dayBtn, out DateTime date)
    {
        if (dayBtn.DataContext is DateTime dt)
        {
            date = dt;
            return true;
        }

        if (dayBtn.DataContext != null &&
            DateTime.TryParse(dayBtn.DataContext.ToString(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }

        date = default;
        return false;
    }

    private void RebuildMonthlyChips()
    {
        MonthlyChipsPanel.Children.Clear();
        if (_monthlyPickedDates.Count == 0)
            return;

        foreach (var day in _monthlyPickedDates.OrderBy(d => d))
        {
            var label = new TextBlock
            {
                Text = day.ToString("MMM d, yyyy", CultureInfo.CurrentCulture),
                FontSize = S(13),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")!)
            };
            var removeBtn = new Button
            {
                Content = "\u00D7",
                Tag = day,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(S(4), S(0), S(2), S(0)),
                Margin = new Thickness(S(4), 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = S(14),
                Foreground = TryThemeBrush("DimmedForeground", "#687280"),
                FocusVisualStyle = null
            };
            removeBtn.Click += MonthlyChipRemove_Click;

            var row = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { label, removeBtn }
            };

            var chip = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")!),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")!),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(S(12)),
                Padding = new Thickness(S(8), S(4), S(6), S(4)),
                Margin = new Thickness(0, 0, S(6), S(6)),
                Child = row
            };
            MonthlyChipsPanel.Children.Add(chip);
        }

        RefreshAddShiftAvailability();
    }

    private void MonthlyChipRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DateOnly day)
            return;
        _monthlyPickedDates.Remove(day);
        ApplyMonthlyPickedDatesToCalendar();
        RebuildMonthlyChips();
    }

    private void RebuildTablePicker()
    {
        TablePickHost.Children.Clear();
        foreach (var grp in OpsServicesStore.GetTables().GroupBy(t => t.LocationName).OrderBy(g => g.Key))
        {
            TablePickHost.Children.Add(CreateFloorLocationPill(grp.Key));

            var grid = new UniformGrid { Columns = 2 };
            foreach (var t in grp.OrderBy(x => x.Name))
            {
                var cb = new System.Windows.Controls.CheckBox
                {
                    IsChecked = _selectedTableIds.Contains(t.Id),
                    Tag = t.Id,
                    FocusVisualStyle = null,
                    Margin = new Thickness(S(4)),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                if (Application.Current.TryFindResource("IcFieldCheckBoxStyle") is System.Windows.Style fieldStyle)
                    cb.Style = fieldStyle;

                var nameBrush = TryThemeBrush("MainForeground", "#111827");
                var dimBrush = TryThemeBrush("DimmedForeground", "#687280");
                var textCol = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center
                };
                textCol.Children.Add(new TextBlock
                {
                    Text = t.Name,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = S(14),
                    Foreground = nameBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                });
                textCol.Children.Add(new TextBlock
                {
                    Text = $"{t.SeatCount} seats",
                    FontWeight = FontWeights.Normal,
                    FontSize = S(13),
                    Foreground = dimBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                });
                cb.Content = textCol;
                cb.Checked += TableCheckChanged;
                cb.Unchecked += TableCheckChanged;

                ApplyTableTileSelectionStyle(cb, _selectedTableIds.Contains(t.Id));
                grid.Children.Add(cb);
            }

            TablePickHost.Children.Add(grid);
        }
    }

    /// <summary>Client layout: pill + same fork/knife icon as Shift Scheduling → Table Management nav.</summary>
    private Border CreateFloorLocationPill(string locationName)
    {
        var row = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        var icon = CreateTableManagementNavForkKnifeIcon(S(14));
        icon.Margin = new Thickness(0, 0, S(6), 0);
        row.Children.Add(icon);
        row.Children.Add(new TextBlock
        {
            Text = locationName,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            FontSize = S(13),
            Foreground = TryThemeBrush("MainForeground", "#111827")
        });

        return new Border
        {
            Background = TryThemeBrush("Brush.SurfaceGreySoft", "#F3F4F6"),
            BorderBrush = TryThemeBrush("Brush.BorderSoft", "#E5E7EB"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(S(18)),
            Padding = new Thickness(S(8), S(4), S(10), S(4)),
            Margin = new Thickness(0, S(8), 0, S(6)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = row
        };
    }

    /// <summary>
    /// Same 24×24 fork + knife paths as the Table Management pill in OpsServicesShiftScheduling.xaml
    /// (OpsLucideNavIconPathStyle geometry); stroke uses MainForeground like the nav artwork.
    /// </summary>
    private static Viewbox CreateTableManagementNavForkKnifeIcon(double viewport)
    {
        var stroke = TryThemeBrush("MainForeground", "#111827");
        var canvas = new Canvas { Width = 24, Height = 24, SnapsToDevicePixels = true };
        foreach (var d in TableManagementNavForkKnifePathDatas)
        {
            canvas.Children.Add(new ShapesPath
            {
                Data = Geometry.Parse(d),
                Stroke = stroke,
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            });
        }

        return new Viewbox { Width = viewport, Height = viewport, Child = canvas };
    }

    /// <summary>Must stay in sync with PillTableManagement icon in OpsServicesShiftScheduling.xaml.</summary>
    private static readonly string[] TableManagementNavForkKnifePathDatas =
    {
        "M3,2 L3,9 C3,10.1045695 3.8954305,11 5,11 L9,11 A2,2 0 0 0 11,9 L11,2",
        "M7,2 L7,22",
        "M21,15 L21,2 A5,5 0 0 0 16,7 L16,13 C16,14.1045695 16.8954305,15 18,15 L21,15 Z M21,15 L21,22"
    };

    private static Brush TryThemeBrush(string key, string fallbackHex) =>
        Application.Current.TryFindResource(key) as Brush
        ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex)!);

    /// <summary>Blue selected tile vs neutral; pairs with the shared IcFieldCheckBoxStyle tile chrome.</summary>
    private static void ApplyTableTileSelectionStyle(System.Windows.Controls.CheckBox tile, bool selected)
    {
        if (selected)
        {
            tile.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")!);
            tile.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF")!);
        }
        else
        {
            tile.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")!);
            tile.Background = Brushes.White;
        }
    }

    private void TableCheckChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox cb || cb.Tag is not Guid id)
            return;

        if (cb.IsChecked == true)
            _selectedTableIds.Add(id);
        else
            _selectedTableIds.Remove(id);

        ApplyTableTileSelectionStyle(cb, _selectedTableIds.Contains(id));

        UpdateTableCountBadge();
        RefreshAddShiftAvailability();
    }

    private void UpdateTableCountBadge()
    {
        var n = _selectedTableIds.Count;
        TxtTableCount.Text = n == 1 ? "1 selected" : $"{n} selected";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _closeDialog();

    private void AddShift_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSubmitAddShift())
            return;

        TxtFormError.Visibility = Visibility.Collapsed;
        UpdateTimeRangeError();

        if (CmbEmployee.SelectedItem is not OpsEmployee emp)
        {
            ShowError("Please select an employee.");
            return;
        }

        if (!TryParseTime(TxtStartTime.Text, out var startT) || !TryParseTime(TxtEndTime.Text, out var endT))
        {
            ShowError("Enter valid start and end times (use 24-hour format, e.g. 09:00 and 17:00).");
            return;
        }

        if (endT <= startT)
        {
            TxtTimeRangeError.Visibility = Visibility.Visible;
            return;
        }

        if (_selectedTableIds.Count == 0)
        {
            ShowError("Select at least one table.");
            return;
        }

        var tableIds = _selectedTableIds.ToList();
        List<OpsScheduledShift> batch = new();
        var kind = _freq switch
        {
            AddShiftFreq.Weekly => OpsShiftFrequencyKind.Weekly,
            AddShiftFreq.Monthly => OpsShiftFrequencyKind.Monthly,
            _ => OpsShiftFrequencyKind.Daily
        };

        switch (_freq)
        {
            case AddShiftFreq.Daily:
                if (DpDaily.SelectedDate is not { } d0)
                {
                    ShowError("Select a date.");
                    return;
                }

                batch.Add(CreateShift(emp.Id, DateOnly.FromDateTime(d0), startT, endT, tableIds, kind));
                break;

            case AddShiftFreq.Weekly:
                if (_selectedDaysOfWeek.Count == 0)
                {
                    ShowError("Select at least one day of the week.");
                    return;
                }

                foreach (var date in WeeklyOccurrenceDates())
                    batch.Add(CreateShift(emp.Id, date, startT, endT, tableIds, kind));
                break;

            case AddShiftFreq.Monthly:
                if (_monthlyPickedDates.Count == 0)
                {
                    ShowError("Select one or more dates on the calendar.");
                    return;
                }

                foreach (var d in _monthlyPickedDates.OrderBy(x => x))
                    batch.Add(CreateShift(emp.Id, d, startT, endT, tableIds, kind));
                break;
        }

        var err = OpsServicesStore.TryAddShifts(batch);
        if (err != null)
        {
            ShowError(err);
            return;
        }

        _closeDialog();
    }

    private static OpsScheduledShift CreateShift(Guid employeeId, DateOnly date, TimeOnly start, TimeOnly end,
        List<Guid> tableIds, OpsShiftFrequencyKind kind) =>
        new()
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            Date = date,
            Start = start,
            End = end,
            TableIds = tableIds.ToList(),
            SourceKind = kind
        };

    private IEnumerable<DateOnly> WeeklyOccurrenceDates()
    {
        var (start, end) = OpsServicesStore.GetDefaultWeeklyRecurrenceRange(DateTime.Today);
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (_selectedDaysOfWeek.Contains(d.DayOfWeek))
                yield return d;
        }
    }

    private static bool TryParseTime(string text, out TimeOnly t)
    {
        var s = text.Trim();
        if (TimeOnly.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out t))
            return true;
        return TimeOnly.TryParseExact(s, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out t);
    }

    private void ShowError(string msg)
    {
        TxtTimeRangeError.Visibility = Visibility.Collapsed;
        TxtFormError.Text = msg;
        TxtFormError.Visibility = Visibility.Visible;
        RefreshAddShiftAvailability();
    }
}
