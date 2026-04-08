using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

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
    private DateTime _anchorDate = DateTime.Today;
    private OpsScheduleViewMode _viewMode = OpsScheduleViewMode.Week;

    public OpsServicesShiftScheduling(
        Action navigateToTableManagement,
        Action openAddShiftDialog)
    {
        _navigateToTableManagement = navigateToTableManagement ?? throw new ArgumentNullException(nameof(navigateToTableManagement));
        _openAddShiftDialog = openAddShiftDialog ?? throw new ArgumentNullException(nameof(openAddShiftDialog));
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
    }

    private void OnStoreChanged(object? sender, EventArgs e) => Dispatcher.Invoke(RebuildAll);

    private void HighlightShiftPill(bool selected)
    {
        if (selected)
        {
            PillShiftScheduling.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF")!);
            PillShiftScheduling.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D5FEF")!);
            PillShiftScheduling.BorderThickness = new Thickness(1);
            PillShiftScheduling.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            PillShiftScheduling.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
            PillShiftScheduling.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            PillShiftScheduling.BorderThickness = new Thickness(1);
            PillShiftScheduling.FontWeight = FontWeights.Normal;
        }
    }

    private double S(double basePx)
    {
        var state = Application.Current.Resources["UiScaleState"] as UiScaleState;
        return basePx * (state?.FontScale ?? 1.25);
    }

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

        var employees = OpsServicesStore.GetEmployees().OrderBy(e => e.Name).ToList();
        double staffCol = S(200);
        ScheduleHostGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(staffCol) });
        foreach (var _ in dates)
        {
            ScheduleHostGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = _viewMode == OpsScheduleViewMode.Month ? S(76) : S(110)
            });
        }

        var minGridWidth = staffCol + dates.Count * (_viewMode == OpsScheduleViewMode.Month ? S(76) : S(110));
        ScheduleHostGrid.MinWidth = minGridWidth;
        ScheduleHorizontalScroll.HorizontalScrollBarVisibility = _viewMode == OpsScheduleViewMode.Month
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;

        int row = 0;
        ScheduleHostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddCornerCell(row);
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

    private void AddCornerCell(int row)
    {
        var tb = new TextBlock
        {
            Text = $"Staff ({OpsServicesStore.GetEmployees().Count})",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")!),
            FontSize = S(14),
            Margin = new Thickness(S(8), S(6), S(8), S(6)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, 0);
        ScheduleHostGrid.Children.Add(tb);
    }

    private void AddHeaderCell(int row, int col, DateOnly date)
    {
        var sp = new StackPanel { Margin = new Thickness(S(4)) };
        sp.Children.Add(new TextBlock
        {
            Text = date.ToString("ddd", CultureInfo.CurrentCulture),
            FontWeight = FontWeights.SemiBold,
            FontSize = S(13),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")!),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        sp.Children.Add(new TextBlock
        {
            Text = date.ToString("d MMM", CultureInfo.CurrentCulture),
            FontSize = S(12),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")!),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        var border = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")!),
            CornerRadius = new CornerRadius(S(6)),
            Margin = new Thickness(S(2)),
            Child = sp
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
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")!),
            FontSize = S(12)
        };
        var datesForHours = GetVisibleDates();
        var hours = datesForHours.Count == 0
            ? 0
            : OpsServicesStore.TotalHoursForEmployeeInRange(emp.Id, datesForHours[0], datesForHours[^1]);
        var hoursTb = new TextBlock
        {
            Text = $"{hours}h",
            FontSize = S(12),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")!),
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

        var sp = new StackPanel { Margin = new Thickness(S(8), S(6), S(8), S(6)) };
        sp.Children.Add(nameRow);
        sp.Children.Add(role);

        var border = new Border
        {
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")!),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = sp
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

        var stack = new StackPanel { Margin = new Thickness(S(4)) };
        foreach (var shift in shifts)
        {
            var accent = (Brush)new BrushConverter().ConvertFromString(emp.AccentColorHex)!;
            var border = new Border
            {
                Background = accent is SolidColorBrush sb
                    ? new SolidColorBrush(Color.FromArgb(40, sb.Color.R, sb.Color.G, sb.Color.B))
                    : accent,
                BorderBrush = accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(S(6)),
                Padding = new Thickness(S(6), S(4), S(6), S(4)),
                Margin = new Thickness(0, 0, 0, S(4))
            };
            var timeTb = new TextBlock
            {
                Text = $"{shift.Start:hh\\:mm} to {shift.End:hh\\:mm}",
                FontSize = S(11),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")!),
                TextWrapping = TextWrapping.Wrap
            };
            var tablesTb = new TextBlock
            {
                Text = OpsServicesStore.TableNamesSummary(shift.TableIds),
                FontSize = S(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B5563")!),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, S(2), 0, 0)
            };
            var inner = new StackPanel();
            inner.Children.Add(timeTb);
            inner.Children.Add(tablesTb);
            border.Child = inner;
            stack.Children.Add(border);
        }

        var cell = new Border
        {
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")!),
            BorderThickness = new Thickness(0, 0, 1, 1),
            MinHeight = S(56),
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
            Text = "Shifts / day",
            FontSize = S(12),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")!),
            Margin = new Thickness(S(8), S(4), S(8), S(4)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        ScheduleHostGrid.Children.Add(tb);
    }

    private void AddTotalsCountCell(int row, int col, DateOnly date)
    {
        var n = OpsServicesStore.GetShifts().Count(s => s.Date == date);
        var tb = new TextBlock
        {
            Text = $"{n} shift(s)",
            FontSize = S(12),
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(S(4)),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")!)
        };
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        ScheduleHostGrid.Children.Add(tb);
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

    private void PillTableManagement_Click(object sender, RoutedEventArgs e)
    {
        HighlightShiftPill(false);
        _navigateToTableManagement();
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(Window.GetWindow(this), "Export schedule is not wired in this demo build.",
            "Export Schedule", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
