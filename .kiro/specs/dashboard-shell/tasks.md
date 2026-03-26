# Implementation Plan: Dashboard Shell

## Overview

Replace the current bare `ContentControl` layout in `DashboardWindow` with a two-panel shell: a sidebar with navigation items and a scale adjuster on the left, and a content area on the right that swaps the active `UserControl`. All implementation uses the existing code-behind pattern, scaling converters, and project conventions.

## Tasks

- [x] 1. Define NavItem record and navigation registry in DashboardWindow
  - [x] 1.1 Add the `NavItem` record and `_navItems` list to `DashboardWindow.xaml.cs`
    - Define `public record NavItem(string Label, Func<UserControl> Factory)` in `DashboardWindow.xaml.cs`
    - Initialize `_navItems` with a "Documents" entry whose factory creates `DocumentRepositoryControl` with `onRequestUploadDialog: ShowUploadDialog`
    - Add `_selectedIndex` field to track the active nav item
    - Add `NavigateTo(int index)` method that calls the factory, sets `ContentArea.Content`, and updates `_selectedIndex`
    - Include error handling: catch factory exceptions and null returns, leave `ContentArea.Content` unchanged, log via `Debug.Print`
    - _Requirements: 4.1, 4.2, 3.1, 3.2, 3.3, 10.1_

  - [ ]* 1.2 Write property test for navigation item count (Property 1)
    - **Property 1: Navigation item count matches registry**
    - Generate random lists of `NavItem` records (0–20 items), verify rendered button count equals list length
    - **Validates: Requirements 2.1**

  - [ ]* 1.3 Write property test for navigation activation (Property 2)
    - **Property 2: Navigation activation sets content to factory result**
    - Generate a random registry (1–10 items), pick a random index, verify `ContentArea.Content` matches factory output after `NavigateTo`
    - **Validates: Requirements 3.1, 7.3**

- [x] 2. Build the two-panel shell layout in DashboardWindow XAML
  - [x] 2.1 Replace DashboardWindow XAML with two-column Grid layout
    - Replace the single `ContentControl` with a two-column `Grid` (Auto sidebar, * content)
    - Left column: `Border` containing a `DockPanel` for sidebar content (nav list top, scale adjuster bottom)
    - Right column: `ContentControl x:Name="ContentArea"`
    - Sidebar width scales via `LayoutScaleConverter` bound to `UiScaleState.FontScale`
    - Sidebar padding scales via `ThicknessScaleConverter`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 8.1_

  - [x] 2.2 Define NavButtonStyle in DashboardWindow resources
    - Create `Style x:Key="NavButtonStyle" TargetType="Button"` with transparent default background, `#EFF6FF` selected, `#F3F4F6` hover
    - Foreground: `#374151` default, `#1D4ED8` selected; FontWeight: Normal default, SemiBold selected
    - CornerRadius 6, padding scaled via `ThicknessScaleConverter` with `'10,8,10,8'`
    - `HorizontalContentAlignment="Left"`, `FocusVisualStyle="{x:Null}"`
    - Include focus ring borders (`FocusOuterRing` / `FocusInnerRing`) visible on `IsKeyboardFocused`
    - Font size scaled via `FontScaleConverter`
    - _Requirements: 2.2, 2.3, 7.1, 7.2, 7.3, 8.2_

  - [x] 2.3 Populate sidebar navigation from registry in code-behind
    - In `DashboardWindow.xaml.cs`, programmatically create `Button` elements from `_navItems` using `NavButtonStyle`
    - Store each button's index in `Tag`; wire `Click` handler to resolve index and call `NavigateTo`
    - On load, call `NavigateTo(0)` to display the first nav item by default
    - Add keyboard handling: Enter/Space on focused nav button activates navigation
    - _Requirements: 2.1, 2.4, 3.2, 4.2, 4.3, 7.1, 7.3_

- [x] 3. Implement the Scale Adjuster in the sidebar
  - [x] 3.1 Add Scale Adjuster UI to the sidebar bottom
    - Add a `StackPanel` docked to the bottom of the sidebar `DockPanel`
    - Include a `TextBlock` showing current percentage (e.g., "100%")
    - Include a `Slider` with `Minimum=0.6`, `Maximum=1.8`, `TickFrequency=0.05`
    - Include four preset `Button` elements (75%, 100%, 125%, 150%) in a horizontal `StackPanel`
    - Slider width scales via `LayoutScaleConverter`; preset buttons use `VcSecondaryButtonStyle` or lightweight variant
    - Preset buttons display focus ring on keyboard focus
    - _Requirements: 5.1, 5.2, 5.4, 5.5, 7.4, 7.5, 8.3_

  - [x] 3.2 Wire Scale Adjuster event handlers in code-behind
    - `ScaleSlider_ValueChanged`: write `slider.Value` to `UiScaleState.FontScale` via the application resource
    - `PresetButton_Click`: set slider value to the preset (0.75, 1.0, 1.25, 1.5), which triggers the slider handler
    - Initialize slider to current `UiScaleState.FontScale` on load
    - Update percentage label in real time as slider moves: `Math.Round(value * 100) + "%"`
    - _Requirements: 5.2, 5.3, 5.4, 5.5, 6.1, 6.3_

  - [x] 3.3 Handle DPI change sync for Scale Adjuster
    - Subscribe to `UiScaleService.DpiChanged` — on fire, read `UiScaleService.FontScale` and update slider position
    - Guard against slider not yet loaded
    - _Requirements: 6.2_

  - [ ]* 3.4 Write property test for scale value round-trip (Property 3)
    - **Property 3: Scale value round-trip with clamping**
    - Generate random doubles in [-1.0, 3.0], write to slider, verify `UiScaleState.FontScale` equals `Clamp(value, 0.6, 1.8)`
    - **Validates: Requirements 5.2, 5.3**

  - [ ]* 3.5 Write property test for percentage label formatting (Property 4)
    - **Property 4: Percentage label formatting**
    - Generate random doubles in [0.6, 1.8], verify label text equals `$"{Math.Round(value * 100)}%"`
    - **Validates: Requirements 5.4**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update existing controls to use Content Root Border pattern
  - [x] 5.1 Update `DocumentRepositoryControl.xaml` root border to Content Root Border
    - Replace the current root `Border` (white background, `ScaleTransform`, `CornerRadius="8"`, padding `'24'`) with the Content Root Border pattern
    - Set `BorderBrush="#E5E7EB"`, `BorderThickness="1"`, `Background="#F8FAFC"`, `SnapsToDevicePixels="True"`, `ClipToBounds="True"`, `FocusVisualStyle="{x:Null}"`
    - Padding via `ThicknessScaleConverter` with `ConverterParameter='28,24,28,24'`
    - Corner radius via `CornerRadiusFromHeightConverter` with `ConverterParameter="12"`
    - Margin via `ThicknessScaleConverter` with `ConverterParameter="0"`
    - Remove the `ScaleTransform` `RenderTransform` (scaling is now handled by converters, not transform)
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

  - [x] 5.2 Update `DocumentDetailView.xaml` root border to Content Root Border
    - Same Content Root Border changes as 5.1
    - Remove the `ScaleTransform` `RenderTransform`
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

  - [x] 5.3 Verify `UploadDocumentDialog` does NOT use Content Root Border
    - Confirm `UploadDocumentDialog.xaml` continues to use the popup/dialog root border pattern (white background, `ConverterParameter='24,16,24,20'`)
    - No changes needed — this is a verification step
    - _Requirements: 9.7_

- [x] 6. Wire dialog hosting through the shell
  - [x] 6.1 Ensure `ShowUploadDialog` in `DashboardWindow.xaml.cs` is passed to `DocumentRepositoryControl` via the nav registry factory
    - The factory in `_navItems` already passes `onRequestUploadDialog: ShowUploadDialog` — verify this wiring is correct
    - Confirm modal `Window` creation uses `SizeToContent.WidthAndHeight`, `WindowStartupLocation.CenterOwner`, `Owner = this`
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck.Xunit with `MaxTest = 100` and STA thread for WPF controls
- Tag format: `// Feature: dashboard-shell, Property {N}: {title}`
