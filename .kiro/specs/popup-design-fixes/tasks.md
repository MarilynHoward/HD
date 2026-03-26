# Tasks — Popup Design Fixes

- [x] 1. Fix chromeless popup window in ShowUploadDialog
  - [x] 1.1 In `DashboardWindow.xaml.cs`, update the `ShowUploadDialog` method to set `WindowStyle = WindowStyle.None`, `AllowsTransparency = true`, `Background = System.Windows.Media.Brushes.Transparent`, and `ResizeMode = ResizeMode.NoResize` on the hosting `Window`.
- [x] 2. Fix OpenFileDialog owner in BrowseButton_Click
  - [x] 2.1 In `UploadDocumentDialog.xaml.cs`, update `BrowseButton_Click` to change `dialog.ShowDialog()` to `dialog.ShowDialog(Window.GetWindow(this))` so the file picker is owned by the popup window.
- [x] 3. Update steering document with popup hosting conventions
  - [x] 3.1 In `SteeringDocument.md` §4 "Dialog hosting" subsection, update the `Window` creation code sample to include `WindowStyle = WindowStyle.None`, `AllowsTransparency = true`, `Background = Brushes.Transparent`, and `ResizeMode = ResizeMode.NoResize`.
  - [x] 3.2 Add a note below the code sample explaining that any `CommonDialog.ShowDialog()` call inside a popup must pass `Window.GetWindow(this)` as the owner to avoid z-order issues with modal windows.
