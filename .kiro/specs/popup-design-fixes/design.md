# Popup Design Fixes — Bugfix Design

## Overview

The `UploadDocumentDialog` popup has two runtime bugs and one documentation gap. The hosting `Window` created in `DashboardWindow.ShowUploadDialog()` uses default chrome, which masks the UserControl's rounded corners with a square title bar frame. The `OpenFileDialog.ShowDialog()` call inside the dialog lacks an owner window reference, causing the file picker to potentially appear behind the modal or fail to display. Finally, the steering document (§4) shows the dialog hosting pattern but omits the required `Window` properties for chromeless, transparent hosting. All three issues share a common root: the hosting `Window` configuration is incomplete.

## Glossary

- **Bug_Condition (C)**: The hosting `Window` is created without `WindowStyle="None"`, `AllowsTransparency="True"`, and `Background="Transparent"`, causing default chrome to render; and `OpenFileDialog.ShowDialog()` is called without an owner, causing z-order issues.
- **Property (P)**: The hosting `Window` renders transparently so the UserControl's rounded `Border` is visible; the `OpenFileDialog` always appears in front of the modal popup.
- **Preservation**: Modal centering, form validation, upload callback, cancel callback, and all non-popup UI behavior must remain unchanged.
- **ShowUploadDialog**: The method in `DashboardWindow.xaml.cs` that creates a `Window`, sets the `UploadDocumentDialog` as its `Content`, and calls `ShowDialog()`.
- **BrowseButton_Click**: The event handler in `UploadDocumentDialog.xaml.cs` that opens an `OpenFileDialog` for file selection.
- **SteeringDocument.md**: The project conventions document at `WpfUserControlFramework/Docs/SteeringDocument.md`.

## Bug Details

### Bug Condition

The bug manifests in two related scenarios:

1. When `ShowUploadDialog` creates the hosting `Window` without chromeless properties, the default Windows title bar and square frame render over the UserControl's rounded `Border`.
2. When `BrowseButton_Click` calls `OpenFileDialog.ShowDialog()` without passing the parent `Window` as owner, the file dialog may appear behind the modal popup or fail to gain focus.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type { action: "open_popup" | "browse_file", windowConfig: WindowConfig }
  OUTPUT: boolean

  IF input.action == "open_popup" THEN
    RETURN windowConfig.WindowStyle != None
           OR windowConfig.AllowsTransparency != True
           OR windowConfig.Background != Transparent
  END IF

  IF input.action == "browse_file" THEN
    RETURN openFileDialog.ShowDialog() is called without owner window parameter
  END IF

  RETURN false
END FUNCTION
```

### Examples

- **Square corners**: User clicks "Upload Document" → popup opens with Windows title bar and square corners, hiding the UserControl's `CornerRadius="14"` border. Expected: chromeless window shows rounded corners.
- **File dialog behind modal**: User clicks "Browse..." inside the popup → `OpenFileDialog` appears behind the modal window or on a different monitor. User cannot interact with it. Expected: file dialog appears in front of the popup.
- **File dialog works on first try but fails on second**: Intermittent z-order issue when the owner is not set — sometimes the OS places the dialog correctly, sometimes not.
- **Steering document gap**: Developer reads §4 "Dialog hosting" and creates a new popup window without chromeless properties, reproducing bug 1 in a new dialog.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- The popup must continue to display as a modal window centered on the `DashboardWindow` (`WindowStartupLocation.CenterOwner`, `Owner = this`)
- Form validation (document name required, file required) must continue to work
- The `onDocumentUploaded` callback must continue to fire with a valid `DocumentModel` on successful upload
- The `onCancel` callback and close button (✕) must continue to close the dialog
- File type and size validation in `BrowseButton_Click` must continue to work
- Auto-population of the document name from the selected filename must continue to work
- All existing steering document sections (§1–§10) must remain intact

**Scope:**
All inputs that do NOT involve opening the popup window or browsing for a file should be completely unaffected by this fix. This includes:
- Main window navigation (sidebar buttons)
- Content area rendering (`DocumentRepositoryControl`, `DocumentDetailView`)
- UI scaling (slider, presets, DPI changes)
- All existing steering document content

## Hypothesized Root Cause

Based on the bug description, the most likely issues are:

1. **Missing chromeless Window properties**: `ShowUploadDialog` creates `new Window { ... }` without setting `WindowStyle = WindowStyle.None`, `AllowsTransparency = true`, or `Background = Brushes.Transparent`. WPF defaults to `WindowStyle.SingleBorderWindow`, which renders the standard title bar and square frame, clipping the UserControl's rounded corners.

2. **Missing owner on OpenFileDialog**: `BrowseButton_Click` calls `dialog.ShowDialog()` (the parameterless overload). The `Microsoft.Win32.OpenFileDialog.ShowDialog()` overload without a `Window` parameter uses `null` as the owner, which means the OS has no parent window to anchor the file dialog to. This causes z-order issues with the already-modal popup.

3. **Steering document omission**: §4 "Dialog hosting" shows the `Window` creation pattern but only includes `SizeToContent`, `WindowStartupLocation`, and `Owner`. It does not mention `WindowStyle`, `AllowsTransparency`, `Background`, or `ResizeMode` — the properties needed for chromeless popup hosting.

## Correctness Properties

Property 1: Bug Condition — Chromeless Popup Window

_For any_ call to `ShowUploadDialog` where a hosting `Window` is created for the `UploadDocumentDialog`, the fixed function SHALL configure the `Window` with `WindowStyle.None`, `AllowsTransparency = true`, `Background = Transparent`, and `ResizeMode = NoResize`, so that the UserControl's rounded `Border` is fully visible without default window chrome.

**Validates: Requirements 2.1**

Property 2: Bug Condition — File Dialog Owner

_For any_ invocation of `BrowseButton_Click` where `OpenFileDialog.ShowDialog()` is called, the fixed function SHALL pass the parent `Window` (obtained via `Window.GetWindow(this)`) as the owner parameter, so that the file dialog always appears in front of the modal popup.

**Validates: Requirements 2.2**

Property 3: Preservation — Modal and Callback Behavior

_For any_ interaction with the `UploadDocumentDialog` that does NOT involve the window chrome or file dialog z-order (form submission, cancellation, file validation, name auto-population), the fixed code SHALL produce exactly the same behavior as the original code, preserving all existing modal, validation, and callback functionality.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

Property 4: Bug Condition — Steering Document Completeness

_For any_ developer referencing the steering document for popup/dialog hosting conventions, the updated document SHALL include the required chromeless `Window` properties (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, `ResizeMode="NoResize"`) and the `OpenFileDialog` owner pattern, so that new popups are created correctly.

**Validates: Requirements 2.3**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `WpfUserControlFramework/DashboardWindow.xaml.cs`

**Function**: `ShowUploadDialog`

**Specific Changes**:
1. **Add chromeless properties**: Set `WindowStyle = WindowStyle.None`, `AllowsTransparency = true`, `Background = System.Windows.Media.Brushes.Transparent` on the hosting `Window`.
2. **Add ResizeMode**: Set `ResizeMode = ResizeMode.NoResize` to prevent resize handles on the chromeless window.

---

**File**: `WpfUserControlFramework/Controls/Suppliers/Documents/UploadDocumentDialog/UploadDocumentDialog.xaml.cs`

**Function**: `BrowseButton_Click`

**Specific Changes**:
3. **Pass owner to ShowDialog**: Change `dialog.ShowDialog()` to `dialog.ShowDialog(Window.GetWindow(this))` so the file dialog is owned by the hosting popup window.

---

**File**: `WpfUserControlFramework/Docs/SteeringDocument.md`

**Section**: §4 "Parent-Child Data Passing" → "Dialog hosting" subsection

**Specific Changes**:
4. **Update dialog hosting code sample**: Replace the current `Window` creation snippet with one that includes `WindowStyle = WindowStyle.None`, `AllowsTransparency = true`, `Background = Brushes.Transparent`, and `ResizeMode = ResizeMode.NoResize`.
5. **Add OpenFileDialog owner guidance**: Add a note or sub-section explaining that any `CommonDialog.ShowDialog()` call inside a popup must pass `Window.GetWindow(this)` as the owner to avoid z-order issues.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bugs on unfixed code, then verify the fixes work correctly and preserve existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bugs BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Inspect the `ShowUploadDialog` method to confirm the `Window` is created without chromeless properties. Inspect `BrowseButton_Click` to confirm `ShowDialog()` is called without an owner. Run the application and visually confirm square corners and file dialog z-order issues.

**Test Cases**:
1. **Square Corners Test**: Open the upload dialog and observe that the window has a title bar and square corners (will fail on unfixed code)
2. **File Dialog Z-Order Test**: Click "Browse..." inside the popup and observe that the file dialog may appear behind the modal (will fail on unfixed code)
3. **Steering Document Audit**: Search §4 for `WindowStyle`, `AllowsTransparency`, `Background` — confirm they are absent (will fail on unfixed code)

**Expected Counterexamples**:
- The hosting `Window` object has `WindowStyle = SingleBorderWindow` (default), `AllowsTransparency = false` (default)
- `OpenFileDialog.ShowDialog()` is called with zero arguments (no owner)
- §4 code sample does not include chromeless properties

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  IF input.action == "open_popup" THEN
    window := ShowUploadDialog_fixed(input)
    ASSERT window.WindowStyle == None
    ASSERT window.AllowsTransparency == true
    ASSERT window.Background == Transparent
    ASSERT window.ResizeMode == NoResize
  END IF

  IF input.action == "browse_file" THEN
    result := BrowseButton_Click_fixed(input)
    ASSERT openFileDialog.ShowDialog was called with owner == Window.GetWindow(this)
  END IF
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT ShowUploadDialog_original(input).Owner == ShowUploadDialog_fixed(input).Owner
  ASSERT ShowUploadDialog_original(input).WindowStartupLocation == ShowUploadDialog_fixed(input).WindowStartupLocation
  ASSERT ShowUploadDialog_original(input).SizeToContent == ShowUploadDialog_fixed(input).SizeToContent
  ASSERT ValidateForm_original(input) == ValidateForm_fixed(input)
  ASSERT onDocumentUploaded callback behavior is unchanged
  ASSERT onCancel callback behavior is unchanged
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for form validation, upload callback, and cancel callback, then write tests capturing that behavior.

**Test Cases**:
1. **Modal Centering Preservation**: Verify the popup still opens centered on the owner with `WindowStartupLocation.CenterOwner` and `Owner = this`
2. **Form Validation Preservation**: Verify that submitting with empty name or no file still shows validation errors
3. **Upload Callback Preservation**: Verify that a valid submission still creates a correct `DocumentModel` and invokes the callback
4. **Cancel Callback Preservation**: Verify that clicking Cancel or ✕ still closes the dialog via `onCancel`
5. **File Validation Preservation**: Verify that selecting an unsupported file type or oversized file still shows the appropriate error

### Unit Tests

- Test that `ShowUploadDialog` creates a `Window` with `WindowStyle.None`, `AllowsTransparency = true`, `Background = Transparent`, `ResizeMode = NoResize`
- Test that `BrowseButton_Click` calls `OpenFileDialog.ShowDialog(owner)` with a non-null owner
- Test that form validation continues to reject empty names and missing files
- Test that the steering document §4 contains chromeless window properties

### Property-Based Tests

- Generate random form inputs (name, category, file path) and verify that validation behavior is identical before and after the fix
- Generate random dialog open/close sequences and verify modal behavior is preserved
- Verify that for all valid file selections, the `DocumentModel` produced is identical before and after the fix

### Integration Tests

- Open the upload dialog, verify rounded corners are visible (no title bar)
- Open the upload dialog, click Browse, verify the file dialog appears in front
- Open the upload dialog, fill in all fields, click Upload, verify the callback fires and the dialog closes
- Open the upload dialog, click Cancel, verify the dialog closes
