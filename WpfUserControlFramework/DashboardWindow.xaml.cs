using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf
{
    public record NavItem(string Category, string Label, Func<UserControl> Factory);

    public partial class DashboardWindow : Window
    {
        private readonly List<NavItem> _navItems;
        private readonly List<string> _categories;
        private NavItem? _selectedNavItem;
        private bool _isInitializing = true;

        public DashboardWindow()
        {
            InitializeComponent();

            // Initialize UI scaling from this window's DPI/screen context
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

            // Initialize slider to current UiScaleState.FontScale
            var scaleState = Application.Current.Resources["UiScaleState"] as UiScaleState;
            if (scaleState != null)
            {
                ScaleSlider.Value = scaleState.FontScale;
                ScalePercentageLabel.Text = Math.Round(scaleState.FontScale * 100) + "%";
            }

            _navItems = new List<NavItem>
            {
                new NavItem("Procurement", "Dashboard", () => BuildProcurementControl()),
                new NavItem("Procurement", "Purchase Orders", () => new PurchaseOrdersControl()),
                new NavItem("Procurement", "Receiving", () => new ReceivingControl()),
                new NavItem("Procurement", "Discrepancies", () => BuildDiscrepanciesControl()),
                new NavItem("Procurement", "Reports", () => new ReportsControl()),
                new NavItem("Procurement", "Suppliers", () => new SuppliersControl()),
                new NavItem("Suppliers", "Documents", () => new DocumentRepositoryControl(
                    onRequestUploadDialog: ShowUploadDialog
                )),
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

        private ProcurementDiscrepancies BuildDiscrepanciesControl()
        {
            return BuildDiscrepanciesControl(new DiscrepanciesNavigationContext
            {
                Records = ProcurementDiscrepancyStore.GetAllAlignedToPurchaseOrders(),
                InitialStatusFilter = "All Statuses"
            });
        }

        private ProcurementDiscrepancies BuildDiscrepanciesControl(DiscrepanciesNavigationContext context)
        {
            return new ProcurementDiscrepancies(
                navigationContext: context,
                onOpenPurchaseOrder: _ => { },
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
