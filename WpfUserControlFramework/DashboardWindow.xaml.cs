using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf
{
    public record NavItem(string Label, Func<UserControl> Factory);

    public partial class DashboardWindow : Window
    {
        private readonly List<NavItem> _navItems;
        private readonly List<Button> _navButtons = new();
        private int _selectedIndex = -1;
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

            _isInitializing = false;

            _navItems = new List<NavItem>
            {
                new NavItem("Supplier: Documents", () => new DocumentRepositoryControl(
                    onRequestUploadDialog: ShowUploadDialog
                )),
                // Future entries added here
            };

            // Populate sidebar navigation buttons from registry
            var navButtonStyle = (Style)FindResource("NavButtonStyle");
            for (int i = 0; i < _navItems.Count; i++)
            {
                var button = new Button
                {
                    Content = _navItems[i].Label,
                    Style = navButtonStyle,
                    Tag = i
                };
                button.Click += NavButton_Click;
                _navButtons.Add(button);
                NavButtonPanel.Children.Add(button);
            }

            // Display the first nav item by default
            if (_navItems.Count > 0)
            {
                NavigateTo(0);
            }
        }

        public void NavigateTo(int index)
        {
            if (index < 0 || index >= _navItems.Count)
            {
                Debug.Print($"NavigateTo: index {index} is out of range (0–{_navItems.Count - 1}).");
                return;
            }

            try
            {
                var control = _navItems[index].Factory();

                if (control is null)
                {
                    Debug.Print($"NavigateTo: factory for '{_navItems[index].Label}' returned null.");
                    return;
                }

                ContentArea.Content = control;
                _selectedIndex = index;

                // Update selected visual state on nav buttons
                for (int i = 0; i < _navButtons.Count; i++)
                {
                    _navButtons[i].Tag = i == _selectedIndex ? "Selected" : i;
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"NavigateTo: factory for '{_navItems[index].Label}' threw: {ex.Message}");
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int index)
            {
                NavigateTo(index);
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
    }
}
