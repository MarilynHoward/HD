# Bugfix Requirements Document

## Introduction

The popup/dialog system in the WPF UserControl Framework has three related bugs affecting the `UploadDocumentDialog` and its hosting `Window`. The popup window renders with square corners despite the inner UserControl having rounded borders, the file browse dialog fails to display properly because it lacks an owner window reference, and the steering document does not comprehensively document popup/dialog window hosting conventions. These issues collectively break the intended design language and block the file upload workflow.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN the `UploadDocumentDialog` popup is opened via `DashboardWindow.ShowUploadDialog()` THEN the hosting `Window` renders with default Windows chrome (title bar, square corners), hiding the rounded `CornerRadius` of the inner UserControl's root `Border`.

1.2 WHEN the user clicks the "Browse..." button inside the `UploadDocumentDialog` THEN the `OpenFileDialog.ShowDialog()` is called without an owner window parameter, which can cause the file dialog to appear behind the modal popup window or fail to display, preventing file selection.

1.3 WHEN a developer references the steering document (`SteeringDocument.md`) for popup/dialog hosting conventions THEN the document describes the UserControl layout pattern (§8) but does not specify the required `Window` properties (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`) needed for the hosting window to render the UserControl's rounded corners correctly.

### Expected Behavior (Correct)

2.1 WHEN the `UploadDocumentDialog` popup is opened via `DashboardWindow.ShowUploadDialog()` THEN the hosting `Window` SHALL be configured with `WindowStyle="None"`, `AllowsTransparency="True"`, and `Background="Transparent"` so that the inner UserControl's rounded `Border` is fully visible without any default window chrome.

2.2 WHEN the user clicks the "Browse..." button inside the `UploadDocumentDialog` THEN the `OpenFileDialog.ShowDialog()` SHALL be called with the parent window as the owner (e.g. `dialog.ShowDialog(Window.GetWindow(this))`) so that the file dialog always appears in front of the modal popup and the user can select a file.

2.3 WHEN a developer references the steering document for popup/dialog hosting conventions THEN the document SHALL include a dedicated section (or update to §4/§8) specifying the required `Window` properties for chromeless, transparent popup hosting, including `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, `ResizeMode="NoResize"`, and `SizeToContent="WidthAndHeight"`.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN the `UploadDocumentDialog` popup is opened THEN the system SHALL CONTINUE TO display the dialog as a modal window centered on the owner (`WindowStartupLocation.CenterOwner`, `Owner = this`).

3.2 WHEN the user fills in all required fields and clicks "Upload Document" THEN the system SHALL CONTINUE TO validate the form, create a `DocumentModel`, and invoke the `onDocumentUploaded` callback correctly.

3.3 WHEN the user clicks "Cancel" or the close button (✕) THEN the system SHALL CONTINUE TO close the dialog window via the `onCancel` callback.

3.4 WHEN the user selects a valid file via the browse dialog THEN the system SHALL CONTINUE TO validate file type and size, update the browse button text, and auto-populate the document name field.

3.5 WHEN main content-area controls (e.g. `DocumentRepositoryControl`, `DocumentDetailView`) are displayed THEN the system SHALL CONTINUE TO use the existing Content Root Border pattern with `Background="#F8FAFC"` and standard window chrome — only popup/dialog hosting windows are affected by the chromeless pattern.

3.6 WHEN the steering document is updated THEN the system SHALL CONTINUE TO preserve all existing sections (§1–§10) and their content, only adding or amending popup hosting guidance.
