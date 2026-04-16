using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public partial class OpsServicesFloorPlanDesign : UserControl
{
    private readonly Action _navigateToShiftScheduling;
    private readonly Action _navigateToTableManagement;
    private readonly Action<string?, DateOnly, OpsReservationListFilter, string?, Guid?> _openReservationsManagement;
    private readonly DispatcherTimer _searchDebounce = new() { Interval = TimeSpan.FromMilliseconds(200) };

    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private OpsReservationListFilter _listFilter = OpsReservationListFilter.All;
    private string _searchQuery = "";
    private bool _sidebarCollapsed;
    /// <summary>Actual <see cref="ScaleTransform"/> scale. UI percent is rebased so this value reads as 100%.</summary>
    private const double FloorPlanZoomDisplayBaseline = 0.8;
    private double _zoom = FloorPlanZoomDisplayBaseline;
    private Guid? _dragTableId;
    private Point _dragMouseStart;
    private Point _dragElemStart;
    private FrameworkElement? _dragElement;
    /// <summary>Max canvas delta (L1) from press to release to treat as a click (open manage) vs a drag reposition.</summary>
    private const double TableCardManageClickMoveTolerance = 8.0;

    private bool _canvasPanning;
    private Point _canvasPanMouseStart;
    private Point _canvasPanTranslateStart;
    private bool _storeRefreshPosted;
    private bool _floorPlanUnloaded;

    public OpsServicesFloorPlanDesign(
        Action navigateToShiftScheduling,
        Action navigateToTableManagement,
        Action<string?, DateOnly, OpsReservationListFilter, string?, Guid?> openReservationsManagement)
    {
        _navigateToShiftScheduling = navigateToShiftScheduling ?? throw new ArgumentNullException(nameof(navigateToShiftScheduling));
        _navigateToTableManagement = navigateToTableManagement ?? throw new ArgumentNullException(nameof(navigateToTableManagement));
        _openReservationsManagement = openReservationsManagement ?? throw new ArgumentNullException(nameof(openReservationsManagement));
        _searchDebounce.Tick += SearchDebounce_Tick;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _floorPlanUnloaded = false;
        OpsServicesStore.EnsureSeeded();
        OpsServicesStore.DataChanged += OnStoreChanged;
        var today = DateTime.Today;
        _selectedDate = DateOnly.FromDateTime(today);
        DpFilterDate.DisplayDate = today;
        DpFilterDate.SelectedDate = today;
        RefreshFloorCombo(resetSelection: true);
        ReservationColumn.Width = new GridLength(S(320));
        HighlightFloorPill(true);
        ApplyZoomTransform();
        RefreshAll();
        UpdateExpandToggleVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _floorPlanUnloaded = true;
        OpsServicesStore.DataChanged -= OnStoreChanged;
        _searchDebounce.Stop();
        _searchDebounce.Tick -= SearchDebounce_Tick;
    }

    /// <summary>
    /// Avoid <see cref="Dispatcher.Invoke"/> here: opening Reservations (Manage) keeps this control loaded behind
    /// the modal; synchronous refresh with other subscribers can freeze the UI.
    /// </summary>
    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (_floorPlanUnloaded || _storeRefreshPosted)
            return;
        _storeRefreshPosted = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _storeRefreshPosted = false;
            if (_floorPlanUnloaded)
                return;
            RefreshAll();
        }), DispatcherPriority.DataBind);
    }

    private double S(double basePx)
    {
        var state = Application.Current.Resources["UiScaleState"] as UiScaleState;
        return basePx * (state?.FontScale ?? 1.25);
    }

    private void HighlightFloorPill(bool floorSelected)
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

        if (floorSelected)
        {
            SetInactive(PillShiftFloor);
            SetActive(PillFloorPlanNav);
            SetInactive(PillTableFloor);
        }
        else
        {
            SetInactive(PillShiftFloor);
            SetInactive(PillFloorPlanNav);
            SetInactive(PillTableFloor);
        }
    }

    private static Brush TryBrush(string? key, string fallbackHex)
    {
        if (!string.IsNullOrEmpty(key) && Application.Current.TryFindResource(key) is Brush b)
            return b;
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex)!);
    }

    private void RefreshFloorCombo(bool resetSelection)
    {
        var floors = OpsServicesStore.GetDistinctFloorNamesForFilter().ToList();
        CmbFloorLocation.ItemsSource = floors;
        if (floors.Count == 0)
            return;
        if (resetSelection || CmbFloorLocation.SelectedItem is not string sel || !floors.Contains(sel))
            CmbFloorLocation.SelectedItem = floors[0];
    }

    private string? SelectedFloor =>
        CmbFloorLocation.SelectedItem as string;

    private void RefreshAll()
    {
        if (!IsLoaded)
            return;
        RefreshFloorCombo(resetSelection: false);
        RefreshFilterButtons();
        RebuildReservationList();
        RebuildFloorCanvas();
        UpdateLegend();
        UpdateZoomLabel();
    }

    private void RefreshFilterButtons()
    {
        // Inactive: hover + shadow. Active: original solid colours + white text; only add table-list–style selected shadow (Effect).
        var inactiveStyle = TryFindResource("OpsFloorPlanFilterPillButtonStyle") as Style;
        var activeStyle = TryFindResource("OpsFloorPlanFilterPillActiveButtonStyle") as Style;
        if (inactiveStyle == null || activeStyle == null)
            return;

        BtnFilterAll.Style = _listFilter == OpsReservationListFilter.All ? activeStyle : inactiveStyle;
        BtnFilterReserved.Style = _listFilter == OpsReservationListFilter.Reserved ? activeStyle : inactiveStyle;
        BtnFilterOccupied.Style = _listFilter == OpsReservationListFilter.Occupied ? activeStyle : inactiveStyle;

        void ClearLocalChrome(Button b)
        {
            b.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            b.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
            b.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            b.ClearValue(UIElement.EffectProperty);
            b.FontWeight = FontWeights.Normal;
        }

        void PaintActiveSolidWithElevation(Button b, Brush solidFill)
        {
            b.Background = solidFill;
            b.BorderBrush = solidFill;
            b.Foreground = Brushes.White;
            b.FontWeight = FontWeights.SemiBold;
            b.Effect = TryFindResource("OpsFloorPlanFilterPillSelectedShadow") as Effect;
        }

        ClearLocalChrome(BtnFilterAll);
        ClearLocalChrome(BtnFilterReserved);
        ClearLocalChrome(BtnFilterOccupied);

        switch (_listFilter)
        {
            case OpsReservationListFilter.All:
                PaintActiveSolidWithElevation(BtnFilterAll, TryBrush("Brush.PrimaryBlue", "#2563EB"));
                break;
            case OpsReservationListFilter.Reserved:
                PaintActiveSolidWithElevation(BtnFilterReserved, TryBrush(key: null, "#AA48FF"));
                break;
            case OpsReservationListFilter.Occupied:
                PaintActiveSolidWithElevation(BtnFilterOccupied, TryBrush(key: null, "#FB2B39"));
                break;
        }
    }

    //private void RebuildReservationList()
    //{
    //    var floor = SelectedFloor;
    //    if (floor == null)
    //    {
    //        ReservationsItems.ItemsSource = Array.Empty<OpsReservation>();
    //        return;
    //    }

    //    var rows = OpsServicesStore.GetReservationsForList(_selectedDate, floor, _listFilter,
    //        string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery.Trim()).ToList();
    //    ReservationsItems.ItemsSource = rows;
    //}

    private void RebuildReservationList()
    {
        var floor = SelectedFloor;
        if (floor == null)
        {
            ReservationsItems.ItemsSource = Array.Empty<OpsReservation>();
            return;
        }

        var effectiveFilter = _listFilter switch
        {
            OpsReservationListFilter.Reserved => OpsReservationListFilter.Occupied,
            OpsReservationListFilter.Occupied => OpsReservationListFilter.Reserved,
            _ => _listFilter
        };

        var rows = OpsServicesStore.GetReservationsForList(_selectedDate, floor, effectiveFilter,
            string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery.Trim()).ToList();
        ReservationsItems.ItemsSource = rows;
    }


    private IReadOnlyList<OpsFloorTable> ActiveTablesOnFloor(string floor)
    {
        var f = OpsServicesStore.NormalizeFloorNamePublic(floor);
        return OpsServicesStore.GetTables()
            .Where(t => t.IsActive && string.Equals(
                OpsServicesStore.NormalizeFloorNamePublic(t.LocationName), f, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    //private IEnumerable<OpsFloorTable> TablesForFloorCanvas(IReadOnlyList<OpsFloorTable> tablesOnFloor)
    //{
    //    return _listFilter switch
    //    {
    //        OpsReservationListFilter.Reserved => tablesOnFloor.Where(t =>
    //            OpsServicesStore.GetFloorPlanTableVisualKind(_selectedDate, t.Id) == OpsFloorPlanTableVisualKind.Reserved),
    //        OpsReservationListFilter.Occupied => tablesOnFloor.Where(t =>
    //            OpsServicesStore.GetFloorPlanTableVisualKind(_selectedDate, t.Id) == OpsFloorPlanTableVisualKind.Occupied),
    //        _ => tablesOnFloor
    //    };
    //}

    private IEnumerable<OpsFloorTable> TablesForFloorCanvas(IReadOnlyList<OpsFloorTable> tablesOnFloor)
    {
        return _listFilter switch
        {
            OpsReservationListFilter.Reserved => tablesOnFloor.Where(t =>
                OpsServicesStore.GetFloorPlanTableVisualKind(_selectedDate, t.Id) == OpsFloorPlanTableVisualKind.Occupied),
            OpsReservationListFilter.Occupied => tablesOnFloor.Where(t =>
                OpsServicesStore.GetFloorPlanTableVisualKind(_selectedDate, t.Id) == OpsFloorPlanTableVisualKind.Reserved),
            _ => tablesOnFloor
        };
    }

    private void RebuildFloorCanvas()
    {
        FloorTablesCanvas.Children.Clear();
        var floor = SelectedFloor;
        if (floor == null)
            return;

        var tables = ActiveTablesOnFloor(floor);
        OpsServicesStore.EnsureDefaultFloorPlanPositions(_selectedDate, floor, tables);

        foreach (var t in TablesForFloorCanvas(tables))
        {
            if (!OpsServicesStore.TryGetFloorPlanTablePosition(_selectedDate, floor, t.Id, out var x, out var y))
                continue;

            var card = CreateTableCard(t, floor);
            Canvas.SetLeft(card, x);
            Canvas.SetTop(card, y);
            FloorTablesCanvas.Children.Add(card);
        }

        Dispatcher.BeginInvoke(new Action(ClampCanvasPan), DispatcherPriority.Loaded);
    }

    private Border CreateTableCard(OpsFloorTable table, string floor)
    {
        var kind = OpsServicesStore.GetFloorPlanTableVisualKind(_selectedDate, table.Id);
        var res = OpsServicesStore.GetActiveReservationForTable(_selectedDate, table.Id);
        var staff = OpsServicesStore.GetPrimaryStaffForTableOnDate(table.Id, _selectedDate);

        var (bg, borderBrush) = kind switch
        {
            OpsFloorPlanTableVisualKind.Reserved => ("#FEE2E2", "#EF4444"),
            OpsFloorPlanTableVisualKind.Occupied => ("#FFEDD5", "#EA580C"),
            _ => ("#DCFCE7", "#22C55E")
        };

        var outer = new Border
        {
            Width = S(176),
            MinHeight = S(118),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)!),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderBrush)!),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(S(10)),
            Cursor = System.Windows.Input.Cursors.SizeAll,
            SnapsToDevicePixels = false,
            UseLayoutRounding = false,
            ClipToBounds = false,
            Tag = table.Id
        };

        if (kind is OpsFloorPlanTableVisualKind.Reserved or OpsFloorPlanTableVisualKind.Occupied)
        {
            outer.Effect = new DropShadowEffect
            {
                BlurRadius = 14,
                Direction = 270,
                Opacity = 0.14,
                ShadowDepth = 3,
                Color = Colors.Black
            };
        }

        outer.MouseLeftButtonDown += TableCard_MouseLeftButtonDown;
        outer.MouseMove += TableCard_MouseMove;
        outer.MouseLeftButtonUp += TableCard_MouseLeftButtonUp;

        var inner = new StackPanel
        {
            Margin = new Thickness(S(12)),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        void AddRow(FrameworkElement fe, double topPad = 0)
        {
            if (topPad > 0)
                fe.Margin = new Thickness(0, topPad, 0, 0);
            inner.Children.Add(fe);
        }

        AddRow(NewFloorPlanCardText(table.Name, FontWeights.SemiBold, S(17), HorizontalAlignment.Center));

        var hasActiveReservation = res != null
            && (kind is OpsFloorPlanTableVisualKind.Reserved or OpsFloorPlanTableVisualKind.Occupied);

        if (hasActiveReservation && res != null)
        {
            var client = string.IsNullOrWhiteSpace(res.CustomerName) ? "—" : res.CustomerName.Trim();
            AddRow(NewFloorPlanCardText(client, FontWeights.Normal, S(14), HorizontalAlignment.Center), S(4));
            var timeStr = res.Time.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            AddRow(NewFloorPlanCardText(timeStr, FontWeights.Normal, S(13), HorizontalAlignment.Center), S(4));
            AddRow(BuildGuestsIconCountRow(res.PartySize), S(4));
        }
        else
        {
            AddRow(BuildGuestsIconCountRow(table.SeatCount), S(4));
        }

        var staffLine = string.IsNullOrWhiteSpace(staff)
            ? "Staff: —"
            : $"Staff: {staff}";
        AddRow(NewFloorPlanCardText(
                staffLine,
                FontWeights.Normal,
                S(11),
                HorizontalAlignment.Center,
                TryBrush("DimmedForeground", "#6B7280")),
            S(4));

        outer.Child = inner;
        return outer;
    }

    private static TextBlock NewFloorPlanCardText(
        string text,
        FontWeight weight,
        double fontPx,
        HorizontalAlignment horizontalAlignment,
        Brush? foreground = null)
    {
        var state = Application.Current.Resources["UiScaleState"] as UiScaleState;
        var scale = state?.FontScale ?? 1.25;
        return new TextBlock
        {
            Text = text,
            FontWeight = weight,
            FontSize = fontPx * scale,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = horizontalAlignment,
            Foreground = foreground
                       ?? (Application.Current.TryFindResource("MainForeground") as Brush ?? Brushes.Black)
        };
    }

    /// <summary>Lucide users icon + count (same strokes as reservation cards), scaled for floor plan.</summary>
    private StackPanel BuildGuestsIconCountRow(int count)
    {
        var row = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var stroke = Application.Current.TryFindResource("MainForeground") as Brush ?? Brushes.Black;
        var thick = S(1.75);

        var vb = new Viewbox
        {
            Width = S(15),
            Height = S(15),
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, S(6), 0)
        };
        var canvas = new Canvas { Width = 24, Height = 24, SnapsToDevicePixels = true };

        void AddPath(string data)
        {
            canvas.Children.Add(new Path
            {
                Data = Geometry.Parse(data),
                Stroke = stroke,
                StrokeThickness = thick,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            });
        }

        AddPath("M16,21 v-2 a4,4 0 0 0 -4,-4 H6 a4,4 0 0 0 -4,4 v2");
        var head = new Ellipse
        {
            Width = 8,
            Height = 8,
            Stroke = stroke,
            StrokeThickness = thick,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(head, 5);
        Canvas.SetTop(head, 3);
        canvas.Children.Add(head);
        AddPath("M22,21 v-2 a4,4 0 0 0 -3,-3.87");
        AddPath("M16,3.13 a4,4 0 0 1 0,7.75");

        vb.Child = canvas;
        row.Children.Add(vb);
        var countTb = NewFloorPlanCardText(
            count.ToString(CultureInfo.InvariantCulture),
            FontWeights.Normal,
            S(13),
            HorizontalAlignment.Left);
        countTb.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(countTb);
        return row;
    }

    private void TableCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not Guid id)
            return;
        _dragTableId = id;
        _dragElement = fe;
        _dragMouseStart = e.GetPosition(FloorTablesCanvas);
        _dragElemStart = new Point(Canvas.GetLeft(fe), Canvas.GetTop(fe));
        fe.CaptureMouse();
        e.Handled = true;
    }

    private void TableCard_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragTableId is not { } id || _dragElement == null || SelectedFloor is not { } floor)
            return;
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(FloorTablesCanvas);
        var dx = pos.X - _dragMouseStart.X;
        var dy = pos.Y - _dragMouseStart.Y;
        var nx = _dragElemStart.X + dx;
        var ny = _dragElemStart.Y + dy;
        var maxX = Math.Max(0, FloorTablesCanvas.ActualWidth - _dragElement.ActualWidth);
        var maxY = Math.Max(0, FloorTablesCanvas.ActualHeight - _dragElement.ActualHeight);
        nx = Math.Max(0, Math.Min(nx, maxX));
        ny = Math.Max(0, Math.Min(ny, maxY));
        Canvas.SetLeft(_dragElement, nx);
        Canvas.SetTop(_dragElement, ny);
        e.Handled = true;
    }

    private void TableCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragElement != null)
            _dragElement.ReleaseMouseCapture();

        Guid? focusReservationFromClick = null;
        if (_dragTableId is { } id && SelectedFloor is { } floor)
        {
            if (sender is FrameworkElement fe)
            {
                var nx = Canvas.GetLeft(fe);
                var ny = Canvas.GetTop(fe);
                var moved = Math.Abs(nx - _dragElemStart.X) + Math.Abs(ny - _dragElemStart.Y);
                if (moved < TableCardManageClickMoveTolerance)
                {
                    var kind = OpsServicesStore.GetFloorPlanTableVisualKind(_selectedDate, id);
                    if (kind is OpsFloorPlanTableVisualKind.Reserved or OpsFloorPlanTableVisualKind.Occupied)
                    {
                        var res = OpsServicesStore.GetActiveReservationForTable(_selectedDate, id);
                        if (res != null)
                            focusReservationFromClick = res.Id;
                    }
                }

                OpsServicesStore.SetFloorPlanTablePosition(_selectedDate, floor, id, nx, ny);
            }
        }

        _dragTableId = null;
        _dragElement = null;
        InvalidateFloorCanvasVisuals();

        if (focusReservationFromClick is { } rid)
            OpenManageReservations(rid);
    }

    private void InvalidateFloorCanvasVisuals()
    {
        FloorTablesCanvas?.InvalidateVisual();
        FloorCanvasContentRoot?.InvalidateVisual();
        FloorCanvasPanWrapper?.InvalidateVisual();
        FloorCanvasHostCanvas?.InvalidateVisual();
        FloorCanvasViewport?.InvalidateVisual();
    }

    /// <summary>
    /// Border passes the viewport size into Arrange; a direct Grid child would be layout-clipped to that rect,
    /// so zoomed content larger than the viewport could never be fully panned into view. Canvas arranges children
    /// at their full desired size, so the pan wrapper keeps the full scaled sheet in layout space.
    /// </summary>
    private void UpdateFloorCanvasPanWrapperLayoutSize()
    {
        if (FloorCanvasPanWrapper == null)
            return;
        var cw = FloorCanvasLogicalWidth * _zoom;
        var ch = FloorCanvasLogicalHeight * _zoom;
        FloorCanvasPanWrapper.Width = cw;
        FloorCanvasPanWrapper.Height = ch;
    }

    private void ApplyZoomTransform()
    {
        FloorZoomScale.ScaleX = _zoom;
        FloorZoomScale.ScaleY = _zoom;
        UpdateFloorCanvasPanWrapperLayoutSize();
        ClampCanvasPan();
        UpdateZoomLabel();
    }

    private const double FloorCanvasLogicalWidth = 2000;
    private const double FloorCanvasLogicalHeight = 1200;

    private void ClampCanvasPan()
    {
        if (FloorCanvasViewport == null)
            return;
        var vw = FloorCanvasViewport.ActualWidth;
        var vh = FloorCanvasViewport.ActualHeight;
        if (vw <= 0 || vh <= 0)
            return;
        var cw = FloorCanvasLogicalWidth * _zoom;
        var ch = FloorCanvasLogicalHeight * _zoom;

        double minX, maxX;
        if (cw <= vw)
        {
            minX = maxX = (vw - cw) / 2;
        }
        else
        {
            minX = vw - cw;
            maxX = 0;
        }

        double minY, maxY;
        if (ch <= vh)
        {
            minY = maxY = (vh - ch) / 2;
        }
        else
        {
            minY = vh - ch;
            maxY = 0;
        }

        FloorCanvasPan.X = Math.Max(minX, Math.Min(maxX, FloorCanvasPan.X));
        FloorCanvasPan.Y = Math.Max(minY, Math.Min(maxY, FloorCanvasPan.Y));
    }

    private bool IsOverFloorTableCard(DependencyObject? src)
    {
        while (src != null)
        {
            if (src is Border b && b.Tag is Guid && FloorTablesCanvas != null && b.IsDescendantOf(FloorTablesCanvas))
                return true;
            src = VisualTreeHelper.GetParent(src);
        }

        return false;
    }

    private void UpdateExpandToggleVisual()
    {
        if (FloorPlanExpandIconHost == null || FloorPlanCollapseIconHost == null || BtnToggleSidebar == null)
            return;
        if (_sidebarCollapsed)
        {
            FloorPlanExpandIconHost.Visibility = Visibility.Collapsed;
            FloorPlanCollapseIconHost.Visibility = Visibility.Visible;
            BtnToggleSidebar.ToolTip = "Show reservations panel";
        }
        else
        {
            FloorPlanExpandIconHost.Visibility = Visibility.Visible;
            FloorPlanCollapseIconHost.Visibility = Visibility.Collapsed;
            BtnToggleSidebar.ToolTip = "Expand floor plan";
        }
    }

    private void FloorCanvasViewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsOverFloorTableCard(e.OriginalSource as DependencyObject))
            return;
        _canvasPanning = true;
        _canvasPanMouseStart = e.GetPosition(FloorCanvasViewport);
        _canvasPanTranslateStart = new Point(FloorCanvasPan.X, FloorCanvasPan.Y);
        FloorCanvasViewport.CaptureMouse();
        e.Handled = true;
    }

    private void FloorCanvasViewport_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_canvasPanning || !FloorCanvasViewport.IsMouseCaptured)
            return;
        var cur = e.GetPosition(FloorCanvasViewport);
        var dx = cur.X - _canvasPanMouseStart.X;
        var dy = cur.Y - _canvasPanMouseStart.Y;
        FloorCanvasPan.X = _canvasPanTranslateStart.X + dx;
        FloorCanvasPan.Y = _canvasPanTranslateStart.Y + dy;
        ClampCanvasPan();
        e.Handled = true;
    }

    private void FloorCanvasViewport_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var wasPanning = _canvasPanning;
        if (FloorCanvasViewport.IsMouseCaptured && wasPanning)
            FloorCanvasViewport.ReleaseMouseCapture();
        if (wasPanning)
            InvalidateFloorCanvasVisuals();
        _canvasPanning = false;
    }

    private void FloorCanvasViewport_LostMouseCapture(object sender, RoutedEventArgs e) => _canvasPanning = false;

    private void FloorCanvasViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var step = S(40) * (e.Delta / 120.0);
        FloorCanvasPan.Y += step;
        ClampCanvasPan();
        e.Handled = true;
    }

    private void FloorCanvasViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateFloorCanvasPanWrapperLayoutSize();
        ClampCanvasPan();
    }

    private void UpdateLegend()
    {
        var floor = SelectedFloor;
        if (floor == null)
        {
            TxtLegendAvailable.Text = "0";
            TxtLegendReserved.Text = "0";
            TxtLegendOccupied.Text = "0";
            TxtTotalTables.Text = "0";
            return;
        }

        var tables = ActiveTablesOnFloor(floor);
        var av = 0;
        var res = 0;
        var occ = 0;
        foreach (var t in tables)
        {
            switch (OpsServicesStore.GetFloorPlanTableVisualKind(_selectedDate, t.Id))
            {
                case OpsFloorPlanTableVisualKind.Reserved:
                    res++;
                    break;
                case OpsFloorPlanTableVisualKind.Occupied:
                    occ++;
                    break;
                default:
                    av++;
                    break;
            }
        }

        TxtLegendAvailable.Text = av.ToString(CultureInfo.CurrentCulture);
        TxtLegendOccupied.Text = res.ToString(CultureInfo.CurrentCulture);
        TxtLegendReserved.Text = occ.ToString(CultureInfo.CurrentCulture);
        TxtTotalTables.Text = tables.Count.ToString(CultureInfo.CurrentCulture);
    }

    private void UpdateZoomLabel()
    {
        var displayPct = Math.Round(_zoom / FloorPlanZoomDisplayBaseline * 100);
        TxtZoomPercent.Text = displayPct.ToString(CultureInfo.CurrentCulture) + "%";
    }

    private void DpFilterDate_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        if (DpFilterDate.SelectedDate is not { } d)
            return;
        var next = DateOnly.FromDateTime(d);
        if (next == _selectedDate)
            return;
        _selectedDate = next;
        DpFilterDate.DisplayDate = d;
        RefreshAll();
    }

    private void CmbFloorLocation_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshAll();

    private void BtnFilterAll_Click(object sender, RoutedEventArgs e)
    {
        _listFilter = OpsReservationListFilter.All;
        RefreshAll();
    }

    private void BtnFilterReserved_Click(object sender, RoutedEventArgs e)
    {
        _listFilter = OpsReservationListFilter.Reserved;
        RefreshAll();
    }

    private void BtnFilterOccupied_Click(object sender, RoutedEventArgs e)
    {
        _listFilter = OpsReservationListFilter.Occupied;
        RefreshAll();
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(2.0, Math.Round(_zoom + 0.1, 2));
        ApplyZoomTransform();
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(0.5, Math.Round(_zoom - 0.1, 2));
        ApplyZoomTransform();
    }

    private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        ReservationHost.Visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        ReservationColumn.Width = _sidebarCollapsed ? new GridLength(0) : new GridLength(S(320));
        UpdateExpandToggleVisual();
        RefreshAll();
    }

    private void PillShiftFloor_Click(object sender, RoutedEventArgs e)
    {
        HighlightFloorPill(false);
        _navigateToShiftScheduling();
    }

    private void PillTableFloor_Click(object sender, RoutedEventArgs e)
    {
        HighlightFloorPill(false);
        _navigateToTableManagement();
    }

    private void BtnManageReservations_Click(object sender, RoutedEventArgs e) =>
        OpenManageReservations(null);

    //private void OpenManageReservations(Guid? focusReservationId)
    //{
    //    var floor = SelectedFloor;
    //    if (string.IsNullOrWhiteSpace(floor))
    //        return;
    //    //App.OpsTrace($"OpenManageReservations: floor={floor} focus={focusReservationId}");
    //    _openReservationsManagement(
    //        floor,
    //        _selectedDate,
    //        _listFilter,
    //        string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery.Trim(),
    //        focusReservationId);
    //}

    private void OpenManageReservations(Guid? focusReservationId)
    {
        var floor = SelectedFloor;
        if (string.IsNullOrWhiteSpace(floor))
            return;

        var effectiveFilter = _listFilter switch
        {
            OpsReservationListFilter.Reserved => OpsReservationListFilter.Occupied,
            OpsReservationListFilter.Occupied => OpsReservationListFilter.Reserved,
            _ => _listFilter
        };

        //App.OpsTrace($"OpenManageReservations: floor={floor} focus={focusReservationId}");
        _openReservationsManagement(
            floor,
            _selectedDate,
            effectiveFilter,
            string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery.Trim(),
            focusReservationId);
    }

    private void ReservationSearchHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Same as Table Management TableSearchHostBorder: let clicks on the TextBox through for caret/selection; only absorb chrome hits.
        if (e.OriginalSource is DependencyObject src && TxtReservationSearch.IsAncestorOf(src))
            return;

        e.Handled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (TxtReservationSearch.IsKeyboardFocusWithin)
                return;
            TxtReservationSearch.Focus();
            Keyboard.Focus(TxtReservationSearch);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void TxtReservationSearch_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (SearchFocusOuter != null)
            SearchFocusOuter.Visibility = Visibility.Visible;

        if (SearchFocusInner != null)
            SearchFocusInner.Visibility = Visibility.Visible;

        if (ReservationSearchHostBorder != null)
            ReservationSearchHostBorder.BorderBrush = Brushes.Transparent;
    }

    private void TxtReservationSearch_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (SearchFocusOuter != null)
            SearchFocusOuter.Visibility = Visibility.Collapsed;

        if (SearchFocusInner != null)
            SearchFocusInner.Visibility = Visibility.Collapsed;

        if (ReservationSearchHostBorder != null)
            ReservationSearchHostBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#CCC");
    }

    private void TxtReservationSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void SearchDebounce_Tick(object? sender, EventArgs e)
    {
        _searchDebounce.Stop();
        _searchQuery = (TxtReservationSearch.Text ?? "").Trim();
        RebuildReservationList();
    }

    /// <summary>
    /// Wheel over the whole reservations panel: scroll the list first (headers + search stay fixed); at list ends, scroll the page.
    /// </summary>
    /// <summary>
    /// Scroll the reservation list; header + search stay outside this ScrollViewer.
    /// When the list cannot absorb the wheel (no scroll or at end), leave unhandled so a parent ScrollViewer (e.g. dashboard) can scroll.
    /// </summary>
    private void ReservationHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var inner = ReservationsScroll;
        var innerHas = inner is { ScrollableHeight: > 0.5 };
        var up = e.Delta > 0;
        var down = e.Delta < 0;

        if (!innerHas)
        {
            e.Handled = false;
            return;
        }

        var atTop = inner.VerticalOffset <= 0.01;
        var atBottom = inner.VerticalOffset >= inner.ScrollableHeight - 0.01;

        if ((up && atTop) || (down && atBottom))
        {
            e.Handled = false;
            return;
        }

        if (up)
            inner.LineUp();
        else if (down)
            inner.LineDown();
        e.Handled = true;
    }

    private void ReservationItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not OpsReservation r)
            return;
        OpenManageReservations(r.Id);
        e.Handled = true;
    }

}
