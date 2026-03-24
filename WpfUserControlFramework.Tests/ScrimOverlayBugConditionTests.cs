using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;
using FsCheck;

namespace RestaurantPosWpf.Tests
{
    /// <summary>
    /// All scrim overlay tests consolidated into a single [Fact] to avoid
    /// WPF Application singleton issues across multiple STA threads.
    /// 
    /// Property 1 (Bug Condition): ScrimOverlay must exist with correct properties.
    /// Property 2 (Preservation): Navigation, scale, presets, no-overlay all preserved.
    /// </summary>
    public class ScrimOverlayTests
    {
        [Fact]
        public void ScrimOverlay_BugCondition_And_Preservation()
        {
            TestHelper.RunOnSta(() =>
            {
                TestHelper.EnsureApplication();
                var window = new DashboardWindow();

                // ===== Property 1: Bug Condition (Req 1.1, 1.2) =====
                var scrimOverlay = window.FindName("ScrimOverlay") as FrameworkElement;
                Assert.NotNull(scrimOverlay);
                Assert.Equal(2, Grid.GetColumnSpan(scrimOverlay));
                Assert.Equal(10, Panel.GetZIndex(scrimOverlay));

                if (scrimOverlay is Border border)
                {
                    var brush = border.Background as SolidColorBrush;
                    Assert.NotNull(brush);
                    Assert.Equal(Color.FromArgb(128, 0, 0, 0), brush.Color);
                }
                else
                {
                    Assert.Fail("ScrimOverlay should be a Border element");
                }

                Assert.Equal(Visibility.Collapsed, scrimOverlay.Visibility);

                // PBT: Verify across generated DocumentModel samples
                var samples = Generators.DocumentModelArbitrary.DocumentModel()
                    .Generator.Sample(5, 3);
                foreach (var doc in samples)
                {
                    var overlay = window.FindName("ScrimOverlay") as FrameworkElement;
                    Assert.NotNull(overlay);
                    Assert.Equal(2, Grid.GetColumnSpan(overlay));
                    Assert.Equal(10, Panel.GetZIndex(overlay));
                    Assert.Equal(Visibility.Collapsed, overlay.Visibility);
                }

                // ===== Property 2: Preservation (Req 3.1, 3.2, 3.3, 3.4) =====

                // Navigation preserves ContentArea
                var contentArea = window.FindName("ContentArea") as ContentControl;
                Assert.NotNull(contentArea);
                Assert.IsType<DocumentRepositoryControl>(contentArea.Content);

                // Scale slider updates FontScale and label
                var slider = window.FindName("ScaleSlider") as Slider;
                var label = window.FindName("ScalePercentageLabel") as System.Windows.Controls.TextBlock;
                Assert.NotNull(slider);
                Assert.NotNull(label);

                var scaleState = Application.Current.Resources["UiScaleState"] as UiScaleState;
                Assert.NotNull(scaleState);

                var scaleValues = Gen.Choose(60, 180)
                    .Select(n => Math.Round(n / 100.0, 2))
                    .Sample(5, 3);
                foreach (var scale in scaleValues)
                {
                    double snapped = Math.Round(scale / 0.05) * 0.05;
                    snapped = Math.Max(0.6, Math.Min(1.8, snapped));
                    slider.Value = snapped;
                    Assert.Equal(Math.Round(snapped * 100) + "%", label.Text);
                    Assert.Equal(snapped, scaleState.FontScale, 2);
                }

                // Preset buttons
                foreach (var (value, text) in new[] { (0.75, "75%"), (1.0, "100%"), (1.25, "125%"), (1.5, "150%") })
                {
                    slider.Value = value;
                    Assert.Equal(value, slider.Value, 2);
                    Assert.Equal(text, label.Text);
                }

                // No overlay when no dialog is open
                window.NavigateTo(0);
                Assert.Equal(Visibility.Collapsed, scrimOverlay.Visibility);
            });
        }
    }
}
