# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Missing Scrim Overlay During Modal Dialog
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to the concrete failing case: invoking `ShowUploadDialog()` on the current `DashboardWindow`
  - Test that `DashboardWindow`'s root Grid contains a child element named `ScrimOverlay` (from Bug Condition `isBugCondition`: scrimOverlayElement does NOT exist in root Grid)
  - Test that `ScrimOverlay` has `Grid.ColumnSpan="2"`, `Background="#80000000"`, and `Panel.ZIndex="10"`
  - Test that when `ShowUploadDialog()` is invoked, `ScrimOverlay.Visibility` is set to `Visible` before `ShowDialog()` is called
  - Test that after `ShowDialog()` returns, `ScrimOverlay.Visibility` is set to `Collapsed`
  - Run test on UNFIXED code - expect FAILURE (element does not exist, `FindName("ScrimOverlay")` returns null)
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bug exists: no scrim overlay element in the visual tree)
  - Document counterexamples found (e.g., "`FindName("ScrimOverlay")` returns null on unfixed DashboardWindow")
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Normal Rendering and Interactions Without Dialog
  - **IMPORTANT**: Follow observation-first methodology
  - Observe on UNFIXED code: sidebar navigation buttons switch content views correctly via `NavigateTo()`
  - Observe on UNFIXED code: `ScaleSlider.Value` changes update `UiScaleState.FontScale` and `ScalePercentageLabel.Text`
  - Observe on UNFIXED code: preset buttons (75%, 100%, 125%, 150%) set `ScaleSlider.Value` to the correct preset
  - Observe on UNFIXED code: `UploadDocumentDialog` opens as modal with `WindowStartupLocation.CenterOwner` and `Owner = this`
  - Observe on UNFIXED code: `onDocumentUploaded` and `onCancel` callbacks fire correctly when dialog completes or is cancelled
  - Write property-based tests: for all non-dialog interactions (nav clicks, scale changes), the DashboardWindow renders without any overlay or dimming
  - Write property-based tests: for dialog open/close, callbacks continue to be invoked correctly and dialog properties are unchanged
  - Verify tests PASS on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Fix for missing scrim overlay during modal dialog

  - [x] 3.1 Add ScrimOverlay Border element to DashboardWindow.xaml
    - Add a `Border` element named `ScrimOverlay` inside the root `<Grid>`, after the `ContentControl` element
    - Set `Grid.ColumnSpan="2"` to span both sidebar and content columns
    - Set `Background="#80000000"` (black at 50% opacity)
    - Set `Visibility="Collapsed"` (hidden by default)
    - Set `Panel.ZIndex="10"` to render above sidebar and content area
    - _Bug_Condition: isBugCondition(input) where scrimOverlayElement does NOT exist in DashboardWindow.xaml root Grid_
    - _Expected_Behavior: ScrimOverlay element exists in root Grid with ColumnSpan=2, semi-transparent background, Collapsed by default, ZIndex=10_
    - _Preservation: When no dialog is open, ScrimOverlay is Collapsed and does not affect rendering or interactions (Requirements 3.1, 3.4)_
    - _Requirements: 1.2, 2.2_

  - [x] 3.2 Toggle ScrimOverlay visibility in ShowUploadDialog()
    - In `DashboardWindow.xaml.cs`, in the `ShowUploadDialog(Action<DocumentModel> onUploaded)` method:
    - Add `ScrimOverlay.Visibility = Visibility.Visible;` immediately before `dialogWindow.ShowDialog();`
    - Add `ScrimOverlay.Visibility = Visibility.Collapsed;` immediately after `dialogWindow.ShowDialog();`
    - Since `ShowDialog()` is blocking, the Collapsed line executes only after the dialog closes (upload or cancel)
    - _Bug_Condition: isBugCondition(input) where ShowUploadDialog is called AND ScrimOverlay.Visibility != Visible_
    - _Expected_Behavior: ScrimOverlay.Visibility = Visible before ShowDialog(), Collapsed after ShowDialog() returns_
    - _Preservation: Dialog continues to open as modal with CenterOwner, callbacks continue to fire correctly (Requirements 3.2, 3.3)_
    - _Requirements: 1.1, 2.1_

  - [x] 3.3 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Scrim Overlay Visible During Modal Dialog
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior: ScrimOverlay exists, is Visible during dialog, Collapsed after
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2_

  - [x] 3.4 Verify preservation tests still pass
    - **Property 2: Preservation** - Normal Rendering and Interactions Without Dialog
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
