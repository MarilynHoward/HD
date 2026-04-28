using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RestaurantPosWpf;

/// <summary>
/// Reporting dashboard: filters, recently used report executions, attention queue, and browse groupings.
/// Models, seed data, and geometry helpers are defined in this code-behind per framework standards.
/// </summary>
public sealed partial class RptDashboardMain : UserControl
{
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
        WireFilterDropdownsFromDummyData();
    }

    private void RptDashboardMain_Loaded(object sender, RoutedEventArgs e)
    {
        if (DpReportRangeStart.SelectedDate == null)
            DpReportRangeStart.SelectedDate = DateTime.Today.AddDays(-30);
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

        var maxDate = Math.Min(440 * scale, Math.Max(148 * scale, w * 0.34));
        if (Math.Abs(maxDate - FilterStripDateMaxWidth) > 0.5)
            FilterStripDateMaxWidth = maxDate;
    }

    /// <summary>
    /// Fits cards to strip width: column count = min(item count, floor((width + gap) / (scaled min card + gap))).
    /// Uses the same base min widths as the previous fixed cards (268 / 292 / 340) × <see cref="UiScaleState.FontScale"/>.
    /// </summary>
    private void RefreshDashboardCardUniformColumns()
    {
        var recentW = RecentCardStripHost?.ActualWidth ?? 0;
        var nextRecent = ComputeUniformColumns(recentW, RecentReports.Count, minCardBaseWidth: 268, horizontalGapBase: 10);
        if (nextRecent != RecentCardUniformColumns)
            RecentCardUniformColumns = nextRecent;

        var attW = AttentionCardStripHost?.ActualWidth ?? 0;
        var nextAtt = ComputeUniformColumns(attW, AttentionItems.Count, minCardBaseWidth: 292, horizontalGapBase: 10);
        if (nextAtt != AttentionCardUniformColumns)
            AttentionCardUniformColumns = nextAtt;

        var b1W = BrowseRow1CardStripHost?.ActualWidth ?? 0;
        var nextB1 = ComputeUniformColumns(b1W, ReportGroupsRow1.Count, minCardBaseWidth: 340, horizontalGapBase: 10);
        if (nextB1 != BrowseRow1CardUniformColumns)
            BrowseRow1CardUniformColumns = nextB1;

        var b2W = BrowseRow2CardStripHost?.ActualWidth ?? 0;
        var nextB2 = ComputeUniformColumns(b2W, ReportGroupsRow2.Count, minCardBaseWidth: 340, horizontalGapBase: 10);
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

    private static double ReadUiFontScale()
    {
        return Application.Current?.TryFindResource("UiScaleState") is UiScaleState s ? s.FontScale : 1.25;
    }

    private void DpReportRangeStart_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateDateRangeLine();
    }

    private void UpdateDateRangeLine()
    {
        var start = DpReportRangeStart.SelectedDate?.Date ?? DateTime.Today.AddDays(-30);
        var end = start.AddDays(30);
        TxtReportDateRangePill.Text = $"Date Range: {start:MMM d} - {end:MMM d, yyyy}";
    }

    public ObservableCollection<RecentUsedRow> RecentReports { get; } = new();

    public ObservableCollection<AttentionNeededRow> AttentionItems { get; } = new();

    public ObservableCollection<BrowseGroupTile> ReportGroupsRow1 { get; } = new();

    public ObservableCollection<BrowseGroupTile> ReportGroupsRow2 { get; } = new();

    public void ReloadDummyDataIntoCollections()
    {
        ReloadCore(BuildRecentRows(), _seedRecent);
        ReloadCore(BuildAttentionRows(), _seedAttention);
        ReloadCore(BuildBrowseRow1(), _seedBrowse1);
        ReloadCore(BuildBrowseRow2(), _seedBrowse2);
        ApplyFilters();
    }

    private static void ReloadCore<T>(IEnumerable<T> source, ICollection<T> target)
    {
        target.Clear();
        foreach (var row in source)
            target.Add(row);
    }

    private void WireFilterDropdownsFromDummyData()
    {
        BranchFilterCombo.DisplayMemberPath = nameof(FilterOption.Label);
        BranchFilterCombo.ItemsSource = BranchOptions.ToList();
        BranchFilterCombo.SelectedIndex = 0;

        ChannelFilterCombo.DisplayMemberPath = nameof(FilterOption.Label);
        ChannelFilterCombo.ItemsSource = ChannelOptions.ToList();
        ChannelFilterCombo.SelectedIndex = 0;

        RoleFilterCombo.DisplayMemberPath = nameof(RoleFilterOption.Label);
        RoleFilterCombo.ItemsSource = RoleOptions.ToList();
        RoleFilterCombo.SelectedIndex = 0;
    }

    /// <summary>Search filters dummy lists; combo filters reserved for branch/channel/role once reporting data is wired.</summary>
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
    /// Card colours as ARGB/RGB hex strings — shape matches five SQL text columns. Does not use
    /// <see cref="PeoplePosTheme"/>; report tiles get colours from seed/data only.
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

    /// <summary>Seed row for Recently Used — expand <see cref="RecentReportSeedCatalog"/> to add reports.</summary>
    public sealed record RptRecentReportSeed(
            string ReportId,
            string DisplayName,
            string LastRunDisplay,
            string? CategoryId,
            TimeSpan LastAccessedUtcOffsetFromNow,
            /// <summary>Five hex colours per card — same fields can be loaded from PostgreSQL later; no XAML theme edits.</summary>
            RptDashboardCardThemeHex ThemeHex,
            /// <summary><see cref="RptDashboardIconCatalog"/> id (e.g. <c>staff_documents_tab</c>).</summary>
            string IconGlyphId,
            bool LastAccessedAtStartOfTodayUtc = false);

    /// <summary>Seed row for Attention Needed.</summary>
    public sealed record RptAttentionReportSeed(
            string ReportId,
            string DisplayName,
            int AttentionCount,
            RptDashboardCardThemeHex IconThemeHex,
            RptDashboardCardThemeHex BadgeThemeHex,
            string IconGlyphId);

    /// <summary>Nested report under a browse group — include icon for each row when UI lists expand.</summary>
    public sealed record RptBrowseReportSeed(
            string ReportId,
            string DisplayName,
            string IconGlyphId);

    /// <summary>Seed row for Browse grouping tiles.</summary>
    public sealed record RptBrowseGroupSeed(
            string GroupId,
            string Title,
            string Description,
            int ReportCount,
            RptDashboardCardThemeHex ThemeHex,
            string IconGlyphId,
            bool ShowChevronNextToTitle,
            IReadOnlyList<RptBrowseReportSeed>? ReportsInGroup);

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

    #region Seed lists (full hex literals per row below — five strings per report, same shape as future PostgreSQL columns)

    private static readonly FilterOption[] BranchOptions =
    {
        new("all", "All Branches"),
        new("sandton", "Sandton"),
        new("cpt", "Cape Town CBD"),
        new("umhlanga", "Umhlanga"),
        new("pta", "Pretoria"),
        new("jhb", "Johannesburg"),
        new("durban", "Durban"),
    };

    private static readonly FilterOption[] ChannelOptions =
    {
        new("all", "All Channels"),
        new("dinein", "Dine-In"),
        new("takeaway", "Takeaway"),
        new("delivery", "Delivery"),
        new("online", "Online Orders"),
        new("ubereats", "Uber Eats"),
        new("mrd", "Mr D Food"),
    };

    private static readonly RoleFilterOption[] RoleOptions =
    {
        new("all", "All Users"),
        new("waiters", "Waiters"),
        new("cashiers", "Cashiers"),
        new("managers", "Managers"),
        new("kitchen", "Kitchen Staff"),
        new("drivers", "Drivers"),
    };

    private static readonly RptRecentReportSeed[] RecentReportSeedCatalog =
    {
        new(
            "rpt.daily_sales",
            "Daily Sales Summary",
            "Last run: 2 hours ago",
            "grp.sales",
            TimeSpan.FromHours(-2),
            new RptDashboardCardThemeHex("#DBEAFE", "#1D4ED8", "#3B82F6", "#E6F3FF", "#334155"),
            "bar_chart"),
        new(
            "rpt.vat_summary",
            "VAT Summary",
            "Last run: Yesterday",
            "grp.financial",
            TimeSpan.FromDays(-1),
            new RptDashboardCardThemeHex("#E6D5FF", "#C95BFF", "#C7B1DA", "#F7F6FC", "#334155"),
            "staff_documents_tab"),
        new(
            "rpt.wastage",
            "Wastage Report",
            "Last run: 3 days ago",
            "grp.stock",
            TimeSpan.FromDays(-3),
            new RptDashboardCardThemeHex("#CCFBF1", "#0F766E", "#14B8A6", "#E8FFFA", "#334155"),
            "staff_delete_user_trash"),
        new(
            "rpt.voids",
            "Voids Report",
            "Last run: Today",
            "grp.ops",
            TimeSpan.Zero,
            new RptDashboardCardThemeHex("#FEE2E2", "#B91C1C", "#EF4444", "#FEF2F2", "#334155"),
            "alert_circle",
            LastAccessedAtStartOfTodayUtc: true),
    };

    private static readonly RptAttentionReportSeed[] AttentionReportSeedCatalog =
    {
        new(
            "rpt.high_value_voids",
            "High-Value Voids",
            3,
            new RptDashboardCardThemeHex("#FEE2E2", "#B91C1C", "#EF4444", "#FEF2F2", "#334155"),
            new RptDashboardCardThemeHex("#FEE2E2", "#B91C1C", "#EF4444", "#FEF2F2", "#334155"),
            "alert_circle"),
        new(
            "rpt.low_stock",
            "Low Stock Items",
            8,
            new RptDashboardCardThemeHex("#FFEDD5", "#9A3412", "#EA580C", "#FFF7ED", "#334155"),
            new RptDashboardCardThemeHex("#FFEDD5", "#9A3412", "#EA580C", "#FFF7ED", "#334155"),
            "package"),
        new(
            "rpt.delivery_variances",
            "Delivery Variances",
            2,
            new RptDashboardCardThemeHex("#CCFBF1", "#0F766E", "#14B8A6", "#E8FFFA", "#334155"),
            new RptDashboardCardThemeHex("#FFEDD5", "#9A3412", "#EA580C", "#FFF7ED", "#334155"),
            "trend_down"),
        new(
            "rpt.till_balance",
            "Till Not Balanced",
            1,
            new RptDashboardCardThemeHex("#FEF9C3", "#A16207", "#EAB308", "#FEFCE8", "#334155"),
            new RptDashboardCardThemeHex("#FEE2E2", "#B91C1C", "#EF4444", "#FEF2F2", "#334155"),
            "coins"),
    };

    private static readonly RptBrowseGroupSeed[] BrowseGroupsRow1SeedCatalog =
    {
        new(
            "grp.sales",
            "Sales Reports",
            "Track daily sales, revenue and product performance.",
            5,
            new RptDashboardCardThemeHex("#DBEAFE", "#1D4ED8", "#3B82F6", "#E6F3FF", "#334155"),
            "bar_chart",
            false,
            new RptBrowseReportSeed[]
            {
                new("rpt.daily_sales", "Daily Sales Summary", "bar_chart"),
                new("rpt.revenue", "Revenue", "pie_chart"),
            }),
        new(
            "grp.stock",
            "Stock Reports",
            "Monitor inventory levels, usage and variance.",
            5,
            new RptDashboardCardThemeHex("#CCFBF1", "#0F766E", "#14B8A6", "#E8FFFA", "#334155"),
            "package",
            true,
            new RptBrowseReportSeed[] { new("rpt.wastage", "Wastage Report", "staff_delete_user_trash"), }),
        new(
            "grp.ops",
            "Operational Control",
            "Monitor voids, refunds and operational compliance.",
            5,
            new RptDashboardCardThemeHex("#FFE4E6", "#BE123C", "#E02424", "#FDF2F4", "#334155"),
            "shield_check",
            false,
            new RptBrowseReportSeed[] { new("rpt.voids", "Voids Report", "alert_circle"), }),
    };

    private static readonly RptBrowseGroupSeed[] BrowseGroupsRow2SeedCatalog =
    {
        new(
            "grp.procurement",
            "Supplier & Procurement",
            "Track purchases, supplier performance and delivery.",
            5,
            new RptDashboardCardThemeHex("#FFEDD5", "#C2410C", "#E8A05B", "#FDF8F2", "#334155"),
            "truck",
            false,
            Array.Empty<RptBrowseReportSeed>()),
        new(
            "grp.profitability",
            "Profitability Reports",
            "Analyse margins, costs and profitability metrics.",
            5,
            new RptDashboardCardThemeHex("#EDE9FE", "#6D28D9", "#8B5CF6", "#F5F3FF", "#334155"),
            "pie_chart",
            true,
            Array.Empty<RptBrowseReportSeed>()),
    };

    private static IEnumerable<RecentUsedRow> BuildRecentRows()
    {
        foreach (var s in RecentReportSeedCatalog)
        {
            var lastAccessedUtc = s.LastAccessedAtStartOfTodayUtc
                ? DateTime.UtcNow.Date
                : DateTime.UtcNow + s.LastAccessedUtcOffsetFromNow;

            var icon = RptDashboardIconCatalog.ResolveGlyph(s.IconGlyphId);
            yield return new RecentUsedRow(
                    new ExecutableReportRef(s.ReportId, s.DisplayName, icon),
                    s.LastRunDisplay,
                    s.ThemeHex.ToCardAccent())
            {
                LastAccessedUtc = lastAccessedUtc,
                CategoryId = s.CategoryId,
            };
        }
    }

    private static IEnumerable<AttentionNeededRow> BuildAttentionRows()
    {
        foreach (var s in AttentionReportSeedCatalog)
        {
            var icon = RptDashboardIconCatalog.ResolveGlyph(s.IconGlyphId);
            yield return new AttentionNeededRow(
                    new ExecutableReportRef(s.ReportId, s.DisplayName, icon),
                    s.AttentionCount,
                    s.IconThemeHex.ToCardAccent(),
                    s.BadgeThemeHex.ToCardAccent());
        }
    }

    private static IEnumerable<BrowseGroupTile> BuildBrowseRow1()
    {
        foreach (var s in BrowseGroupsRow1SeedCatalog)
        {
            var tile = new BrowseGroupTile(
                    s.GroupId,
                    s.Title,
                    s.Description,
                    s.ReportCount,
                    s.ThemeHex.ToCardAccent(),
                    RptDashboardIconCatalog.ResolveGlyph(s.IconGlyphId),
                    s.ShowChevronNextToTitle);

            tile.ReportsInGroup = MapBrowseReportsInGroup(s.ReportsInGroup);
            yield return tile;
        }
    }

    private static IEnumerable<BrowseGroupTile> BuildBrowseRow2()
    {
        foreach (var s in BrowseGroupsRow2SeedCatalog)
        {
            var tile = new BrowseGroupTile(
                    s.GroupId,
                    s.Title,
                    s.Description,
                    s.ReportCount,
                    s.ThemeHex.ToCardAccent(),
                    RptDashboardIconCatalog.ResolveGlyph(s.IconGlyphId),
                    s.ShowChevronNextToTitle);

            tile.ReportsInGroup = MapBrowseReportsInGroup(s.ReportsInGroup);
            yield return tile;
        }
    }

    private static IReadOnlyList<ExecutableReportRef>? MapBrowseReportsInGroup(
            IReadOnlyList<RptBrowseReportSeed>? reportsInGroup)
    {
        if (reportsInGroup == null || reportsInGroup.Count == 0)
            return Array.Empty<ExecutableReportRef>();

        return reportsInGroup
            .Select(p => new ExecutableReportRef(p.ReportId, p.DisplayName, RptDashboardIconCatalog.ResolveGlyph(p.IconGlyphId)))
            .ToList();
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

        InvokeIfNotNull(_onRecentReport, m);
    }

    private void AttentionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not AttentionNeededRow m)
            return;

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
