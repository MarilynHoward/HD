using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RestaurantPosWpf;

/// <summary>
/// Immutable filter state passed from <see cref="RptDashboardMain"/> into report overlays (e.g. Daily Sales Summary).
/// </summary>
public sealed record RptDashboardFilterSnapshot(
        DateOnly RangeStart,
        DateOnly RangeEnd,
        string BranchFilterId,
        string ChannelFilterId,
        string UserRoleFilterId,
        string DateRangeDisplay,
        string BranchDisplay,
        string ChannelDisplay,
        string UserRoleDisplay);

public sealed partial class RptDashboardMain : UserControl
{
    private const int RecentlyUsedMaxDistinctReports = 4;

    public static readonly DependencyProperty RecentCardUniformColumnsProperty =
        DependencyProperty.Register(
            nameof(RecentCardUniformColumns),
            typeof(int),
            typeof(RptDashboardMain),
            new PropertyMetadata(1));

    public static readonly DependencyProperty AttentionCardUniformColumnsProperty =
        DependencyProperty.Register(
            nameof(AttentionCardUniformColumns),
            typeof(int),
            typeof(RptDashboardMain),
            new PropertyMetadata(1));

    public static readonly DependencyProperty BrowseRow1CardUniformColumnsProperty =
        DependencyProperty.Register(
            nameof(BrowseRow1CardUniformColumns),
            typeof(int),
            typeof(RptDashboardMain),
            new PropertyMetadata(1));

    public static readonly DependencyProperty BrowseRow2CardUniformColumnsProperty =
        DependencyProperty.Register(
            nameof(BrowseRow2CardUniformColumns),
            typeof(int),
            typeof(RptDashboardMain),
            new PropertyMetadata(1));

    /// <summary>Max width for the date-range pill; derived from <see cref="FilterStripHost"/> width so the strip fits without horizontal scroll.</summary>
    public static readonly DependencyProperty FilterStripDateMaxWidthProperty =
        DependencyProperty.Register(
            nameof(FilterStripDateMaxWidth),
            typeof(double),
            typeof(RptDashboardMain),
            new PropertyMetadata(560.0));

    /// <summary>Columns for the Recently Used strip's UniformGrid.</summary>
    public int RecentCardUniformColumns
    {
        get => (int)GetValue(RecentCardUniformColumnsProperty);
        set => SetValue(RecentCardUniformColumnsProperty, value);
    }

    /// <summary>Columns for <see cref="AttentionItems"/> strip.</summary>
    public int AttentionCardUniformColumns
    {
        get => (int)GetValue(AttentionCardUniformColumnsProperty);
        set => SetValue(AttentionCardUniformColumnsProperty, value);
    }

    /// <summary>Columns for first browse row.</summary>
    public int BrowseRow1CardUniformColumns
    {
        get => (int)GetValue(BrowseRow1CardUniformColumnsProperty);
        set => SetValue(BrowseRow1CardUniformColumnsProperty, value);
    }

    /// <summary>Columns for second browse row.</summary>
    public int BrowseRow2CardUniformColumns
    {
        get => (int)GetValue(BrowseRow2CardUniformColumnsProperty);
        set => SetValue(BrowseRow2CardUniformColumnsProperty, value);
    }

    /// <summary>Caps date pill width so filters share horizontal space; caption wraps inside the pill.</summary>
    public double FilterStripDateMaxWidth
    {
        get => (double)GetValue(FilterStripDateMaxWidthProperty);
        set => SetValue(FilterStripDateMaxWidthProperty, value);
    }

    private UiScaleState? _uiScaleStateForCardLayout;

    private readonly Action<RecentUsedRow>? _onRecentReport;
    private readonly Action<AttentionNeededRow>? _onAttentionItem;
    private readonly Action<BrowseGroupTile>? _onBrowseGroupOrViewReports;
    private readonly Action? _onExportDashboard;

    private readonly List<RecentUsedRow> _seedRecent = new();
    private readonly List<AttentionNeededRow> _seedAttention = new();
    private readonly List<BrowseGroupTile> _seedBrowse1 = new();
    private readonly List<BrowseGroupTile> _seedBrowse2 = new();

    private string _searchQuery = string.Empty;
    private readonly DispatcherTimer _searchDebounce = new() { Interval = TimeSpan.FromMilliseconds(200) };

    private bool _suppressReportDateChanged;

    /// <summary>Matches <c>public.rpt_reports.report_code</c> for Daily Sales Summary.</summary>
    public const string DailySalesReportCode = "rpt.daily_sales";

    /// <summary>Matches <c>public.rpt_reports.report_code</c> for VAT Summary.</summary>
    public const string VatSummaryReportCode = "rpt.vat_summary";

    /// <summary>Matches <c>public.rpt_reports.report_code</c> for Voids Report.</summary>
    public const string VoidsReportCode = "rpt.voids";

    /// <summary>Matches <c>public.rpt_reports.report_code</c> for Wastage Report.</summary>
    public const string WastageReportCode = "rpt.wastage";

    public RptDashboardMain()
        : this(null, null, null, null)
    {
    }

    public RptDashboardMain(
            Action<RecentUsedRow>? onRecentlyUsedReportActivated,
            Action<AttentionNeededRow>? onAttentionNeededActivated,
            Action<BrowseGroupTile>? onBrowseGroupingActivated,
            Action? onExportDashboard = null)
    {
        _onRecentReport = onRecentlyUsedReportActivated;
        _onAttentionItem = onAttentionNeededActivated;
        _onBrowseGroupOrViewReports = onBrowseGroupingActivated;
        _onExportDashboard = onExportDashboard;

        InitializeComponent();

        _searchDebounce.Tick += (_, _) =>
        {
            _searchQuery = (RptDashboardSearchBox?.Text ?? string.Empty).Trim();
            ApplyFilters();
        };
        Unloaded += RptDashboardMain_Unloaded;
        Loaded += RptDashboardMain_Loaded;

        RecentReports.CollectionChanged += DashboardReportCollections_Changed;
        AttentionItems.CollectionChanged += DashboardReportCollections_Changed;
        ReportGroupsRow1.CollectionChanged += DashboardReportCollections_Changed;
        ReportGroupsRow2.CollectionChanged += DashboardReportCollections_Changed;

        ReloadDummyDataIntoCollections();
        WireFilterDropdownsFromDatabase();
    }

    private static DateTime YesterdayLocal => DateTime.Today.AddDays(-1).Date;

    private void RptDashboardMain_Loaded(object sender, RoutedEventArgs e)
    {
        _suppressReportDateChanged = true;
        try
        {
            var y = YesterdayLocal;
            DpReportRangeEnd.SelectedDate ??= y;
            DpReportRangeStart.SelectedDate ??= y.AddDays(-29);
        }
        finally
        {
            _suppressReportDateChanged = false;
        }

        ClampReportDateRange();
        UpdateDateRangeLine();

        if (Application.Current.TryFindResource("UiScaleState") is UiScaleState st)
        {
            _uiScaleStateForCardLayout = st;
            st.PropertyChanged += UiScaleStateForCardLayout_PropertyChanged;
        }

        ScheduleResponsiveLayout();
    }

    private void RptDashboardMain_Unloaded(object sender, RoutedEventArgs e)
    {
        _searchDebounce.Stop();
        if (_uiScaleStateForCardLayout != null)
            _uiScaleStateForCardLayout.PropertyChanged -= UiScaleStateForCardLayout_PropertyChanged;
        _uiScaleStateForCardLayout = null;
    }

    private void UiScaleStateForCardLayout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UiScaleState.FontScale))
            ScheduleResponsiveLayout();
    }

    private void DashboardReportCollections_Changed(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleResponsiveLayout();
    }

    private void DashboardCardStripHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
            ScheduleResponsiveLayout();
    }

    private void FilterStripHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
            ScheduleResponsiveLayout();
    }

    private void ScheduleResponsiveLayout()
    {
        if (!IsLoaded)
            return;

        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                RefreshFilterStripLayout();
                RefreshDashboardCardUniformColumns();
            }),
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Sizes the date-range caption max width from the filter strip width (similar spirit to card column math).
    /// </summary>
    private void RefreshFilterStripLayout()
    {
        var w = FilterStripHost?.ActualWidth ?? 0;
        var scale = ReadUiFontScale();
        if (w <= 1)
            return;

        // Wider strip: start + end date pickers share one capped width.
        var maxDate = Math.Min(560 * scale, Math.Max(240 * scale, w * 0.52));
        if (Math.Abs(maxDate - FilterStripDateMaxWidth) > 0.5)
            FilterStripDateMaxWidth = maxDate;
    }

    /// <summary>
    /// Fits cards to strip width: column count = min(item count, floor((width + gap) / (scaled min card + gap))).
    /// Uses the same base min widths as the previous fixed cards (268 / 292 / 340) × <see cref="UiScaleState.FontScale"/>.
    /// </summary>
    private void RefreshDashboardCardUniformColumns()
    {
        var nextRecent = ComputePinnedUniformColumns(RecentReports.Count, 4);
        if (nextRecent != RecentCardUniformColumns)
            RecentCardUniformColumns = nextRecent;

        var nextAtt = ComputePinnedUniformColumns(AttentionItems.Count, 4);
        if (nextAtt != AttentionCardUniformColumns)
            AttentionCardUniformColumns = nextAtt;

        var nextB1 = ComputePinnedUniformColumns(ReportGroupsRow1.Count, 3);
        if (nextB1 != BrowseRow1CardUniformColumns)
            BrowseRow1CardUniformColumns = nextB1;

        var nextB2 = ComputePinnedUniformColumns(ReportGroupsRow2.Count, 3);
        if (nextB2 != BrowseRow2CardUniformColumns)
            BrowseRow2CardUniformColumns = nextB2;
    }

    private static int ComputeUniformColumns(double availableWidth, int itemCount, double minCardBaseWidth, double horizontalGapBase)
    {
        if (itemCount <= 0)
            return 1;

        var scale = ReadUiFontScale();
        var minCard = minCardBaseWidth * scale;
        var gap = horizontalGapBase * scale;
        var slot = minCard + gap;
        if (slot <= 0.01)
            return 1;

        var maxCols = (int)Math.Floor((availableWidth + gap) / slot);
        maxCols = Math.Max(1, maxCols);
        return Math.Min(itemCount, maxCols);
    }

    private static Int32 ComputePinnedUniformColumns(Int32 itemCount, Int32 maxColumns)
    {
        if (itemCount <= 0)
            return 1;

        if (maxColumns <= 0)
            return 1;

        return Math.Min(itemCount, maxColumns);
    }

    private static double ReadUiFontScale()
    {
        return Application.Current?.TryFindResource("UiScaleState") is UiScaleState s ? s.FontScale : 1.25;
    }

    private void ReportRangeDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressReportDateChanged)
            return;

        ClampReportDateRange();
        UpdateDateRangeLine();
    }

    private void ClampReportDateRange()
    {
        var y = YesterdayLocal;
        var end = DpReportRangeEnd.SelectedDate?.Date ?? y;
        if (end > y)
            end = y;

        var start = DpReportRangeStart.SelectedDate?.Date ?? end.AddDays(-29);
        if (start > end)
            start = end;

        _suppressReportDateChanged = true;
        try
        {
            if (DpReportRangeEnd.SelectedDate?.Date != end)
                DpReportRangeEnd.SelectedDate = end;
            if (DpReportRangeStart.SelectedDate?.Date != start)
                DpReportRangeStart.SelectedDate = start;

            DpReportRangeEnd.DisplayDateEnd = y;
            DpReportRangeStart.DisplayDateEnd = y;
        }
        finally
        {
            _suppressReportDateChanged = false;
        }
    }

    private void UpdateDateRangeLine()
    {
        var y = YesterdayLocal;
        var end = DpReportRangeEnd.SelectedDate?.Date ?? y;
        var start = DpReportRangeStart.SelectedDate?.Date ?? end.AddDays(-29);
        TxtReportDateRangeStartPill.Text = start.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
        TxtReportDateRangeEndPill.Text = end.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
    }

    /// <summary>Current dashboard filters for report overlays and SQL.</summary>
    public RptDashboardFilterSnapshot BuildFilterSnapshot()
    {
        ClampReportDateRange();
        var endDt = DpReportRangeEnd.SelectedDate ?? YesterdayLocal;
        var startDt = DpReportRangeStart.SelectedDate ?? endDt.AddDays(-29);
        var start = DateOnly.FromDateTime(startDt.Date);
        var end = DateOnly.FromDateTime(endDt.Date);

        var branch = BranchFilterCombo.SelectedItem as FilterOption
                     ?? new FilterOption("all", RptThemeString("Rpt.Filter.AllBranchesCaption"));
        var channel = ChannelFilterCombo.SelectedItem as FilterOption
                      ?? new FilterOption("all", RptThemeString("Rpt.Filter.AllChannelsCaption"));
        var role = RoleFilterCombo.SelectedItem as RoleFilterOption
                   ?? new RoleFilterOption("all", RptThemeString("Rpt.Filter.AllUsersCaption"));

        var dateDisp = start.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) + " – "
                       + end.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

        return new RptDashboardFilterSnapshot(
            start,
            end,
            branch.Id,
            channel.Id,
            role.RoleId,
            dateDisp,
            branch.Label,
            channel.Label,
            role.Label);
    }

    private void CloseReportOverlay()
    {
        RptOverlayLayer.Visibility = Visibility.Collapsed;
        RptOverlayContentHost.Content = null;
    }

    private void OpenDailySalesOverlay()
    {
        var snapshot = BuildFilterSnapshot();
        RptOverlayContentHost.Content = new RptDailySalesSummaryOverlay(snapshot, CloseReportOverlay);
        RptOverlayLayer.Visibility = Visibility.Visible;
    }

    private void OpenVatSummaryOverlay()
    {
        var snapshot = BuildFilterSnapshot();
        RptOverlayContentHost.Content = new RptVatSummaryOverlay(snapshot, CloseReportOverlay);
        RptOverlayLayer.Visibility = Visibility.Visible;
    }

    private void OpenVoidsReportOverlay()
    {
        var snapshot = BuildFilterSnapshot();
        RptOverlayContentHost.Content = new RptVoidsReportOverlay(snapshot, CloseReportOverlay);
        RptOverlayLayer.Visibility = Visibility.Visible;
    }

    private void OpenWastageReportOverlay()
    {
        var snapshot = BuildFilterSnapshot();
        RptOverlayContentHost.Content = new RptWastageReportOverlay(snapshot, CloseReportOverlay);
        RptOverlayLayer.Visibility = Visibility.Visible;
    }

    public ObservableCollection<RecentUsedRow> RecentReports { get; } = new();

    public ObservableCollection<AttentionNeededRow> AttentionItems { get; } = new();

    public ObservableCollection<BrowseGroupTile> ReportGroupsRow1 { get; } = new();

    public ObservableCollection<BrowseGroupTile> ReportGroupsRow2 { get; } = new();

    public void ReloadDummyDataIntoCollections()
    {
        ReloadDashboardCollectionsFromDatabase();
    }

    private static void ReloadCore<T>(IEnumerable<T> source, ICollection<T> target)
    {
        target.Clear();
        foreach (var row in source)
            target.Add(row);
    }

    private void WireFilterDropdownsFromDatabase()
    {
        BranchFilterCombo.DisplayMemberPath = nameof(FilterOption.Label);
        BranchFilterCombo.ItemsSource = LoadBranchFilterOptions();
        BranchFilterCombo.SelectedIndex = 0;

        ChannelFilterCombo.DisplayMemberPath = nameof(FilterOption.Label);
        ChannelFilterCombo.ItemsSource = LoadChannelFilterOptions();
        ChannelFilterCombo.SelectedIndex = 0;

        RoleFilterCombo.DisplayMemberPath = nameof(RoleFilterOption.Label);
        RoleFilterCombo.ItemsSource = LoadRoleFilterOptions();
        RoleFilterCombo.SelectedIndex = 0;
    }

    private static List<FilterOption> LoadBranchFilterOptions()
    {
        var list = new List<FilterOption> { new("all", RptThemeString("Rpt.Filter.AllBranchesCaption")) };
        try
        {
            var dt = App.aps.pda.GetDataTable(
                App.aps.LocalConnectionstring(App.aps.propertyBranchCode),
                App.aps.sql.SelectLocalRptBranchesForFilters(),
                60);
            foreach (DataRow r in dt.Rows)
            {
                var code = Convert.ToString(r["branch_code"], CultureInfo.InvariantCulture)?.Trim();
                var descr = Convert.ToString(r["descr"], CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                    continue;
                list.Add(new FilterOption(code, string.IsNullOrWhiteSpace(descr) ? code : descr));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[RptDashboardMain] SelectLocalRptBranchesForFilters failed: " + ex.Message);
        }

        return list;
    }

    private static List<FilterOption> LoadChannelFilterOptions()
    {
        var list = new List<FilterOption> { new("all", RptThemeString("Rpt.Filter.AllChannelsCaption")) };
        try
        {
            var dt = App.aps.pda.GetDataTable(
                App.aps.LocalConnectionstring(App.aps.propertyBranchCode),
                App.aps.sql.SelectLocalRptChannelsForFilters(),
                60);
            foreach (DataRow r in dt.Rows)
            {
                var code = Convert.ToString(r["channel_code"], CultureInfo.InvariantCulture)?.Trim();
                var descr = Convert.ToString(r["descr"], CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                    continue;
                list.Add(new FilterOption(code, string.IsNullOrWhiteSpace(descr) ? code : descr));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[RptDashboardMain] SelectLocalRptChannelsForFilters failed: " + ex.Message);
        }

        return list;
    }

    private static List<RoleFilterOption> LoadRoleFilterOptions()
    {
        var list = new List<RoleFilterOption> { new("all", RptThemeString("Rpt.Filter.AllUsersCaption")) };
        try
        {
            var dt = App.aps.pda.GetDataTable(
                App.aps.LocalConnectionstring(App.aps.propertyBranchCode),
                App.aps.sql.SelectLocalRptUserRolesForFilters(),
                60);
            foreach (DataRow r in dt.Rows)
            {
                var code = Convert.ToString(r["userrole_code"], CultureInfo.InvariantCulture)?.Trim();
                var descr = Convert.ToString(r["descr"], CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                    continue;
                list.Add(new RoleFilterOption(code, string.IsNullOrWhiteSpace(descr) ? code : descr));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[RptDashboardMain] SelectLocalRptUserRolesForFilters failed: " + ex.Message);
        }

        return list;
    }

    /// <summary>Records branch-local report open for future Recently Used ordering; failures are ignored.</summary>
    private static void TryRecordReportAccess(string reportCode)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            return;
        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            var uid = App.aps.signedOnUserId;
            App.aps.Execute(cn, App.aps.sql.InsertReportAccessLog(uid, reportCode.Trim(), uid));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[RptDashboardMain] InsertReportAccessLog failed: " + ex.Message);
        }
    }

    /// <summary>Search filters in-memory dashboard rows; branch/channel/role combos are populated from local <c>rpt_*</c> tables (POS_CONTROL sync).</summary>
    private void ApplyFilters()
    {
        var q = (_searchQuery ?? string.Empty).Trim();
        bool Match(string? s) =>
            q.Length == 0 || (!string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase));

        ReloadCore(_seedRecent.Where(r => Match(r.Report.DisplayName) || Match(r.LastRunDisplay)), RecentReports);
        ReloadCore(_seedAttention.Where(a => Match(a.Report.DisplayName)), AttentionItems);
        ReloadCore(_seedBrowse1.Where(g => Match(g.Title) || Match(g.Description)), ReportGroupsRow1);
        ReloadCore(_seedBrowse2.Where(g => Match(g.Title) || Match(g.Description)), ReportGroupsRow2);
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        _searchQuery = (RptDashboardSearchBox?.Text ?? string.Empty).Trim();
        ApplyFilters();
    }

    private void ExportDashboard_Click(object sender, RoutedEventArgs e)
    {
        _onExportDashboard?.Invoke();
    }

    private void RptDashboardSearchBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (RptDashboardSearchBox != null && RptDashboardSearchBox.IsKeyboardFocusWithin)
                return;
            RptDashboardSearchBox?.Focus();
            Keyboard.Focus(RptDashboardSearchBox);
        }), DispatcherPriority.Input);
    }

    private void RptDashboardSearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (RptSearchFocusOuter != null)
            RptSearchFocusOuter.Visibility = Visibility.Visible;

        if (RptSearchFocusInner != null)
            RptSearchFocusInner.Visibility = Visibility.Visible;

        if (RptDashboardSearchBorder != null)
            RptDashboardSearchBorder.BorderBrush = Brushes.Transparent;
    }

    private void RptDashboardSearchBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (RptSearchFocusOuter != null)
            RptSearchFocusOuter.Visibility = Visibility.Collapsed;

        if (RptSearchFocusInner != null)
            RptSearchFocusInner.Visibility = Visibility.Collapsed;

        if (RptDashboardSearchBorder != null)
            RptDashboardSearchBorder.BorderBrush = ThemeBrush("Brush.BorderGrey");
    }

    private void RptDashboardSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private static void InvokeIfNotNull<T>(Action<T>? callback, T arg)
    {
        if (callback != null)
            callback(arg);
    }

    private static Brush ThemeBrush(string key)
    {
        if (Application.Current?.TryFindResource(key) is Brush b)
            return b;
        return Brushes.Magenta;
    }

    private static string RptThemeString(string resourceKey) =>
        Application.Current?.TryFindResource(resourceKey) is string s ? s : string.Empty;

    private static string RptThemeFormat(string resourceKey, params object[] args)
    {
        var fmt = RptThemeString(resourceKey);
        if (string.IsNullOrEmpty(fmt))
            return string.Empty;
        try
        {
            return string.Format(CultureInfo.InvariantCulture, fmt, args);
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    /// <summary>Parses WPF web colour strings (#RGB / #RRGGBB / #AARRGGBB). Used by <see cref="CardAccent.FromHex"/>.</summary>
    private static bool TryParseWebHex(string? s, out Color color)
    {
        color = Colors.Magenta;
        if (string.IsNullOrWhiteSpace(s))
            return false;

        var t = s.Trim();
        if (!t.StartsWith("#", StringComparison.Ordinal))
            t = "#" + t;

        try
        {
            var o = ColorConverter.ConvertFromString(t);
            if (o is Color c)
            {
                color = c;
                return true;
            }
        }
        catch (FormatException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return false;
    }

    private static SolidColorBrush SolidBrushFromWebHex(string? s)
    {
        if (!TryParseWebHex(s, out var color))
            color = Colors.Magenta;

        var b = new SolidColorBrush(color);
        if (b.CanFreeze)
            b.Freeze();
        return b;
    }

    #region Nested data contracts (host & future SQL layers may reference these types)

    /// <summary>Stable id + label + icon for a runnable report (navigation, audit, nested browse). Matches seed/SQL columns.</summary>
    public sealed class ExecutableReportRef
    {
        public ExecutableReportRef(string id, string displayName, DashboardIconGlyph iconKind = DashboardIconGlyph.Document)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            IconKind = iconKind;
            IconGeometry = BuildIconGeometry(iconKind);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public DashboardIconGlyph IconKind { get; }
        public Geometry IconGeometry { get; }
    }

    /// <summary>
    /// Resolved palette for a card row. Build from <see cref="ThemeBrush"/> resource keys (shared theme)
    /// or from ARGB hex strings loaded from SQL — both paths produce the same runtime brushes for bindings.
    /// </summary>
    public sealed class CardAccent
    {
        public CardAccent(
                Brush iconBackdrop,
                Brush iconForeground,
                Brush hoverBorder,
                Brush hoverBackground,
                Brush chevronHot)
        {
            IconBackdrop = iconBackdrop;
            IconForeground = iconForeground;
            HoverBorder = hoverBorder;
            HoverBackground = hoverBackground;
            ChevronHot = chevronHot;
        }

        public Brush IconBackdrop { get; }
        public Brush IconForeground { get; }
        public Brush HoverBorder { get; }
        public Brush HoverBackground { get; }
        public Brush ChevronHot { get; }

        public static CardAccent FromKeys(
                string backdropKey,
                string foregroundKey,
                string hoverBorderKey,
                string hoverSurfaceKey,
                string chevronHotKey = "Brush.RptChevronStrong")
            => new(
                ThemeBrush(backdropKey),
                ThemeBrush(foregroundKey),
                ThemeBrush(hoverBorderKey),
                ThemeBrush(hoverSurfaceKey),
                ThemeBrush(chevronHotKey));

        /// <summary>
        /// Builds accent brushes from hex strings (e.g. from PostgreSQL text columns). Accepts
        /// <c>#RRGGBB</c>, <c>#AARRGGBB</c>, <c>RRGGBB</c>, or <c>rgb</c> (CSS-style doubled digits).
        /// Invalid segments resolve to magenta so issues are visible during development.
        /// </summary>
        /// <param name="chevronHotHex">Optional; when null or whitespace, uses <paramref name="iconForegroundHex"/>.</param>
        public static CardAccent FromHex(
                string iconBackdropHex,
                string iconForegroundHex,
                string hoverBorderHex,
                string hoverSurfaceHex,
                string? chevronHotHex = null)
            => new(
                SolidBrushFromWebHex(iconBackdropHex),
                SolidBrushFromWebHex(iconForegroundHex),
                SolidBrushFromWebHex(hoverBorderHex),
                SolidBrushFromWebHex(hoverSurfaceHex),
                SolidBrushFromWebHex(string.IsNullOrWhiteSpace(chevronHotHex) ? iconForegroundHex : chevronHotHex!));

        /// <summary>Returns false if any required colour fails to parse (chevron defaults to foreground when omitted).</summary>
        public static bool TryFromHex(
                string iconBackdropHex,
                string iconForegroundHex,
                string hoverBorderHex,
                string hoverSurfaceHex,
                string? chevronHotHex,
                out CardAccent? accent)
        {
            accent = null;
            var chevron = string.IsNullOrWhiteSpace(chevronHotHex) ? iconForegroundHex : chevronHotHex;
            if (!TryParseWebHex(iconBackdropHex, out _)
                || !TryParseWebHex(iconForegroundHex, out _)
                || !TryParseWebHex(hoverBorderHex, out _)
                || !TryParseWebHex(hoverSurfaceHex, out _)
                || !TryParseWebHex(chevron, out _))
                return false;

            accent = FromHex(iconBackdropHex, iconForegroundHex, hoverBorderHex, hoverSurfaceHex, chevronHotHex);
            return true;
        }
    }

    /// <summary>
    /// Optional theme as PeoplePosTheme brush keys. Report dashboard tiles use <see cref="RptDashboardCardThemeHex"/> instead so colours need not live in XAML.
    /// </summary>
    public readonly record struct RptDashboardCardThemeKeys(
            string IconBackdropKey,
            string IconForegroundKey,
            string HoverBorderKey,
            string HoverSurfaceKey,
            string ChevronHotKey = "Brush.RptChevronStrong")
    {
        public CardAccent ToCardAccent() =>
            CardAccent.FromKeys(
                IconBackdropKey,
                IconForegroundKey,
                HoverBorderKey,
                HoverSurfaceKey,
                ChevronHotKey);
    }

    /// <summary>
    /// Card colours as ARGB/RGB hex strings — maps to <c>ui_*_hex</c> columns on
    /// <c>public.rpt_reports</c> / <c>public.rpt_report_categories</c>. Does not use <see cref="PeoplePosTheme"/>.
    /// </summary>
    public readonly record struct RptDashboardCardThemeHex(
            string IconBackdropHex,
            string IconForegroundHex,
            string HoverBorderHex,
            string HoverSurfaceHex,
            string? ChevronHotHex = null)
    {
        public CardAccent ToCardAccent() =>
            CardAccent.FromHex(
                IconBackdropHex,
                IconForegroundHex,
                HoverBorderHex,
                HoverSurfaceHex,
                ChevronHotHex);
    }

    public enum DashboardIconGlyph
    {
        BarChart,
        Document,
        WasteBin,
        /// <summary>Trash/delete icon from Staff Access user row (<c>StaffAccessUserDetails</c> remove user).</summary>
        StaffDeleteUserTrash,
        /// <summary>Documents tab icon from User Details tab strip (<c>RadStaffTabDocs</c>).</summary>
        StaffDocumentsTab,
        AlertCircle,
        Box,
        /// <summary>Lucide <c>package</c> (inventory / stock).</summary>
        Package,
        TrendDown,
        Coins,
        Truck,
        PieChart,
        ShieldCheck,
        CubeStock,
    }

    /// <summary>One selectable dashboard icon — persist <see cref="Id"/> in seed/SQL/UI.</summary>
    public sealed record RptDashboardIconDefinition(string Id, string Label, DashboardIconGlyph Glyph);

    /// <summary>
    /// Canonical icon ids (stable for PostgreSQL) and labels for admin pickers. Add a row here and a <see cref="BuildIconGeometry"/> case to extend.
    /// </summary>
    public static class RptDashboardIconCatalog
    {
        private static readonly RptDashboardIconDefinition[] Definitions =
        {
            new("bar_chart", "Bar chart", DashboardIconGlyph.BarChart),
            new("document", "Document", DashboardIconGlyph.Document),
            new("waste_bin", "Waste bin", DashboardIconGlyph.WasteBin),
            new("staff_delete_user_trash", "Trash / delete user", DashboardIconGlyph.StaffDeleteUserTrash),
            new("staff_documents_tab", "Documents tab", DashboardIconGlyph.StaffDocumentsTab),
            new("alert_circle", "Alert circle", DashboardIconGlyph.AlertCircle),
            new("box", "Box", DashboardIconGlyph.Box),
            new("package", "Package / stock", DashboardIconGlyph.Package),
            new("trend_down", "Trending down", DashboardIconGlyph.TrendDown),
            new("coins", "Coins / cash", DashboardIconGlyph.Coins),
            new("truck", "Truck / delivery", DashboardIconGlyph.Truck),
            new("pie_chart", "Pie chart", DashboardIconGlyph.PieChart),
            new("shield_check", "Shield check", DashboardIconGlyph.ShieldCheck),
            new("cube_stock", "Cube / stock", DashboardIconGlyph.CubeStock),
        };

        /// <summary>All icons — bind for combo/list; store <see cref="RptDashboardIconDefinition.Id"/> on reports.</summary>
        public static IReadOnlyList<RptDashboardIconDefinition> All { get; } = Definitions;

        private static readonly IReadOnlyDictionary<string, DashboardIconGlyph> GlyphByCatalogOrEnumName =
                BuildGlyphMap();

        private static IReadOnlyDictionary<string, DashboardIconGlyph> BuildGlyphMap()
        {
            var d = new Dictionary<string, DashboardIconGlyph>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in Definitions)
            {
                d[def.Id] = def.Glyph;
                d[def.Glyph.ToString()] = def.Glyph;
            }

            return d;
        }

        /// <summary>
        /// Resolves seed/SQL value: canonical <paramref name="iconRef"/> id, or legacy enum name (e.g. <c>BarChart</c>).
        /// Unknown values resolve to <see cref="DashboardIconGlyph.Document"/>.
        /// </summary>
        public static DashboardIconGlyph ResolveGlyph(string? iconRef)
        {
            if (string.IsNullOrWhiteSpace(iconRef))
                return DashboardIconGlyph.Document;

            var t = iconRef.Trim();
            if (GlyphByCatalogOrEnumName.TryGetValue(t, out var g))
                return g;

            return DashboardIconGlyph.Document;
        }

        /// <summary>Returns false when <paramref name="iconRef"/> is empty or not recognised.</summary>
        public static bool TryResolveGlyph(string? iconRef, out DashboardIconGlyph glyph)
        {
            glyph = DashboardIconGlyph.Document;
            if (string.IsNullOrWhiteSpace(iconRef))
                return false;

            var t = iconRef.Trim();
            if (GlyphByCatalogOrEnumName.TryGetValue(t, out glyph))
                return true;

            return false;
        }
    }

    public sealed class RecentUsedRow
    {
        public RecentUsedRow(ExecutableReportRef report, string lastRunDisplay, CardAccent theme)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
            LastRunDisplay = lastRunDisplay ?? string.Empty;
            Theme = theme ?? throw new ArgumentNullException(nameof(theme));
            IconGeometry = report.IconGeometry;
        }

        public ExecutableReportRef Report { get; }
        public string LastRunDisplay { get; }
        public CardAccent Theme { get; }
        public Geometry IconGeometry { get; }

        public DateTime LastAccessedUtc { get; set; }
        public string? CategoryId { get; set; }
    }

    public sealed class AttentionNeededRow
    {
        public AttentionNeededRow(
                ExecutableReportRef report,
                int attentionCount,
                CardAccent iconTheme,
                CardAccent badgeTheme)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
            AttentionCount = attentionCount;
            IconTheme = iconTheme ?? throw new ArgumentNullException(nameof(iconTheme));
            BadgeTheme = badgeTheme ?? throw new ArgumentNullException(nameof(badgeTheme));
            IconGeometry = report.IconGeometry;
        }

        public ExecutableReportRef Report { get; }
        public int AttentionCount { get; }
        public CardAccent IconTheme { get; }
        public CardAccent BadgeTheme { get; }
        public Geometry IconGeometry { get; }
    }

    public sealed class BrowseGroupTile
    {
        public BrowseGroupTile(
                string groupId,
                string title,
                string description,
                int reportCount,
                CardAccent theme,
                DashboardIconGlyph iconKind,
                bool showChevronNextToTitle)
        {
            GroupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            ReportCount = reportCount;
            Theme = theme ?? throw new ArgumentNullException(nameof(theme));
            IconKind = iconKind;
            ShowChevronNextToTitle = showChevronNextToTitle;
            IconGeometry = BuildIconGeometry(iconKind);
        }

        public string GroupId { get; }
        public string Title { get; }
        public string Description { get; }
        public int ReportCount { get; }
        public CardAccent Theme { get; }
        public DashboardIconGlyph IconKind { get; }
        public bool ShowChevronNextToTitle { get; }
        public Geometry IconGeometry { get; }

        public IReadOnlyList<ExecutableReportRef>? ReportsInGroup { get; set; }
    }

    public sealed class FilterOption
    {
        public FilterOption(string id, string label)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        public string Id { get; }
        public string Label { get; }

        public override string ToString() => Label;
    }

    public sealed class RoleFilterOption
    {
        public RoleFilterOption(string roleId, string label)
        {
            RoleId = roleId ?? throw new ArgumentNullException(nameof(roleId));
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        public string RoleId { get; }
        public string Label { get; }

        public override string ToString() => Label;
    }

    /// <summary>Shape for future audit insert when a user opens a report.</summary>
    public sealed class ReportAccessAudit
    {
        public int UserId { get; set; }
        public string ReportId { get; set; } = string.Empty;
        public DateTime AccessedUtc { get; set; }
    }

    #endregion

    #region Dashboard DB loaders (Recently Used / Attention / Browse — themes from PostgreSQL)

    /// <summary>When SQL hex columns are missing/invalid, use shared theme keys (not literals in C#).</summary>
    private static CardAccent DashboardUiFallbackAccentFromTheme() =>
        CardAccent.FromKeys(
            "Brush.SurfaceGreySoft",
            "Brush.TextMuted",
            "Brush.BorderGrey",
            "Brush.SurfaceOffWhite",
            "Brush.RptChevronStrong");

    private void ReloadDashboardCollectionsFromDatabase()
    {
        const int timeoutSeconds = 60;
        var recent = new List<RecentUsedRow>();
        var attention = new List<AttentionNeededRow>();
        var browse1 = new List<BrowseGroupTile>();
        var browse2 = new List<BrowseGroupTile>();

        try
        {
            var cn = App.aps.LocalConnectionstring(App.aps.propertyBranchCode);
            LoadRecentUsedRowsFromDb(cn, timeoutSeconds, recent);
            if (recent.Count == 0)
            {
                App.aps.Execute(
                    cn,
                    App.aps.sql.InsertLocalRptDefaultRecentReportsWhenUserHasNoHistory(
                        App.aps.signedOnUserId,
                        App.aps.signedOnUserId));
                recent.Clear();
                LoadRecentUsedRowsFromDb(cn, timeoutSeconds, recent);
            }

            var dtAttention = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectLocalRptReportsForDashboardAttention(), timeoutSeconds);
            foreach (DataRow row in dtAttention.Rows)
            {
                var code = DbCellString(row, "report_code").Trim();
                if (code.Length == 0)
                    continue;
                var title = DbCellString(row, "descr").Trim();
                if (title.Length == 0)
                    title = code;
                var cnt = DbCellNullableInt(row, "dashboard_attention_count");
                if (!cnt.HasValue || cnt.Value < 1)
                    continue;
                var glyph = RptDashboardIconCatalog.ResolveGlyph(DbCellString(row, "icon_glyph_id"));
                attention.Add(new AttentionNeededRow(
                    new ExecutableReportRef(code, title, glyph),
                    cnt.Value,
                    AccentFromReportPrimaryHexRow(row),
                    AccentFromReportBadgeHexRow(row)));
            }

            browse1.AddRange(LoadBrowseRowFromDb(cn, browseRow: 1, timeoutSeconds));
            browse2.AddRange(LoadBrowseRowFromDb(cn, browseRow: 2, timeoutSeconds));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[RptDashboardMain] ReloadDashboardCollectionsFromDatabase: " + ex.Message);
        }

        ReloadCore(recent, _seedRecent);
        ReloadCore(attention, _seedAttention);
        ReloadCore(browse1, _seedBrowse1);
        ReloadCore(browse2, _seedBrowse2);
        ApplyFilters();
    }

    private static void LoadRecentUsedRowsFromDb(string cn, int timeoutSeconds, List<RecentUsedRow> recent)
    {
        var dtRecent = App.aps.pda.GetDataTable(
            cn,
            App.aps.sql.SelectLocalRptRecentlyUsedReportsForUser(
                App.aps.signedOnUserId,
                RecentlyUsedMaxDistinctReports),
            timeoutSeconds);
        foreach (DataRow row in dtRecent.Rows)
        {
            var code = DbCellString(row, "report_code").Trim();
            if (code.Length == 0)
                continue;
            var title = DbCellString(row, "descr").Trim();
            if (title.Length == 0)
                title = code;
            var glyph = RptDashboardIconCatalog.ResolveGlyph(DbCellString(row, "icon_glyph_id"));
            var accessedUtc = DbCellDateTimeUtc(row, "last_accessed_ts");
            var lastRun = FormatLastRunCaptionUtc(accessedUtc);
            if (lastRun.Length == 0)
                lastRun = RptThemeString("Rpt.LastRun.UnknownCaption");
            var cat = DbCellString(row, "category_code").Trim();
            recent.Add(new RecentUsedRow(new ExecutableReportRef(code, title, glyph), lastRun, AccentFromReportPrimaryHexRow(row))
            {
                LastAccessedUtc = accessedUtc,
                CategoryId = cat.Length == 0 ? null : cat,
            });
        }
    }

    private static List<BrowseGroupTile> LoadBrowseRowFromDb(string cn, int browseRow, int timeoutSeconds)
    {
        var list = new List<BrowseGroupTile>();
        var dt = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectLocalRptCategoriesForDashboardBrowseRow(browseRow), timeoutSeconds);
        foreach (DataRow cat in dt.Rows)
        {
            var groupId = DbCellString(cat, "category_code").Trim();
            if (groupId.Length == 0)
                continue;
            var title = DbCellString(cat, "descr").Trim();
            if (title.Length == 0)
                title = groupId;
            var panel = DbCellString(cat, "browse_panel_descr").Trim();
            if (panel.Length == 0)
                panel = title;
            var tile = new BrowseGroupTile(
                groupId,
                title,
                panel,
                DbCellInt(cat, "browse_tile_report_count", 0),
                AccentFromCategoryHexRow(cat),
                RptDashboardIconCatalog.ResolveGlyph(DbCellString(cat, "browse_icon_glyph_id")),
                DbCellBool(cat, "browse_show_chevron", fallback: false));
            var sub = App.aps.pda.GetDataTable(cn, App.aps.sql.SelectLocalRptReportsForBrowseSubgroup(groupId), timeoutSeconds);
            tile.ReportsInGroup = MapBrowseReportsFromDataTable(sub);
            list.Add(tile);
        }

        return list;
    }

    private static IReadOnlyList<ExecutableReportRef> MapBrowseReportsFromDataTable(DataTable dt)
    {
        var list = new List<ExecutableReportRef>();
        foreach (DataRow r in dt.Rows)
        {
            var id = DbCellString(r, "report_code").Trim();
            if (id.Length == 0)
                continue;
            var dn = DbCellString(r, "descr").Trim();
            if (dn.Length == 0)
                dn = id;
            list.Add(new ExecutableReportRef(id, dn, RptDashboardIconCatalog.ResolveGlyph(DbCellString(r, "icon_glyph_id"))));
        }

        return list;
    }

    private static DateTime DbCellDateTimeUtc(DataRow r, string columnName)
    {
        var v = DbRaw(r, columnName);
        if (v == null || v == DBNull.Value)
            return DateTime.UtcNow;
        if (v is DateTime dt)
        {
            return dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            };
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim();
        if (!string.IsNullOrEmpty(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed.ToUniversalTime();
        return DateTime.UtcNow;
    }

    /// <summary>Human-readable &quot;Last run: …&quot; line from audit timestamp (UTC).</summary>
    private static string FormatLastRunCaptionUtc(DateTime accessedUtc)
    {
        var prefix = RptThemeString("Rpt.LastRun.Prefix");
        var a = accessedUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(accessedUtc, DateTimeKind.Utc)
            : accessedUtc.ToUniversalTime();
        var now = DateTime.UtcNow;

        if (a.TimeOfDay == TimeSpan.Zero && a.Date == now.Date)
            return prefix + RptThemeString("Rpt.LastRun.Today");

        var delta = now - a;
        if (delta < TimeSpan.Zero)
            delta = TimeSpan.Zero;

        if (delta.TotalMinutes < 2)
            return prefix + RptThemeString("Rpt.LastRun.JustNow");

        if (a.Date == now.Date.AddDays(-1))
            return prefix + RptThemeString("Rpt.LastRun.Yesterday");

        if (a.Date == now.Date && delta.TotalHours < 24)
        {
            if (delta.TotalHours >= 1)
            {
                var h = Math.Max(1, (int)Math.Floor(delta.TotalHours));
                var hs = RptThemeFormat("Rpt.LastRun.HoursAgoFormat", h);
                return hs.Length > 0 ? prefix + hs : prefix + h.ToString(CultureInfo.InvariantCulture) + "h";
            }

            var m = Math.Max(1, (int)Math.Floor(delta.TotalMinutes));
            var ms = RptThemeFormat("Rpt.LastRun.MinutesAgoFormat", m);
            return ms.Length > 0 ? prefix + ms : prefix + m.ToString(CultureInfo.InvariantCulture) + "m";
        }

        var calDays = (now.Date - a.Date).Days;
        if (calDays >= 1)
        {
            var ds = RptThemeFormat("Rpt.LastRun.DaysAgoFormat", calDays);
            return ds.Length > 0 ? prefix + ds : prefix + calDays.ToString(CultureInfo.InvariantCulture) + "d";
        }

        return prefix + a.ToString("MMM d", CultureInfo.InvariantCulture);
    }

    private static CardAccent AccentFromReportPrimaryHexRow(DataRow r)
    {
        var h1 = DbCellString(r, "ui_icon_backdrop_hex");
        var h2 = DbCellString(r, "ui_icon_foreground_hex");
        var h3 = DbCellString(r, "ui_hover_border_hex");
        var h4 = DbCellString(r, "ui_hover_surface_hex");
        var ch = DbCellString(r, "ui_chevron_hot_hex");
        var chOpt = string.IsNullOrWhiteSpace(ch) ? null : ch;
        if (CardAccent.TryFromHex(h1, h2, h3, h4, chOpt, out var ac) && ac != null)
            return ac;
        return DashboardUiFallbackAccentFromTheme();
    }

    private static CardAccent AccentFromReportBadgeHexRow(DataRow r)
    {
        var h1 = DbCellString(r, "ui_badge_icon_backdrop_hex");
        if (string.IsNullOrWhiteSpace(h1))
            return AccentFromReportPrimaryHexRow(r);
        var h2 = DbCellString(r, "ui_badge_icon_foreground_hex");
        var h3 = DbCellString(r, "ui_badge_hover_border_hex");
        var h4 = DbCellString(r, "ui_badge_hover_surface_hex");
        var ch = DbCellString(r, "ui_badge_chevron_hot_hex");
        var chOpt = string.IsNullOrWhiteSpace(ch) ? null : ch;
        if (CardAccent.TryFromHex(h1, h2, h3, h4, chOpt, out var ac) && ac != null)
            return ac;
        return AccentFromReportPrimaryHexRow(r);
    }

    private static CardAccent AccentFromCategoryHexRow(DataRow r)
    {
        var h1 = DbCellString(r, "ui_icon_backdrop_hex");
        var h2 = DbCellString(r, "ui_icon_foreground_hex");
        var h3 = DbCellString(r, "ui_hover_border_hex");
        var h4 = DbCellString(r, "ui_hover_surface_hex");
        var ch = DbCellString(r, "ui_chevron_hot_hex");
        var chOpt = string.IsNullOrWhiteSpace(ch) ? null : ch;
        if (CardAccent.TryFromHex(h1, h2, h3, h4, chOpt, out var ac) && ac != null)
            return ac;
        return DashboardUiFallbackAccentFromTheme();
    }

    private static object? DbRaw(DataRow r, string columnName)
    {
        foreach (DataColumn c in r.Table.Columns)
        {
            if (string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                return r.IsNull(c) ? null : r[c];
        }

        return null;
    }

    private static string DbCellString(DataRow r, string columnName)
    {
        var v = DbRaw(r, columnName);
        return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
    }

    private static bool DbCellBool(DataRow r, string columnName, bool fallback)
    {
        var v = DbRaw(r, columnName);
        if (v == null)
            return fallback;
        if (v is bool b)
            return b;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (s.Length == 0)
            return fallback;
        if (bool.TryParse(s, out var p))
            return p;
        if (s == "1" || s.Equals("t", StringComparison.OrdinalIgnoreCase))
            return true;
        if (s == "0" || s.Equals("f", StringComparison.OrdinalIgnoreCase))
            return false;
        return fallback;
    }

    private static int DbCellInt(DataRow r, string columnName, int fallback)
    {
        var v = DbRaw(r, columnName);
        if (v == null)
            return fallback;
        if (v is int i)
            return i;
        if (v is long l)
        {
            try
            {
                return checked((int)l);
            }
            catch
            {
                return fallback;
            }
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static int? DbCellNullableInt(DataRow r, string columnName)
    {
        var v = DbRaw(r, columnName);
        if (v == null)
            return null;
        if (v is int i)
            return i;
        if (v is long l)
        {
            try
            {
                return checked((int)l);
            }
            catch
            {
                return null;
            }
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    #endregion

    #region Icons (frozen geometry — Lucide-aligned path data, 24×24 viewbox)

    private static Geometry FreezeGeometry(string pathData)
    {
        var g = Geometry.Parse(pathData);
        if (g.CanFreeze)
            g.Freeze();
        return g;
    }

    private static Geometry FreezeGroup(params string[] pathDataParts)
    {
        var gg = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var part in pathDataParts)
            gg.Children.Add(Geometry.Parse(part));
        if (gg.CanFreeze)
            gg.Freeze();
        return gg;
    }

    private static Geometry BuildIconGeometry(DashboardIconGlyph kind)
    {
        return kind switch
        {
            // Lucide chart-column
            DashboardIconGlyph.BarChart => FreezeGroup(
                "M3 3 v16 a2 2 0 0 0 2 2 h16",
                "M18 17 V9",
                "M13 17 V5",
                "M8 17 v-3"),

            DashboardIconGlyph.Document => FreezeGeometry("M8,4 L20,4 L20,24 L6,24 L6,12 L8,4 M8,12 L6,12"),
            DashboardIconGlyph.WasteBin => FreezeGeometry(
                "M10,10 L22,10 M9,10 L10,26 L22,26 L23,10 M12,10 L13,6 L19,6 L20,10"),

            // StaffAccessUserDetails — delete user (Lucide trash), stroke colour comes from card theme
            DashboardIconGlyph.StaffDeleteUserTrash => FreezeGroup(
                "M10 11 v6",
                "M14 11 v6",
                "M19 6 v14 a2 2 0 0 1 -2 2 H7 a2 2 0 0 1 -2 -2 V6",
                "M3 6 h18",
                "M8 6 V4 a2 2 0 0 1 2 -2 h4 a2 2 0 0 1 2 2 v2"),

            // StaffAccessUserDetails — Documents tab (file with lines)
            DashboardIconGlyph.StaffDocumentsTab => FreezeGroup(
                "M15 2 H6 A2 2 0 0 0 4 4 V20 A2 2 0 0 0 6 22 H18 A2 2 0 0 0 20 20 V7 Z",
                "M14 2 L14 6 A2 2 0 0 0 16 8 L20 8",
                "M10 9 L8 9",
                "M16 13 L8 13",
                "M16 17 L8 17"),

            // Lucide circle-alert
            DashboardIconGlyph.AlertCircle => FreezeGroup(
                "M12 2 A10 10 0 1 1 12 22 A10 10 0 1 1 12 2",
                "M12 8 L12 12",
                "M12 16 L12.01 16"),

            DashboardIconGlyph.Box => FreezeGeometry(
                "M10,12 L24,12 L24,26 L8,26 L8,12 M10,12 L17,7 L24,12 M17,7 L17,12"),

            // Lucide package
            DashboardIconGlyph.Package => FreezeGroup(
                "M11 21.73 a2 2 0 0 0 2 0 l7 -4 A2 2 0 0 0 21 16 V8 a2 2 0 0 0 -1 -1.73 l-7 -4 a2 2 0 0 0 -2 0 l-7 4 A2 2 0 0 0 3 8 v8 a2 2 0 0 0 1 1.73 Z",
                "M12 22 V12",
                "M3.29 7 L12 12 L20.71 7",
                "M7.5 4.27 l9 5.15"),

            // Lucide trending-down
            DashboardIconGlyph.TrendDown => FreezeGroup(
                "M22 17 L13.5 8.5 L8.5 13.5 L2 7",
                "M16 17 L22 17 L22 11"),

            // Lucide coins
            DashboardIconGlyph.Coins => FreezeGroup(
                "M8 2 A6 6 0 1 1 8 14 A6 6 0 1 1 8 2",
                "M18.09 10.37 A6 6 0 1 1 10.34 18",
                "M7 6 h1 v4",
                "M16.71 13.88 l0.7 0.71 -2.82 2.82"),

            // Lucide truck
            DashboardIconGlyph.Truck => FreezeGroup(
                "M14 18 V6 a2 2 0 0 0 -2 -2 H4 a2 2 0 0 0 -2 2 v11 a1 1 0 0 0 1 1 h2",
                "M15 18 H9",
                "M19 18 h2 a1 1 0 0 0 1 -1 v-3.65 a1 1 0 0 0 -0.22 -0.624 l-3.48 -4.35 A1 1 0 0 0 17.52 8 H14",
                "M19 18 A2 2 0 1 1 15 18 A2 2 0 1 1 19 18",
                "M9 18 A2 2 0 1 1 5 18 A2 2 0 1 1 9 18"),

            // Lucide chart-pie
            DashboardIconGlyph.PieChart => FreezeGroup(
                "M21 12 c0.552 0 1.005 -0.449 0.95 -0.998 a10 10 0 0 0 -8.953 -8.951 c-0.55 -0.055 -0.998 0.398 -0.998 0.95 v8 a1 1 0 0 0 1 1 Z",
                "M21.21 15.89 A10 10 0 1 1 8 2.83"),

            // Lucide shield-check
            DashboardIconGlyph.ShieldCheck => FreezeGroup(
                "M20 13 c0 5 -3.5 7.5 -7.66 8.95 a1 1 0 0 1 -0.67 -0.01 C7.5 20.5 4 18 4 13 V6 a1 1 0 0 1 1 -1 c2 0 4.5 -1.2 6.24 -2.72 a1.17 1.17 0 0 1 1.52 0 C14.51 3.81 17 5 19 5 a1 1 0 0 1 1 1 Z",
                "M9 12 l2 2 4 -4"),

            DashboardIconGlyph.CubeStock => FreezeGeometry(
                "M10,16 L18,12 L26,16 L18,20 Z M10,16 L10,24 L18,28 L18,20 M26,16 L26,24 L18,28"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    #endregion

    private void RecentReport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not RecentUsedRow m)
            return;

        TryRecordReportAccess(m.Report.Id);
        if (string.Equals(m.Report.Id, DailySalesReportCode, StringComparison.Ordinal))
        {
            OpenDailySalesOverlay();
            return;
        }

        if (string.Equals(m.Report.Id, VatSummaryReportCode, StringComparison.Ordinal))
        {
            OpenVatSummaryOverlay();
            return;
        }

        if (string.Equals(m.Report.Id, VoidsReportCode, StringComparison.Ordinal))
        {
            OpenVoidsReportOverlay();
            return;
        }

        if (string.Equals(m.Report.Id, WastageReportCode, StringComparison.Ordinal))
        {
            OpenWastageReportOverlay();
            return;
        }

        InvokeIfNotNull(_onRecentReport, m);
    }

    private void AttentionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not AttentionNeededRow m)
            return;

        TryRecordReportAccess(m.Report.Id);
        if (string.Equals(m.Report.Id, DailySalesReportCode, StringComparison.Ordinal))
        {
            OpenDailySalesOverlay();
            return;
        }

        if (string.Equals(m.Report.Id, VatSummaryReportCode, StringComparison.Ordinal))
        {
            OpenVatSummaryOverlay();
            return;
        }

        if (string.Equals(m.Report.Id, VoidsReportCode, StringComparison.Ordinal))
        {
            OpenVoidsReportOverlay();
            return;
        }

        if (string.Equals(m.Report.Id, WastageReportCode, StringComparison.Ordinal))
        {
            OpenWastageReportOverlay();
            return;
        }

        InvokeIfNotNull(_onAttentionItem, m);
    }

    private void ReportGroupChrome_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not BrowseGroupTile g)
            return;

        InvokeIfNotNull(_onBrowseGroupOrViewReports, g);
    }

    private void BrowseViewReports_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement host || host.DataContext is not BrowseGroupTile group)
            return;

        InvokeIfNotNull(_onBrowseGroupOrViewReports, group);
        e.Handled = true;
    }
}
