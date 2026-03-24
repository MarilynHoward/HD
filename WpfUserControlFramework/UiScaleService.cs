using System;
using System.Configuration;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Forms;

namespace RestaurantPosWpf
{
    public static class UiScaleService
    {
        // Baseline = 150% (144 DPI) and your 150% logical width ≈ 1280 DIPs
        private const double BaselineDpi_150 = 144.0;
        private const double BaselineWidth_150 = 1280.0;

        private const double BaselineDpi_125 = 120.0;
        private const double BaselineWidth_125 = 1536.0;

        private const double BaselineDpi_100 = 96.0;
        private const double BaselineWidth_100 = 1920.0;

        // Safety limits
        private const double MinScale = 0.6;
        private const double MaxScale = 1.80;

        private static bool _dpiHooked;

        public static double FontScale { get; private set; } = 1.0;

        public static event Action<double>? ScaleChanged;

        private static double _lastFontScale = 1.0;

        private static bool _scaleSnapshotEnabled = true;
        private static bool _scaleSnapshotUseMessageBox = true;

        private static void ScaleSnapshot(string reason, Visual visual)
        {
            if (!_scaleSnapshotEnabled)
                return;

            try
            {
                if (System.Windows.Application.Current == null)
                    return;

                Action emit = () =>
                {
                    string profile = GetRenderProfilePercent().ToString(CultureInfo.InvariantCulture);
                    double svcScale = FontScale;
                    double dpiX = 0.0;
                    double baselineDpi = ActiveBaselineDpi;
                    double dpiDerivedScale = 0.0;

                    try
                    {
                        if (visual != null)
                        {
                            var dpi = VisualTreeHelper.GetDpi(visual);
                            dpiX = dpi.PixelsPerInchX;
                            if (dpiX > 0.0)
                                dpiDerivedScale = Clamp(baselineDpi / dpiX, MinScale, MaxScale);
                        }
                    }
                    catch { }

                    double width = 0.0;
                    double baselineWidth = ActiveBaselineWidth;
                    double widthDerivedScale = 0.0;

                    try
                    {
                        width = SystemParameters.PrimaryScreenWidth;
                        if (width > 0.0)
                            widthDerivedScale = Clamp(width / baselineWidth, MinScale, MaxScale);
                    }
                    catch { }

                    double uiFontScaleRes = 0.0;
                    try
                    {
                        object? res = System.Windows.Application.Current.TryFindResource("UiFontScale");
                        if (res is double d)
                            uiFontScaleRes = d;
                        else if (res != null)
                            double.TryParse(res.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out uiFontScaleRes);
                    }
                    catch { }

                    double uiScaleState = 0.0;
                    try
                    {
                        object stateObj = System.Windows.Application.Current.Resources["UiScaleState"];
                        UiScaleState? state = stateObj as UiScaleState;
                        if (state != null)
                            uiScaleState = state.FontScale;
                    }
                    catch { }

                    string msg =
                        "Scale Snapshot | Reason: " + (reason ?? "") +
                        " | UiRenderProfilePercent: " + profile +
                        " | ActiveBaselineDpi: " + baselineDpi.ToString("0.###", CultureInfo.InvariantCulture) +
                        " | DpiX: " + dpiX.ToString("0.###", CultureInfo.InvariantCulture) +
                        " | Scale (baselineDpi/dpiX): " + dpiDerivedScale.ToString("0.###", CultureInfo.InvariantCulture) +
                        " | ActiveBaselineWidth: " + baselineWidth.ToString("0.###", CultureInfo.InvariantCulture) +
                        " | PrimaryScreenWidth (DIPs): " + width.ToString("0.###", CultureInfo.InvariantCulture) +
                        " | Scale (width/baselineWidth): " + widthDerivedScale.ToString("0.###", CultureInfo.InvariantCulture) +
                        " | UiScaleService.FontScale: " + svcScale.ToString("0.###", CultureInfo.InvariantCulture) +
                        " | Resource UiScaleState.FontScale: " + uiScaleState.ToString("0.###", CultureInfo.InvariantCulture) +
                        " | Resource UiFontScale: " + uiFontScaleRes.ToString("0.###", CultureInfo.InvariantCulture);

                    System.Diagnostics.Debug.Print(msg);
                };

                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                    emit();
                else
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(emit, DispatcherPriority.Background);
            }
            catch { }
        }

        private static int GetRenderProfilePercent()
        {
            try
            {
                string? value = ConfigurationManager.AppSettings["UiRenderProfilePercent"];
                int percent;
                if (value != null && Int32.TryParse(value, out percent))
                {
                    if (percent == 100 || percent == 125 || percent == 150)
                        return percent;
                }
            }
            catch { }

            return 150;
        }

        private static void NotifyScaleChanged(double newScale)
        {
            if (Math.Abs(newScale - _lastFontScale) < 0.001) return;
            _lastFontScale = newScale;
            ScaleChanged?.Invoke(newScale);
        }

        public static void InitializeFromWindow(Window window)
        {
            if (window == null) return;

            double widthScale = GetScaleFromWidths(window);
            ApplyScale(widthScale);

            if (!_sourceInitializedAttached)
            {
                window.SourceInitialized += Window_SourceInitialized;
                _sourceInitializedAttached = true;
            }
        }

        private static void Window_SourceInitialized(object? sender, EventArgs e)
        {
            var window = sender as Window;
            if (window == null) return;
            HookDpiTracking(window);
        }

        private static void HookDpiTracking(Window window)
        {
            if (_dpiHooked) return;

            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;

            var source = HwndSource.FromHwnd(helper.Handle);
            if (source == null) return;

            _dpiHooked = true;

            UpdateScaleFromDpiOrWidth(window);

            source.AddHook(WndProc);
        }

        public static event Action? DpiChanged;

        private static bool _sourceInitializedAttached;

        private static IntPtr WndProc(
            IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DPICHANGED = 0x02E0;

            if (msg == WM_DPICHANGED)
            {
                handled = false;

                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                    new Action(() => DpiChanged?.Invoke()),
                    DispatcherPriority.Loaded);
            }

            return IntPtr.Zero;
        }

        private static void UpdateScaleFromDpiOrWidth(Window window, double dpiXOverride = 0.0)
        {
            if (window == null) return;

            double scaleFromDpi = dpiXOverride > 0.0
                ? GetScaleFromDpiX(dpiXOverride)
                : GetScaleFromDpi(window);

            double scaleFromWidth = GetScaleFromWidths(window);

            const double TOL = 0.05;
            double chosen = 0.0;

            bool hasDpi = (scaleFromDpi > 0.0);
            bool hasWidth = (scaleFromWidth > 0.0);

            if (hasDpi && hasWidth)
            {
                double diff = Math.Abs(scaleFromDpi - scaleFromWidth);
                chosen = diff > TOL ? scaleFromWidth : scaleFromDpi;
            }
            else if (hasDpi)
                chosen = scaleFromDpi;
            else if (hasWidth)
                chosen = scaleFromWidth;

            if (chosen <= 0.0) chosen = 1.0;

            ApplyScale(chosen);

            // Deferred re-check after layout settles
            window.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    double sDpi2 = GetScaleFromDpi(window);
                    double sW2 = GetScaleFromWidths(window);

                    double chosen2 = sDpi2;
                    if (sDpi2 > 0.0 && sW2 > 0.0 && Math.Abs(sDpi2 - sW2) > TOL)
                        chosen2 = sW2;
                    if (chosen2 <= 0.0) chosen2 = chosen;

                    ApplyScale(chosen2);
                }),
                DispatcherPriority.Loaded);
        }

        private static double GetScaleFromDpiX(double dpiX)
        {
            if (dpiX <= 0.0) return 0.0;
            double scale = ActiveBaselineDpi / dpiX;
            return Clamp(scale, MinScale, MaxScale);
        }

        private static double ActiveBaselineDpi
        {
            get
            {
                switch (GetRenderProfilePercent())
                {
                    case 125: return BaselineDpi_125;
                    case 100: return BaselineDpi_100;
                    case 150:
                    default: return BaselineDpi_150;
                }
            }
        }

        private static double ActiveBaselineWidth
        {
            get
            {
                switch (GetRenderProfilePercent())
                {
                    case 125: return BaselineWidth_125;
                    case 100: return BaselineWidth_100;
                    case 150:
                    default: return BaselineWidth_150;
                }
            }
        }

        private static double GetScaleFromDpi(Visual visual)
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(visual);
                double dpiX = dpi.PixelsPerInchX;
                if (dpiX <= 0.0) return 0.0;

                double scale = ActiveBaselineDpi / dpiX;
                return Clamp(scale, MinScale, MaxScale);
            }
            catch
            {
                return 0.0;
            }
        }

        private static double GetScaleFromWidths(Window window)
        {
            try
            {
                double logicalWidthDip = 0.0;

                if (window != null)
                {
                    var helper = new WindowInteropHelper(window);
                    if (helper.Handle != IntPtr.Zero)
                    {
                        Screen screen = Screen.FromHandle(helper.Handle);
                        int pixelWidth = screen.Bounds.Width;

                        var source = PresentationSource.FromVisual(window);
                        if (source?.CompositionTarget != null)
                        {
                            Matrix m = source.CompositionTarget.TransformFromDevice;
                            logicalWidthDip = m.Transform(new System.Windows.Point(pixelWidth, 0)).X;
                        }
                    }
                }

                if (logicalWidthDip <= 0.0)
                    logicalWidthDip = SystemParameters.PrimaryScreenWidth;

                if (logicalWidthDip <= 0.0)
                    return 1.0;

                double scale = logicalWidthDip / ActiveBaselineWidth;
                return Clamp(scale, MinScale, MaxScale);
            }
            catch
            {
                return 1.0;
            }
        }

        private static void ApplyScale(double fontScale)
        {
            if (fontScale <= 0.0) fontScale = 1.0;

            FontScale = fontScale;

            if (System.Windows.Application.Current == null) return;

            Dispatcher? dispatcher = System.Windows.Application.Current.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
                ApplyScale_Core(fontScale);
            else
                dispatcher.Invoke(() => ApplyScale_Core(fontScale));
        }

        private static void ApplyScale_Core(double fontScale)
        {
            try
            {
                if (System.Windows.Application.Current == null) return;

                // Legacy binding path
                System.Windows.Application.Current.Resources["UiFontScale"] = fontScale;

                object? res = System.Windows.Application.Current.TryFindResource("UiScaleState");
                UiScaleState? state = res as UiScaleState;
                if (state != null)
                    state.FontScale = fontScale;

                NotifyScaleChanged(fontScale);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print($"UiScaleService.ApplyScale_Core exception: {ex.Message}");
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
