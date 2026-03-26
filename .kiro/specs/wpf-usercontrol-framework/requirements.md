# Requirements Document

## Introduction

A .NET 8 WPF solution that serves as a user control testing framework. The framework provides a structured approach to building, testing, and demonstrating WPF User Controls with demo data, shared styling via a ResourceDictionary, and a parent-child data passing pattern using constructor injection and callback returns. The first two controls implemented are a Document Repository (main list view) and an Upload Document dialog. The framework is designed to be extensible so additional user controls can be added over time.

## Glossary

- **Framework**: The .NET 8 WPF solution that hosts and tests user controls
- **User_Control**: A WPF UserControl (.xaml + .xaml.cs code-behind kept together) that represents a self-contained UI component
- **Parent_Control**: A User_Control that instantiates and hosts a Child_Control
- **Child_Control**: A User_Control that is instantiated by a Parent_Control and receives data via its constructor
- **Shared_Stylesheet**: A WPF ResourceDictionary (PeoplePosTheme.xaml) containing all shared styles, colors, brushes, converters, and templates
- **Inline_Stylesheet**: Styles defined within a User_Control's XAML Resources section for control-specific styling
- **Document_Repository_Control**: The main User_Control displaying a summary dashboard and a list of documents with category badges, file type, size, and upload date
- **Upload_Document_Dialog**: A popup/dialog User_Control for adding a new document with name, category, file selector, and notes fields
- **Demo_Data**: Hard-coded sample data defined inside each User_Control's code-behind (.xaml.cs) to populate that control so users can experience how the application works without a backend
- **Steering_Document**: A reference document describing design patterns, conventions, shared stylesheet usage, focus ring patterns, root border patterns, data passing conventions, and style references used throughout the Framework
- **Summary_Card**: A UI element in the Document_Repository_Control that displays an aggregate count (Total Documents, Compliance, Legal, Banking)
- **Category_Badge**: A colored label on each document row indicating its category (Compliance, Legal, Banking)
- **Focus_Ring**: A two-border visual pattern using inner color #93C5FD and outer color #C1DEFE applied to focused interactive elements
- **Root_Border**: The main border element wrapping each User_Control's content, using a specific Border pattern with UiScaleState bindings
- **VcPrimaryButtonStyle**: The shared style key for primary action buttons
- **VcSecondaryButtonStyle**: The shared style key for secondary/cancel buttons
- **IcEnabledComboBoxStyle**: The shared style key for enabled ComboBox controls
- **IcEnabledTextBoxStyle**: The shared style key for enabled TextBox controls
- **UiScaleState**: A binding source used by the Root_Border to support UI scaling

## Requirements

### Requirement 1: Solution Structure

**User Story:** As a developer, I want a .NET 8 WPF solution structured as a user control testing framework, so that I can create, test, and demonstrate user controls in an organized and extensible manner.

#### Acceptance Criteria

1. THE Framework SHALL target .NET 8 and use the WPF application model.
2. THE Framework SHALL organize each User_Control as a pair of co-located files (.xaml and .xaml.cs code-behind) within the same directory.
3. THE Framework SHALL include Demo_Data within each User_Control's code-behind so that users can experience how the application works without external dependencies.
4. THE Framework SHALL support adding new User_Controls over time without requiring changes to existing User_Controls.

### Requirement 2: Parent-Child Data Passing

**User Story:** As a developer, I want a consistent data passing pattern between parent and child user controls, so that data flows predictably through the control hierarchy.

#### Acceptance Criteria

1. WHEN a Parent_Control instantiates a Child_Control, THE Parent_Control SHALL pass required data to the Child_Control via the Child_Control's constructor parameters.
2. WHEN a Child_Control completes its operation, THE Child_Control SHALL pass updated data back to the Parent_Control via a callback or return mechanism.
3. THE Framework SHALL ensure that each Child_Control constructor explicitly declares all data dependencies as parameters.

### Requirement 3: Shared Stylesheet

**User Story:** As a developer, I want a shared ResourceDictionary for the main look and feel, so that all user controls have a consistent visual appearance.

#### Acceptance Criteria

1. THE Framework SHALL include a Shared_Stylesheet (ResourceDictionary) containing all shared styles, colors, brushes, converters, and control templates.
2. THE Shared_Stylesheet SHALL define the default FontFamily as "Segoe UI".
3. THE Shared_Stylesheet SHALL define the main foreground color as #111827.
4. THE Shared_Stylesheet SHALL define the dimmed text color as #687280.
5. THE Shared_Stylesheet SHALL define VcPrimaryButtonStyle for primary action buttons.
6. THE Shared_Stylesheet SHALL define VcSecondaryButtonStyle for secondary action buttons.
7. THE Shared_Stylesheet SHALL define IcEnabledComboBoxStyle for enabled ComboBox controls.
8. THE Shared_Stylesheet SHALL define IcEnabledTextBoxStyle for enabled TextBox controls.
9. THE Framework SHALL merge the Shared_Stylesheet into the application-level resources via App.xaml, alongside global FontFamily styles and base layout constants, so that all User_Controls can reference shared styles.

### Requirement 4: Inline Styling Convention

**User Story:** As a developer, I want to use inline stylesheets within user controls for control-specific styles, so that specialized styling stays co-located with the control that uses it.

#### Acceptance Criteria

1. WHEN a User_Control requires styles beyond the Shared_Stylesheet, THE User_Control SHALL define those styles in its own XAML Resources section as an Inline_Stylesheet.
2. THE Inline_Stylesheet SHALL only contain styles specific to that User_Control and not duplicate styles from the Shared_Stylesheet.

### Requirement 5: Root Border Pattern

**User Story:** As a developer, I want each user control to use a consistent root border pattern, so that controls have a uniform framing and support UI scaling.

#### Acceptance Criteria

1. THE Root_Border of each User_Control SHALL use a Border element with UiScaleState bindings as the outermost content wrapper.
2. THE Root_Border pattern SHALL be consistent across all User_Controls in the Framework.

### Requirement 6: Focus Ring Pattern

**User Story:** As a developer, I want a consistent focus ring pattern for interactive elements, so that keyboard navigation is visually clear and accessible.

#### Acceptance Criteria

1. WHEN an interactive element receives keyboard focus, THE Framework SHALL display a Focus_Ring using an inner border with color #93C5FD and an outer border with color #C1DEFE.
2. THE Focus_Ring pattern SHALL be defined in the Shared_Stylesheet so that all User_Controls apply it consistently.

### Requirement 7: Document Repository Control

**User Story:** As a developer, I want a Document Repository user control that displays a summary dashboard and document list, so that I can demonstrate a typical list-view pattern with category filtering and summary cards.

#### Acceptance Criteria

1. THE Document_Repository_Control SHALL display Summary_Cards showing counts for Total Documents, Compliance documents, Legal documents, and Banking documents.
2. THE Document_Repository_Control SHALL display a list of documents where each row shows the document name, Category_Badge, file type, file size, and upload date.
3. THE Document_Repository_Control SHALL populate the document list and Summary_Cards using Demo_Data.
4. WHEN a user selects a document from the list, THE Document_Repository_Control SHALL open a view of the selected document's details.
5. WHEN a user initiates an upload action, THE Document_Repository_Control SHALL open the Upload_Document_Dialog and pass any required context data via the Upload_Document_Dialog constructor.
6. WHEN the Upload_Document_Dialog returns a new document, THE Document_Repository_Control SHALL add the new document to the document list and update the Summary_Cards accordingly.

### Requirement 8: Upload Document Dialog

**User Story:** As a developer, I want an Upload Document dialog user control, so that I can demonstrate a form-based popup pattern with data entry and parent notification.

#### Acceptance Criteria

1. THE Upload_Document_Dialog SHALL display a Document Name text field using IcEnabledTextBoxStyle.
2. THE Upload_Document_Dialog SHALL display a Category dropdown using IcEnabledComboBoxStyle with options for Compliance, Legal, and Banking.
3. THE Upload_Document_Dialog SHALL display a file selector area that supports a drag-and-drop visual pattern for file selection.
4. THE Upload_Document_Dialog SHALL display a Notes text field for optional notes.
5. THE Upload_Document_Dialog SHALL display a Cancel button using VcSecondaryButtonStyle and an Upload Document button using VcPrimaryButtonStyle.
6. WHEN the user clicks the Cancel button, THE Upload_Document_Dialog SHALL close without returning data to the Parent_Control.
7. WHEN the user clicks the Upload Document button with valid data, THE Upload_Document_Dialog SHALL simulate file selection and pass the new document data back to the Parent_Control.
8. IF the user clicks the Upload Document button without providing a Document Name, THEN THE Upload_Document_Dialog SHALL display a validation message indicating the Document Name is required.

### Requirement 9: Document Viewing

**User Story:** As a developer, I want to view a document's details from the document list, so that I can demonstrate a detail-view navigation pattern.

#### Acceptance Criteria

1. WHEN a user selects a document in the Document_Repository_Control and triggers a view action, THE Framework SHALL display the selected document's details including name, category, file type, size, upload date, and notes.
2. THE Framework SHALL pass the selected document data to the detail view via constructor parameters, following the parent-child data passing pattern.

### Requirement 10: Steering Document

**User Story:** As a developer, I want a steering document that describes all design patterns and conventions, so that contributors can follow consistent practices when adding new user controls.

#### Acceptance Criteria

1. THE Framework SHALL include a Steering_Document that describes the Shared_Stylesheet usage conventions.
2. THE Steering_Document SHALL describe the Focus_Ring pattern including inner color #93C5FD and outer color #C1DEFE.
3. THE Steering_Document SHALL describe the Root_Border pattern with UiScaleState bindings.
4. THE Steering_Document SHALL describe the parent-child data passing convention using constructor injection and callback returns.
5. THE Steering_Document SHALL list all shared style references (VcPrimaryButtonStyle, VcSecondaryButtonStyle, IcEnabledComboBoxStyle, IcEnabledTextBoxStyle) with usage guidance.
6. THE Steering_Document SHALL describe the Inline_Stylesheet convention for control-specific styles.
7. THE Steering_Document SHALL describe the Demo_Data convention: all models, constants, and demo data reside in the owning User_Control's code-behind with no separate Models or DemoData folders.
