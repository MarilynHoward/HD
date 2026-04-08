using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

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
    private AddShiftFreq _freq = AddShiftFreq.Daily;

    public OpsServicesAddShift(Action closeDialog)
    {
        _closeDialog = closeDialog ?? throw new ArgumentNullException(nameof(closeDialog));
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        OpsServicesStore.EnsureSeeded();
        CmbEmployee.ItemsSource = OpsServicesStore.GetEmployees().OrderBy(x => x.Name).ToList();
        if (CmbEmployee.Items.Count > 0)
            CmbEmployee.SelectedIndex = 0;

        CalDaily.SelectedDate = DateTime.Today;
        CalMonthly.SelectionMode = CalendarSelectionMode.MultipleRange;
        CalMonthly.SelectedDates.Clear();
        CalMonthly.DisplayDate = DateTime.Today;

        UpdateDailySelectedLabel();
        RefreshWeeklyRangeLabel();
        UpdateWeeklyDaysSummary();
        RebuildTablePicker();
        UpdateTableCountBadge();
        SetFrequency(AddShiftFreq.Daily);
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
    }

    private static void HighlightFreqButton(Button b, bool on)
    {
        if (on)
        {
            b.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF")!);
            b.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D5FEF")!);
            b.BorderThickness = new Thickness(1);
            b.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            b.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            b.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
            b.BorderThickness = new Thickness(1);
            b.FontWeight = FontWeights.Normal;
        }
    }

    private void BtnFreqDaily_Click(object sender, RoutedEventArgs e) => SetFrequency(AddShiftFreq.Daily);

    private void BtnFreqWeekly_Click(object sender, RoutedEventArgs e) => SetFrequency(AddShiftFreq.Weekly);

    private void BtnFreqMonthly_Click(object sender, RoutedEventArgs e) => SetFrequency(AddShiftFreq.Monthly);

    private void CalDaily_OnSelectedDatesChanged(object sender, SelectionChangedEventArgs e) => UpdateDailySelectedLabel();

    private void UpdateDailySelectedLabel()
    {
        if (CalDaily.SelectedDate is { } dt)
        {
            TxtDailySelected.Text = $"Selected: {dt:dddd, MMMM d, yyyy}";
        }
        else
        {
            TxtDailySelected.Text = "Select a date on the calendar.";
        }
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

    private void CalMonthly_OnSelectedDatesChanged(object sender, SelectionChangedEventArgs e) => RebuildMonthlyChips();

    private void RebuildMonthlyChips()
    {
        MonthlyChipsPanel.Children.Clear();
        if (CalMonthly.SelectedDates.Count == 0)
            return;

        foreach (var dt in CalMonthly.SelectedDates.Cast<DateTime>().OrderBy(d => d.Date))
        {
            var chip = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")!),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")!),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(S(12)),
                Padding = new Thickness(S(10), S(4), S(10), S(4)),
                Margin = new Thickness(0, 0, S(6), S(6)),
                Child = new TextBlock
                {
                    Text = dt.ToString("MMM d", CultureInfo.CurrentCulture),
                    FontSize = S(12),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")!)
                }
            };
            MonthlyChipsPanel.Children.Add(chip);
        }
    }

    private void RebuildTablePicker()
    {
        TablePickHost.Children.Clear();
        foreach (var grp in OpsServicesStore.GetTables().GroupBy(t => t.LocationName).OrderBy(g => g.Key))
        {
            var header = new TextBlock
            {
                Text = "🍴 " + grp.Key,
                FontWeight = FontWeights.SemiBold,
                FontSize = S(14),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")!),
                Margin = new Thickness(0, S(10), 0, S(6))
            };
            TablePickHost.Children.Add(header);

            var grid = new UniformGrid { Columns = 2 };
            foreach (var t in grp.OrderBy(x => x.Name))
            {
                var card = new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(S(8)),
                    Margin = new Thickness(S(4)),
                    Padding = new Thickness(S(8)),
                    Tag = t.Id
                };
                ApplyTableCardStyle(card, _selectedTableIds.Contains(t.Id));

                var cb = new System.Windows.Controls.CheckBox
                {
                    IsChecked = _selectedTableIds.Contains(t.Id),
                    Tag = t.Id,
                    FocusVisualStyle = null
                };
                var label = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = S(13)
                };
                label.Inlines.Add(new System.Windows.Documents.Run(t.Name) { FontWeight = FontWeights.SemiBold });
                label.Inlines.Add(new System.Windows.Documents.Run($"  ·  {t.SeatCount} seats")
                {
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")!)
                });
                cb.Content = label;
                cb.Checked += TableCheckChanged;
                cb.Unchecked += TableCheckChanged;

                card.Child = cb;
                grid.Children.Add(card);
            }

            TablePickHost.Children.Add(grid);
        }
    }

    private static void ApplyTableCardStyle(Border card, bool selected)
    {
        if (selected)
        {
            card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")!);
            card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF")!);
        }
        else
        {
            card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")!);
            card.Background = Brushes.White;
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

        if (cb.Parent is Border b)
            ApplyTableCardStyle(b, _selectedTableIds.Contains(id));

        UpdateTableCountBadge();
    }

    private void UpdateTableCountBadge()
    {
        var n = _selectedTableIds.Count;
        TxtTableCount.Text = n == 1 ? "1 selected" : $"{n} selected";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _closeDialog();

    private void AddShift_Click(object sender, RoutedEventArgs e)
    {
        TxtFormError.Visibility = Visibility.Collapsed;

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
            ShowError("End time must be after start time.");
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
                if (CalDaily.SelectedDate is not { } d0)
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
                if (CalMonthly.SelectedDates.Count == 0)
                {
                    ShowError("Select one or more dates on the calendar.");
                    return;
                }

                foreach (var dt in CalMonthly.SelectedDates.Cast<DateTime>())
                    batch.Add(CreateShift(emp.Id, DateOnly.FromDateTime(dt), startT, endT, tableIds, kind));
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
        TxtFormError.Text = msg;
        TxtFormError.Visibility = Visibility.Visible;
    }
}
