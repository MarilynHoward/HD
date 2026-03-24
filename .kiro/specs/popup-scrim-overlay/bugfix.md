# Bugfix Requirements Document

## Introduction

When a modal popup/dialog is shown in the WPF application (e.g., the `UploadDocumentDialog`), the background content behind the dialog is not visually dimmed. A semi-transparent scrim overlay should appear over the `DashboardWindow` content to clearly indicate the modal state, but currently no such overlay exists. The `DashboardWindow.xaml` Grid has no scrim element, and the `ShowUploadDialog()` method in `DashboardWindow.xaml.cs` does not show or hide any overlay when opening or closing the dialog.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a modal popup is opened via `ShowUploadDialog()` in `DashboardWindow.xaml.cs` THEN the system displays the dialog window but does not render any semi-transparent scrim overlay over the `DashboardWindow` content, leaving the background fully visible and providing no visual indication of the modal state.

1.2 WHEN the `DashboardWindow.xaml` layout is rendered THEN the system has no scrim/overlay UI element defined in the Grid, so there is no element available to show or hide when a dialog is opened.

### Expected Behavior (Correct)

2.1 WHEN a modal popup is opened via `ShowUploadDialog()` in `DashboardWindow.xaml.cs` THEN the system SHALL display a semi-transparent dark scrim overlay covering the entire `DashboardWindow` content area (both sidebar and main content) before the dialog is shown, and SHALL hide the scrim after the dialog is closed.

2.2 WHEN the `DashboardWindow.xaml` layout is rendered THEN the system SHALL include a scrim overlay element (e.g., a `Border` or `Rectangle` with a semi-transparent black background) in the root Grid that spans all columns, is collapsed by default, and can be toggled to visible when a modal dialog is displayed.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN no modal popup is open THEN the system SHALL CONTINUE TO display the `DashboardWindow` content (sidebar and `ContentArea`) without any overlay or dimming.

3.2 WHEN the `UploadDocumentDialog` popup is opened THEN the system SHALL CONTINUE TO display the dialog as a modal window centered on the owner with `WindowStartupLocation.CenterOwner` and `Owner = this`.

3.3 WHEN the user completes or cancels the upload dialog THEN the system SHALL CONTINUE TO close the dialog window and invoke the appropriate callback (`onDocumentUploaded` or `onCancel`) correctly.

3.4 WHEN the user interacts with sidebar navigation or the UI scale controls THEN the system SHALL CONTINUE TO function normally, unaffected by the presence of the scrim element in the XAML tree (since it is collapsed when no dialog is open).
