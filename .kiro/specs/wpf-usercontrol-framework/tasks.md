# Implementation Plan: WPF UserControl Framework

## Overview

Incrementally build a .NET 8 WPF user control testing framework. Start with the solution skeleton and shared stylesheet, then build each control (Document Repository → Upload Dialog → Detail View), wire navigation in DashboardWindow, and finish with the steering document. Property-based tests use FsCheck + xUnit.

## Tasks

- [x] 1. Create solution structure and project scaffolding
  - [x] 1.1 Create the .NET 8 WPF solution and project files
    - Create `WpfUserControlFramework.sln` and `WpfUserControlFramework/WpfUserControlFramework.csproj` targeting `net8.0-windows` with `<UseWPF>true</UseWPF>`
    - Create the `Controls/DocumentRepository/`, `Controls/UploadDocumentDialog/`, `Controls/DocumentDetailView/`, and `Docs/` directories
    - _Requirements: 1.1, 1.2, 1.4_

  - [x] 1.2 Create App.xaml and App.xaml.cs
    - Implement `App.xaml` with `StartupUri="DashboardWindow.xaml"`, merge `PeoplePosTheme.xaml`, define global `FontFamily` styles for `TextBlock` and `Control` as "Segoe UI", and add all base layout constants (`UiFontScale`, `BaseNavWidth`, `BaseButtonHeight`, `BaseControlGap`, padding values, `BaseTextBoxHeight`, `BaseHeaderHeight`, etc.) as `sys:Double` resources
    - Implement `App.xaml.cs` with namespace `RestaurantPosWpf`
    - _Requirements: 3.2, 3.9, 1.1_

  - [x] 1.3 Create PeoplePosTheme.xaml shared ResourceDictionary
    - Place `PeoplePosTheme.xaml` at the project root (not in a Themes/ subfolder)
    - Include all colors/brushes: MainForeground (#111827), DimmedForeground (#687280), FocusInner (#93C5FD), FocusOuter (#C1DEFE), category badge colors
    - Include converters: `WidthMultiplierConverter`, `FontScaleConverter`, `LayoutScaleConverter`, `ThicknessScaleConverter`, `CornerRadiusFromHeightConverter`, `ScaledDoubleConverter`
    - Include `UiScaleState` binding source
    - Include control styles: `VcPrimaryButtonStyle`, `VcSecondaryButtonStyle`, `IcEnabledComboBoxStyle`, `IcEnabledTextBoxStyle`, `IcBaseTextBoxStyle`, `IcBaseComboBoxStyle`
    - Include focus ring template with inner #93C5FD and outer #C1DEFE borders
    - Use the content from the attached PeoplePosTheme.txt as the actual ResourceDictionary content
    - _Requirements: 3.1, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 6.1, 6.2_

  - [x] 1.4 Create the xUnit + FsCheck test project
    - Create `WpfUserControlFramework.Tests/WpfUserControlFramework.Tests.csproj` referencing the main project, with packages: `xunit`, `xunit.runner.visualstudio`, `FsCheck`, `FsCheck.Xunit`, `Microsoft.NET.Test.Sdk`
    - _Requirements: (testing infrastructure)_

- [x] 2. Checkpoint - Verify solution builds
  - Ensure the solution compiles with no errors, ask the user if questions arise.

- [x] 3. Implement DocumentRepositoryControl
  - [x] 3.1 Create DocumentRepositoryControl code-behind with models and demo data
    - Define `DocumentModel` class with properties: `Id`, `Name`, `Category`, `FileType`, `FileSize`, `UploadDate`, `Notes`, `FileName` (all with defaults)
    - Define `DocumentCategories` static class with `Compliance`, `Legal`, `Banking` constants and `All` list
    - Define `SummaryCardData` record with `Title`, `Count`, `ColorKey`
    - Implement `GetDemoDocuments()` static method returning sample documents across all three categories
    - Implement `ComputeSummaries(List<DocumentModel>)` method
    - Constructor accepts `Action<DocumentModel> onNavigateToDetail` and `Action<Action<DocumentModel>> onRequestUploadDialog` with null-guard checks
    - Implement logic to add a new document from the upload callback and refresh summary cards
    - _Requirements: 1.2, 1.3, 2.1, 2.3, 7.1, 7.3, 7.5, 7.6_

  - [x] 3.2 Create DocumentRepositoryControl XAML
    - Root border using the Root_Border pattern with UiScaleState bindings
    - 4 summary cards (Total Documents, Compliance, Legal, Banking) bound to computed counts
    - Scrollable document list with columns: Name, Category (badge), File Type, Size, Upload Date
    - Category badges with colored backgrounds per category
    - "Upload Document" button using `VcPrimaryButtonStyle`
    - Empty state message when no documents
    - Inline styles in `<UserControl.Resources>` for control-specific styling (category badge colors, card layout)
    - _Requirements: 4.1, 4.2, 5.1, 5.2, 7.1, 7.2, 7.4_

  - [ ]* 3.3 Write property test: Summary card count correctness (Property 3)
    - **Property 3: Summary card count correctness**
    - Generate random lists of `DocumentModel` with random categories, compute summaries, verify Total == list.Count and per-category counts match LINQ aggregations. Add a random document and verify counts update by exactly one.
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - **Validates: Requirements 7.1, 7.6**

  - [ ]* 3.4 Write unit tests for DocumentRepositoryControl
    - Test demo data is non-empty (Req 1.3, 7.3)
    - Test demo data covers all three categories (Req 7.1)
    - Test summary cards with empty list return zero counts (Req 7.1 edge case)
    - _Requirements: 1.3, 7.1, 7.3_

- [x] 4. Implement UploadDocumentDialog
  - [x] 4.1 Create UploadDocumentDialog code-behind with validation
    - Define `ValidationResult` record with `IsValid` and `ErrorMessage`
    - Constructor accepts `IEnumerable<string> categories`, `Action<DocumentModel> onDocumentUploaded`, `Action onCancel` with null-guard checks
    - Implement `ValidateForm()` returning `ValidationResult` — Document Name required (non-empty, non-whitespace)
    - Implement submit logic: validate, create `DocumentModel`, invoke `onDocumentUploaded` callback, close
    - Implement cancel logic: invoke `onCancel` callback
    - Pre-select first category as default
    - Simulate file selection (placeholder file name and size)
    - _Requirements: 2.1, 2.2, 2.3, 8.1, 8.2, 8.6, 8.7, 8.8_

  - [x] 4.2 Create UploadDocumentDialog XAML
    - Root border using the Root_Border pattern with UiScaleState bindings
    - Document Name field using `IcEnabledTextBoxStyle`
    - Category dropdown using `IcEnabledComboBoxStyle` with Compliance, Legal, Banking options
    - File selector area with drag-and-drop visual pattern (placeholder/simulated)
    - Notes multiline text field using `IcEnabledTextBoxStyle`
    - Cancel button using `VcSecondaryButtonStyle`, Upload Document button using `VcPrimaryButtonStyle`
    - Inline validation message below Document Name field
    - Inline styles in `<UserControl.Resources>` for dialog-specific styling
    - _Requirements: 4.1, 4.2, 5.1, 5.2, 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ]* 4.3 Write property test: Callback data fidelity on valid submission (Property 2)
    - **Property 2: Callback data fidelity on valid submission**
    - Generate random valid form inputs (non-empty name, random category from allowed set, random notes), trigger submit, verify callback receives `DocumentModel` with matching `Name`, `Category`, and `Notes`.
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - **Validates: Requirements 2.2, 8.7**

  - [ ]* 4.4 Write property test: Empty or whitespace name fails validation (Property 5)
    - **Property 5: Empty or whitespace document name fails validation**
    - Generate random whitespace-only strings (spaces, tabs, newlines, empty), attempt validation, verify `IsValid` is false and error message is produced.
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - **Validates: Requirements 8.8**

  - [ ]* 4.5 Write unit tests for UploadDocumentDialog
    - Test cancel does not invoke `onDocumentUploaded` callback (Req 8.6)
    - Test category dropdown contains exactly ["Compliance", "Legal", "Banking"] (Req 8.2)
    - _Requirements: 8.2, 8.6_

- [x] 5. Checkpoint - Verify controls build and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement DocumentDetailView
  - [x] 6.1 Create DocumentDetailView code-behind
    - Constructor accepts `DocumentModel document` and `Action onBack` with null-guard checks (`ArgumentNullException`)
    - Store document reference and expose fields for binding
    - Implement back button handler invoking `onBack` callback
    - _Requirements: 2.1, 2.3, 9.1, 9.2_

  - [x] 6.2 Create DocumentDetailView XAML
    - Root border using the Root_Border pattern with UiScaleState bindings
    - Read-only display of: Name, Category, File Type, Size, Upload Date, Notes
    - Back button using `VcSecondaryButtonStyle`
    - Inline styles in `<UserControl.Resources>` for detail-specific styling
    - _Requirements: 4.1, 4.2, 5.1, 5.2, 9.1_

  - [ ]* 6.3 Write property test: Constructor data round-trip (Property 1)
    - **Property 1: Constructor data round-trip**
    - Generate random `DocumentModel` instances via FsCheck custom `Arbitrary<DocumentModel>`, pass to `DocumentDetailView` constructor, verify all fields on the view match the original model.
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - **Validates: Requirements 2.1, 9.1, 9.2**

  - [ ]* 6.4 Write unit test for DocumentDetailView
    - Test document fields are accessible after construction with a known document (Req 9.1)
    - _Requirements: 9.1_

- [x] 7. Wire navigation in DashboardWindow
  - [x] 7.1 Create DashboardWindow.xaml and DashboardWindow.xaml.cs
    - XAML: Window with a `ContentControl` named `ContentArea` as the main content host
    - Code-behind: Instantiate `DocumentRepositoryControl` with `onNavigateToDetail` and `onRequestUploadDialog` callbacks
    - Implement `ShowDocumentDetail(DocumentModel)` — swap `ContentArea.Content` to a new `DocumentDetailView` with `onBack` callback that restores the repository
    - Implement `ShowUploadDialog(Action<DocumentModel>)` — create a `Window` hosting `UploadDocumentDialog` as a modal dialog per the dialog hosting pattern, pass categories and callbacks
    - _Requirements: 2.1, 2.2, 7.4, 7.5, 7.6, 8.6, 8.7_

  - [ ]* 7.2 Write property test: Document selection triggers navigation (Property 4)
    - **Property 4: Document selection triggers navigation with correct document**
    - Generate a random document list, pick a random document, trigger selection callback, verify the callback receives the same document.
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - **Validates: Requirements 7.4**

- [x] 8. Checkpoint - Full application runs and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Create Steering Document
  - [x] 9.1 Write Docs/SteeringDocument.md
    - Describe Shared_Stylesheet usage conventions (how to reference styles from PeoplePosTheme.xaml, when to use inline styles)
    - Describe Focus_Ring pattern: inner #93C5FD, outer #C1DEFE, when to apply
    - Describe Root_Border pattern with UiScaleState bindings and Border structure
    - Describe parent-child data passing convention: constructor injection for data in, `Action<T>` callbacks for data out
    - List all shared style references with usage guidance: `VcPrimaryButtonStyle`, `VcSecondaryButtonStyle`, `IcEnabledComboBoxStyle`, `IcEnabledTextBoxStyle`
    - Describe Inline_Stylesheet convention for control-specific styles
    - Describe Demo_Data convention: all models, constants, and demo data reside in the owning control's code-behind — no separate Models/ or DemoData/ folders
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_

- [x] 10. Create FsCheck custom generator and shared test utilities
  - [x] 10.1 Implement custom `Arbitrary<DocumentModel>` generator
    - Generate `Name` as non-null strings, `Category` from `DocumentCategories.All`, `FileType` from ["PDF", "DOCX", "XLSX", "TXT"], `FileSize` as non-negative long, `UploadDate` within reasonable range, `Notes` and `FileName` as arbitrary non-null strings
    - Place in test project as a shared utility for all property tests
    - _Requirements: (testing infrastructure)_

- [x] 11. Final verification and resource dictionary tests
  - [ ]* 11.1 Write unit tests for PeoplePosTheme.xaml
    - Verify style keys exist: `VcPrimaryButtonStyle`, `VcSecondaryButtonStyle`, `IcEnabledComboBoxStyle`, `IcEnabledTextBoxStyle` (Req 3.5–3.8)
    - Verify MainForeground is #111827 and DimmedForeground is #687280 (Req 3.3, 3.4)
    - Verify focus ring color resources exist (Req 6.2)
    - _Requirements: 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 6.2_

  - [x] 11.2 Final checkpoint - All tests pass and application runs
    - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- All C# code uses namespace `RestaurantPosWpf`
- PeoplePosTheme.xaml content comes from the attached PeoplePosTheme.txt file
