using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RestaurantPosWpf
{
    public record NavItem(string Category, string Label, Func<UserControl> Factory);

    public partial class DashboardWindow : Window
    {
        private const double DefaultDashboardFontScale = 1.25;

        private readonly List<NavItem> _navItems;
        private readonly List<string> _categories;
        private NavItem? _selectedNavItem;
        private bool _isInitializing = true;
        private bool _defaultScaleApplied;

        private OpsServicesShiftScheduling _opsShiftSchedulingControl;
        private OpsServicesTableManagement _opsTableManagementControl;
        private OpsServicesFloorPlanDesign _opsFloorPlanDesignControl;

        public DashboardWindow()
        {
            InitializeComponent();

            // Initialize UI scaling from this window's DPI/screen context (hooks DPI changes)
            UiScaleService.InitializeFromWindow(this);

            // Re-apply scale when DPI changes (monitor switch, OS scale change)
            UiScaleService.DpiChanged += () =>
            {
                UiScaleService.InitializeFromWindow(this);

                // Sync slider to the new automatically computed scale value
                if (ScaleSlider != null && ScaleSlider.IsLoaded)
                {
                    ScaleSlider.Value = UiScaleService.FontScale;
                }
            };

            // Slider/label sync until default scale is applied on Loaded (see DashboardWindow_Loaded)
            var scaleState = Application.Current.Resources["UiScaleState"] as UiScaleState;
            if (scaleState != null)
            {
                ScaleSlider.Value = scaleState.FontScale;
                ScalePercentageLabel.Text = Math.Round(scaleState.FontScale * 100) + "%";
            }

            _navItems = new List<NavItem>
            {
                new NavItem("Procurement", "Dashboard", () => BuildProcurementControl()),
                new NavItem("Procurement", "Purchase Orders", () => BuildPurchaseOrdersControl()),
                new NavItem("Procurement", "Receiving", () => new ReceivingControl()),
                new NavItem("Procurement", "Discrepancies", () => BuildDiscrepanciesControl()),
                new NavItem("Procurement", "Reports", () => new ReportsControl()),
                new NavItem("Procurement", "Suppliers", () => new SuppliersControl()),
                new NavItem("Suppliers", "Documents", () => new DocumentRepositoryControl(
                    onRequestUploadDialog: ShowUploadDialog
                )),
                new NavItem("Operations and Services", "Shift Scheduling", () => BuildOpsShiftScheduling()),
                new NavItem("Staff and Access", "User details", () => new StaffAccessUserDetails()),
                new NavItem("Reporting", "Dashboard", () => BuildRptDashboard()),
            };

            // Build distinct category list preserving registration order
            _categories = _navItems.Select(n => n.Category).Distinct().ToList();
            CategoryComboBox.ItemsSource = _categories;

            _isInitializing = false;

            // Select first category — triggers CategoryComboBox_SelectionChanged
            if (_categories.Count > 0)
            {
                CategoryComboBox.SelectedIndex = 0;
            }
        }

        private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_defaultScaleApplied)
                return;

            // Run after automatic DPI/width scale passes (UiScaleService deferred Loaded callback)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_defaultScaleApplied)
                    return;
                _defaultScaleApplied = true;

                UiScaleService.SetFontScale(DefaultDashboardFontScale);
                if (ScaleSlider != null)
                    ScaleSlider.Value = DefaultDashboardFontScale;
                if (ScalePercentageLabel != null)
                    ScalePercentageLabel.Text = Math.Round(DefaultDashboardFontScale * 100) + "%";
            }), DispatcherPriority.ApplicationIdle);
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || CategoryComboBox.SelectedItem is not string selectedCategory)
                return;

            RebuildNavButtons(selectedCategory);
        }

        private void RebuildNavButtons(string category)
        {
            NavButtonPanel.Children.Clear();

            var filtered = _navItems.Where(n => n.Category == category).ToList();
            var navButtonStyle = (Style)FindResource("NavButtonStyle");

            foreach (var item in filtered)
            {
                var button = new Button
                {
                    Content = item.Label,
                    Style = navButtonStyle,
                    DataContext = item
                };
                button.Click += NavButton_Click;
                NavButtonPanel.Children.Add(button);
            }

            // Auto-select the first item in this category
            if (filtered.Count > 0)
            {
                NavigateTo(filtered[0]);
            }
        }

        public void NavigateTo(NavItem item)
        {
            try
            {
                var control = item.Factory();

                if (control is null)
                {
                    Debug.Print($"NavigateTo: factory for '{item.Label}' returned null.");
                    return;
                }

                ContentArea.Content = control;
                _selectedNavItem = item;

                // Update selected visual state on nav buttons
                foreach (var child in NavButtonPanel.Children.OfType<Button>())
                {
                    var navTag = child.DataContext as NavItem;
                    child.Tag = navTag == _selectedNavItem ? "Selected" : navTag;
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"NavigateTo: factory for '{item.Label}' threw: {ex.Message}");
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is NavItem item)
            {
                NavigateTo(item);
            }
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Guard: don't write during initialization or before UI is ready
            if (_isInitializing || ScalePercentageLabel == null)
                return;

            double value = e.NewValue;

            // Update percentage label in real time
            ScalePercentageLabel.Text = Math.Round(value * 100) + "%";

            // Write directly to UiScaleState.FontScale (bypasses UiScaleService)
            var scaleState = Application.Current.Resources["UiScaleState"] as UiScaleState;
            if (scaleState != null)
            {
                scaleState.FontScale = value;
            }
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr &&
                double.TryParse(tagStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double preset))
            {
                // Setting the slider value triggers ScaleSlider_ValueChanged,
                // which writes to UiScaleState.FontScale and updates the label
                ScaleSlider.Value = preset;
            }
        }

        private void ShowUploadDialog(Action<DocumentModel> onUploaded)
        {
            Window? dialogWindow = null;

            var dialog = new UploadDocumentDialog(
                categories: DocumentCategories.All,
                onDocumentUploaded: newDoc =>
                {
                    onUploaded(newDoc);
                    dialogWindow?.Close();
                },
                onCancel: () => dialogWindow?.Close()
            );

            dialogWindow = new Window
            {
                Content = dialog,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };

            ScrimOverlay.Visibility = Visibility.Visible;
            dialogWindow.ShowDialog();
            ScrimOverlay.Visibility = Visibility.Collapsed;
        }

        private ProcurementControl BuildProcurementControl()
        {
            return new ProcurementControl(
                onViewDiscrepancies: context => NavigateToDiscrepancies(context));
        }

        private RptDashboardMain BuildRptDashboard()
        {
            return new RptDashboardMain(
                onRecentlyUsedReportActivated: row =>
                    MessageBox.Show(
                        this,
                        $"Opening: {row.Report.DisplayName}\r\nCode: {row.Report.Id}",
                        "Reporting",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information),
                onAttentionNeededActivated: row =>
                    MessageBox.Show(
                        this,
                        $"Attention — {row.Report.DisplayName}\r\nCode: {row.Report.Id}",
                        "Reporting",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information),
                onBrowseGroupingActivated: tile =>
                    MessageBox.Show(
                        this,
                        $"Browse group: {tile.Title}\r\nId: {tile.GroupId}",
                        "Reporting",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information));
        }

        private ProcurementPOrders BuildPurchaseOrdersControl()
        {
            return new ProcurementPOrders(
                onOpenPurchaseOrder: _ => { },
                onClose: NavigateToProcurementDashboard);
        }

        private ProcurementDiscrepancies BuildDiscrepanciesControl()
        {
            var aligned = ProcurementDiscrepancyStore.GetAllAlignedToPurchaseOrders();
            var filteredRecords = aligned
                .Where(r => ProcurementPurchaseOrderStore.GetByPONumber(r.PONumber)?.HasDiscrepancy == true)
                .ToList();
            var purchaseOrders = ProcurementPurchaseOrderStore.GetAll()
                .Where(p => p.HasDiscrepancy)
                .ToList();

            return BuildDiscrepanciesControl(new DiscrepanciesNavigationContext
            {
                Records = filteredRecords,
                PurchaseOrders = purchaseOrders,
                InitialStatusFilter = "All Statuses"
            });
        }

        private ProcurementDiscrepancies BuildDiscrepanciesControl(DiscrepanciesNavigationContext context)
        {
            return new ProcurementDiscrepancies(
                navigationContext: context,
                onOpenPurchaseOrder: _ => { },
                onOpenDispute: _ => { },
                onClose: NavigateToProcurementDashboard);
        }

        private void NavigateToDiscrepancies(DiscrepanciesNavigationContext context)
        {
            var navItem = _navItems.First(i => i.Category == "Procurement" && i.Label == "Discrepancies");
            ContentArea.Content = BuildDiscrepanciesControl(context);
            _selectedNavItem = navItem;

            foreach (var child in NavButtonPanel.Children.OfType<Button>())
            {
                var navTag = child.DataContext as NavItem;
                child.Tag = navTag == _selectedNavItem ? "Selected" : navTag;
            }
        }

        private void NavigateToProcurementDashboard()
        {
            var navItem = _navItems.First(i => i.Category == "Procurement" && i.Label == "Dashboard");
            NavigateTo(navItem);
        }

        private OpsServicesShiftScheduling BuildOpsShiftScheduling()
        {
            _opsShiftSchedulingControl ??= new OpsServicesShiftScheduling(
                navigateToTableManagement: NavigateToOpsTableManagement,
                navigateToFloorPlan: NavigateToOpsFloorPlan,
                openAddShiftDialog: ShowOpsAddShiftDialog);
            return _opsShiftSchedulingControl;
        }

        private OpsServicesTableManagement BuildOpsTableManagement()
        {
            _opsTableManagementControl ??= new OpsServicesTableManagement(
                navigateToShiftScheduling: () =>
                    NavigateTo(_navItems.First(i =>
                        i.Category == "Operations and Services" && i.Label == "Shift Scheduling")),
                navigateToFloorPlan: NavigateToOpsFloorPlan,
                openAddTableDialog: ShowOpsAddTableDialog,
                openManageFloorsDialog: ShowOpsManageFloorsDialog);
            return _opsTableManagementControl;
        }

        private OpsServicesFloorPlanDesign BuildOpsFloorPlanDesign()
        {
            _opsFloorPlanDesignControl ??= new OpsServicesFloorPlanDesign(
                navigateToShiftScheduling: () =>
                    NavigateTo(_navItems.First(i =>
                        i.Category == "Operations and Services" && i.Label == "Shift Scheduling")),
                navigateToTableManagement: NavigateToOpsTableManagement,
                openReservationsManagement: ShowOpsReservationsManagementDialog);
            return _opsFloorPlanDesignControl;
        }

        private void ShowOpsReservationsManagementDialog(
            string? floorName,
            DateOnly date,
            OpsReservationListFilter listFilter,
            string? reservationSearchTrimmed,
            Guid? focusReservationId = null)
        {
            if (string.IsNullOrWhiteSpace(floorName))
                return;

            Window? w = null;
            var dlg = new OpsServicesReservationsManagement(
                () => w?.Close(),
                floorName,
                date,
                listFilter,
                reservationSearchTrimmed,
                focusReservationId);
            w = CreateOpsModalWindow(dlg);
            var wa = SystemParameters.WorkArea;
            w.SizeToContent = SizeToContent.Manual;
            w.Width = Math.Min(wa.Width * 0.52, 720);
            w.Height = Math.Min(Math.Max(620, wa.Height * 0.90), wa.Height * 0.94);
            w.MinWidth = 480;
            w.MinHeight = 560;
            w.MaxWidth = Math.Min(wa.Width * 0.58, 880);
            w.MaxHeight = wa.Height * 0.94;
            ScrimOverlay.Visibility = Visibility.Visible;
            w.ShowDialog();
            ScrimOverlay.Visibility = Visibility.Collapsed;
        }

        private void NavigateToOpsTableManagement()
        {
            var opsNav = _navItems.First(i =>
                i.Category == "Operations and Services" && i.Label == "Shift Scheduling");
            ContentArea.Content = BuildOpsTableManagement();
            _selectedNavItem = opsNav;
            foreach (var child in NavButtonPanel.Children.OfType<Button>())
            {
                var navTag = child.DataContext as NavItem;
                child.Tag = navTag == _selectedNavItem ? "Selected" : navTag;
            }
        }

        private void NavigateToOpsFloorPlan()
        {
            var opsNav = _navItems.First(i =>
                i.Category == "Operations and Services" && i.Label == "Shift Scheduling");
            ContentArea.Content = BuildOpsFloorPlanDesign();
            _selectedNavItem = opsNav;
            foreach (var child in NavButtonPanel.Children.OfType<Button>())
            {
                var navTag = child.DataContext as NavItem;
                child.Tag = navTag == _selectedNavItem ? "Selected" : navTag;
            }
        }

        private Window CreateOpsModalWindow(UserControl content)
        {
            return new Window
            {
                Content = content,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };
        }

        private void ShowOpsAddShiftDialog()
        {
            Window? w = null;
            var dlg = new OpsServicesAddShift(() => w?.Close());
            w = CreateOpsModalWindow(dlg);
            var wa = SystemParameters.WorkArea;
            w.MaxHeight = wa.Height * 0.9;
            w.MaxWidth = Math.Min(wa.Width * 0.42, 560);
            ScrimOverlay.Visibility = Visibility.Visible;
            w.ShowDialog();
            ScrimOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowOpsManageFloorsDialog()
        {
            Window? w = null;
            var dlg = new OpsServicesManageFloors(() => w?.Close());
            w = CreateOpsModalWindow(dlg);
            var wa = SystemParameters.WorkArea;
            w.MinWidth = 400;
            w.MaxWidth = Math.Min(wa.Width * 0.5, 640);
            w.MaxHeight = wa.Height * 0.92;
            ScrimOverlay.Visibility = Visibility.Visible;
            w.ShowDialog();
            ScrimOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowOpsAddTableDialog()
        {
            Window? w = null;
            var dlg = new OpsServicesAddTable(() => w?.Close());
            w = CreateOpsModalWindow(dlg);
            var wa = SystemParameters.WorkArea;
            w.MinWidth = 400;
            w.MaxWidth = Math.Min(wa.Width * 0.48, 600);
            w.MaxHeight = wa.Height * 0.92;
            ScrimOverlay.Visibility = Visibility.Visible;
            w.ShowDialog();
            ScrimOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Pushes the procurement discrepancies overlay footer height into <see cref="UiScaleState.FooterAlignHeight"/>
        /// so split layouts can align with the overlay strip.
        /// </summary>
        public void RefreshFooterAlignHeightForChildOverlay()
        {
            var scaleState = Application.Current.Resources["UiScaleState"] as UiScaleState;
            if (scaleState == null)
                return;

            if (ContentArea?.Content is ProcurementControl pc)
                scaleState.FooterAlignHeight = pc.GetActiveFooterStripHeight();
            else
                scaleState.FooterAlignHeight = 0;
        }
    }
}
