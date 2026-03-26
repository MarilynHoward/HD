# Popup Scrim Overlay Bugfix Design

## Overview

When a modal dialog (e.g., `UploadDocumentDialog`) is opened via `ShowUploadDialog()` in `DashboardWindow`, no semi-transparent scrim overlay dims the background content. The fix adds a `Border` element to the root `Grid` in `DashboardWindow.xaml` that spans all columns, is `Collapsed` by default, and is toggled to `Visible`/`Collapsed` in the `ShowUploadDialog()` code-behind method before and after `ShowDialog()`.

## Glossary

- **Bug_Condition (C)**: A modal dialog is shown via `ShowUploadDialog()` but no scrim overlay is rendered over the `DashboardWindow` content.
- **Property (P)**: A semi-transparent dark overlay covers the entire `DashboardWindow` (sidebar + content area) while the dialog is open, and is hidden when the dialog closes.
- **Preservation**: All existing behavior — sidebar navigation, UI scale controls, dialog callbacks, and normal content rendering — must remain unchanged.
- **ScrimOverlay**: A `Border` element in `DashboardWindow.xaml`'s root `Grid` with a semi-transparent black `Background`, spanning all columns, `Collapsed` by default.
- **ShowUploadDialog()**: The method in `DashboardWindow.xaml.cs` that creates and shows the `UploadDocumentDialog` in a modal `Window`.

## Bug Details

### Bug Condition

The bug manifests when `ShowUploadDialog()` is called. The method creates a modal `Window` containing the `UploadDocumentDialog` and calls `ShowDialog()`, but no overlay element exists in the XAML tree and no code toggles any overlay visibility. The user sees the dialog floating over a fully visible, undimmed background.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type ShowDialogInvocation
  OUTPUT: boolean

  RETURN input.method == "ShowUploadDialog"
         AND input.dialogWindow != null
         AND input.dialogWindow.ShowDialog() is called
         AND scrimOverlayElement does NOT exist in DashboardWindow.xaml root Grid
         OR scrimOverlayElement.Visibility != Visibility.Visible
END FUNCTION
```

### Examples

- User clicks "Upload Document" button → `ShowUploadDialog()` fires → dialog appears but background is fully visible with no dimming. **Expected**: Background should be dimmed by a semi-transparent overlay.
- User completes the upload and dialog closes → no overlay to hide because none was shown. **Expected**: Overlay should collapse, restoring the normal view.
- User cancels the dialog → same issue, no overlay transition. **Expected**: Overlay should collapse on cancel as well.
- No dialog is open → background renders normally. **Expected**: No change — scrim element is `Collapsed` and invisible.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Mouse clicks on sidebar navigation buttons must continue to navigate between views exactly as before.
- UI scale slider and preset buttons must continue to adjust `UiScaleState.FontScale` correctly.
- `UploadDocumentDialog` must continue to open as a modal `Window` with `WindowStartupLocation.CenterOwner` and `Owner = this`.
- The `onDocumentUploaded` and `onCancel` callbacks must continue to be invoked correctly when the dialog completes or is cancelled.
- When no dialog is open, the `DashboardWindow` content (sidebar and `ContentArea`) must render without any overlay or dimming.

**Scope:**
All interactions that do NOT involve opening a modal dialog should be completely unaffected by this fix. This includes:
- Sidebar navigation clicks
- UI scale adjustments (slider and preset buttons)
- Content area rendering and user control interactions
- Window resize, move, and close operations

## Hypothesized Root Cause

Based on the bug description, the root cause is straightforward — the scrim overlay was never implemented:

1. **Missing XAML Element**: The root `Grid` in `DashboardWindow.xaml` contains only two children (the sidebar `Border` in column 0 and the `ContentControl` in column 1). There is no overlay element defined anywhere in the visual tree.

2. **Missing Show/Hide Logic**: The `ShowUploadDialog()` method in `DashboardWindow.xaml.cs` creates the dialog window and calls `ShowDialog()` directly, with no code to toggle any overlay visibility before or after the call.

3. **No Omission in Grid.ColumnSpan**: Since the overlay element doesn't exist at all, there is no issue with column spanning — the element simply needs to be added with `Grid.ColumnSpan="2"` to cover both the sidebar and content area.

## Correctness Properties

Property 1: Bug Condition - Scrim Overlay Visible During Modal Dialog

_For any_ invocation of `ShowUploadDialog()` where a modal dialog is displayed, the fixed code SHALL make the `ScrimOverlay` element `Visible` before `ShowDialog()` is called and `Collapsed` after `ShowDialog()` returns, ensuring the background is dimmed while the dialog is open.

**Validates: Requirements 2.1, 2.2**

Property 2: Preservation - Normal Rendering Without Dialog

_For any_ state where no modal dialog is open, the fixed code SHALL keep the `ScrimOverlay` element `Collapsed`, producing the same visual rendering as the original code, preserving sidebar navigation, UI scale controls, content area display, and all non-dialog interactions.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `WpfUserControlFramework/DashboardWindow.xaml`

**Location**: Inside the root `<Grid>`, after the `ContentControl` element.

**Specific Changes**:
1. **Add ScrimOverlay Border**: Add a `Border` element named `ScrimOverlay` with:
   - `Grid.ColumnSpan="2"` to span both the sidebar and content columns
   - `Background="#80000000"` (black at 50% opacity) for the semi-transparent dimming effect
   - `Visibility="Collapsed"` so it is hidden by default
   - `Panel.ZIndex="10"` to ensure it renders above the sidebar and content area but below the dialog window

**File**: `WpfUserControlFramework/DashboardWindow.xaml.cs`

**Function**: `ShowUploadDialog(Action<DocumentModel> onUploaded)`

**Specific Changes**:
2. **Show scrim before dialog**: Add `ScrimOverlay.Visibility = Visibility.Visible;` immediately before the `dialogWindow.ShowDialog()` call.
3. **Hide scrim after dialog**: Add `ScrimOverlay.Visibility = Visibility.Collapsed;` immediately after the `dialogWindow.ShowDialog()` call. Since `ShowDialog()` is blocking, this line executes only after the dialog is closed (whether by upload completion or cancellation).

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm that no scrim element exists and no visibility toggling occurs.

**Test Plan**: Write tests that invoke `ShowUploadDialog()` and inspect the visual tree of `DashboardWindow` for a scrim overlay element. Run these tests on the UNFIXED code to confirm the element is missing.

**Test Cases**:
1. **Missing Element Test**: Inspect `DashboardWindow`'s root Grid children for any element named `ScrimOverlay` (will fail on unfixed code — element does not exist)
2. **No Dimming Test**: Open dialog and check if any overlay with semi-transparent background is visible (will fail on unfixed code — no overlay rendered)
3. **Post-Close State Test**: Close dialog and verify no orphaned overlay remains (trivially passes on unfixed code since no overlay exists)

**Expected Counterexamples**:
- `FindName("ScrimOverlay")` returns `null` on the unfixed `DashboardWindow`
- No child of the root Grid has a semi-transparent background during dialog display

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := ShowUploadDialog_fixed(input)
  ASSERT ScrimOverlay.Visibility == Visibility.Visible BEFORE ShowDialog()
  ASSERT ScrimOverlay.Visibility == Visibility.Collapsed AFTER ShowDialog() returns
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT DashboardWindow_original(input) renders identically to DashboardWindow_fixed(input)
  ASSERT ScrimOverlay.Visibility == Visibility.Collapsed
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-dialog interactions

**Test Plan**: Observe behavior on UNFIXED code first for sidebar navigation, UI scale changes, and content rendering, then write tests capturing that behavior.

**Test Cases**:
1. **Sidebar Navigation Preservation**: Verify clicking nav buttons continues to switch content views correctly after the scrim element is added to the XAML tree
2. **UI Scale Preservation**: Verify slider and preset buttons continue to update `UiScaleState.FontScale` correctly
3. **Dialog Callback Preservation**: Verify `onDocumentUploaded` and `onCancel` callbacks still fire correctly
4. **Default State Preservation**: Verify `ScrimOverlay` is `Collapsed` on window load and during all non-dialog interactions

### Unit Tests

- Test that `ScrimOverlay` element exists in the visual tree with correct properties (`Grid.ColumnSpan`, `Background`, default `Visibility`)
- Test that `ShowUploadDialog()` sets `ScrimOverlay.Visibility` to `Visible` before `ShowDialog()`
- Test that `ShowUploadDialog()` sets `ScrimOverlay.Visibility` to `Collapsed` after `ShowDialog()` returns
- Test edge case: rapid open/close cycles don't leave the scrim in a visible state

### Property-Based Tests

- Generate random sequences of dialog open/close operations and verify scrim state is always consistent (visible during dialog, collapsed otherwise)
- Generate random non-dialog interactions (nav clicks, scale changes) and verify scrim remains collapsed throughout
- Test that scrim visibility state is always `Collapsed` when no dialog is active, across many random interaction sequences

### Integration Tests

- Test full flow: click upload button → scrim appears → complete upload → scrim disappears → content renders normally
- Test full flow: click upload button → scrim appears → cancel → scrim disappears → content renders normally
- Test that sidebar navigation works correctly both before and after a dialog open/close cycle
