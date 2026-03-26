# Requirements Document

## Introduction

The Dashboard Shell is the main application page that replaces the current bare `DashboardWindow` layout. It provides a sidebar navigation panel for switching between user controls (starting with `DocumentRepositoryControl`, with more to follow), a manual UI scale adjustment control that writes to `UiScaleState.FontScale`, and a content area that swaps the active control based on the selected navigation item. The shell follows all existing project conventions: shared `PeoplePosTheme.xaml` styles, scaling converters bound to `UiScaleState.FontScale`, constructor injection, `Action` callbacks, and the root border / focus ring patterns.

## Glossary

- **Dashboard_Shell**: The top-level layout hosted inside `DashboardWindow`, composed of a sidebar navigation panel and a content area.
- **Sidebar**: A vertical panel on the left side of the Dashboard_Shell containing navigation items and the scale adjustment UI.
- **Navigation_Item**: A clickable element in the Sidebar representing a registered user control. Each Navigation_Item has a display label and a factory that produces the corresponding control instance.
- **Content_Area**: The region of the Dashboard_Shell that displays the currently active user control, swapped when the user selects a different Navigation_Item.
- **Content_Root_Border**: The standardized root `Border` that wraps every main user control (not popups/dialogs) when displayed inside the Content_Area. It provides consistent framing with a light border, rounded corners, scaled padding, and device-pixel snapping.
- **Scale_Adjuster**: A UI element in the Sidebar that allows the user to manually set the `UiScaleState.FontScale` value, overriding the automatic DPI/screen-width computation from `UiScaleService`.
- **UiScaleState**: The existing sealed `INotifyPropertyChanged` class whose `FontScale` property drives all scaling converters and `ScaleTransform` bindings throughout the application.
- **UiScaleService**: The existing static service that computes `FontScale` from DPI and screen width and writes it to `UiScaleState.FontScale`.
- **Navigation_Registry**: The in-code list of available Navigation_Items maintained by the Dashboard_Shell, used to populate the Sidebar and resolve which control to display.

## Requirements

### Requirement 1: Shell Layout Structure

**User Story:** As a developer, I want the DashboardWindow to host a two-panel shell layout, so that users can navigate between controls using a sidebar while viewing the active control in a content area.

#### Acceptance Criteria

1. THE Dashboard_Shell SHALL display a Sidebar on the left and a Content_Area on the right, filling the full window area.
2. THE Sidebar SHALL have a fixed base width that scales with `UiScaleState.FontScale` using the `LayoutScaleConverter`.
3. THE Content_Area SHALL occupy all remaining horizontal space to the right of the Sidebar.
4. THE Dashboard_Shell SHALL apply the root border pattern with `ScaleTransform` bound to `UiScaleState.FontScale` on the Sidebar content.

### Requirement 2: Navigation Item Display

**User Story:** As a user, I want to see a list of available controls in the sidebar, so that I know which sections I can navigate to.

#### Acceptance Criteria

1. THE Sidebar SHALL display one Navigation_Item for each entry in the Navigation_Registry.
2. WHEN a Navigation_Item is selected, THE Sidebar SHALL visually distinguish the selected Navigation_Item from unselected items using a highlighted background and foreground color.
3. THE Sidebar SHALL render each Navigation_Item label with a font size scaled via `FontScaleConverter`.
4. THE Sidebar SHALL include "Documents" as the default Navigation_Item mapped to `DocumentRepositoryControl`.

### Requirement 3: Control Switching via Navigation

**User Story:** As a user, I want to click a navigation item in the sidebar to switch the displayed control, so that I can move between different sections of the application.

#### Acceptance Criteria

1. WHEN the user clicks a Navigation_Item, THE Dashboard_Shell SHALL replace the Content_Area content with the user control produced by that Navigation_Item factory.
2. WHEN the Dashboard_Shell loads, THE Dashboard_Shell SHALL display the first Navigation_Item control in the Content_Area by default.
3. THE Dashboard_Shell SHALL pass required constructor parameters (including `Action` callbacks) to each control when creating the control instance via the Navigation_Item factory.

### Requirement 4: Navigation Registry Extensibility

**User Story:** As a developer, I want to add new controls to the navigation by registering them in a single location, so that extending the sidebar requires minimal code changes.

#### Acceptance Criteria

1. THE Navigation_Registry SHALL store each Navigation_Item as a record containing a display label and a `Func` factory that returns a `UserControl` instance.
2. THE Sidebar SHALL populate its navigation list from the Navigation_Registry without hardcoding individual control references in XAML.
3. WHEN a new control is added to the Navigation_Registry, THE Sidebar SHALL display the new Navigation_Item without changes to the Sidebar XAML.

### Requirement 5: Manual Scale Adjustment UI

**User Story:** As a user, I want to manually adjust the UI scale from the sidebar, so that I can override the automatic DPI-based scaling to my preferred size.

#### Acceptance Criteria

1. THE Sidebar SHALL display a Scale_Adjuster below the navigation items.
2. THE Scale_Adjuster SHALL include a slider that allows the user to set `UiScaleState.FontScale` to a value between 0.6 and 1.8.
3. WHEN the user moves the slider, THE Scale_Adjuster SHALL write the new value to `UiScaleState.FontScale` so that all scaling converters and `ScaleTransform` bindings update immediately.
4. THE Scale_Adjuster SHALL display the current scale value as a percentage label (e.g., "100%") next to the slider, updated in real time as the slider moves.
5. THE Scale_Adjuster SHALL include preset buttons for common scale values (75%, 100%, 125%, 150%) that set the slider and `UiScaleState.FontScale` to the corresponding value when clicked.

### Requirement 6: Scale Adjuster Interaction with UiScaleService

**User Story:** As a developer, I want the manual scale override to coexist with the automatic DPI-based scaling, so that the user's manual choice is respected until the next automatic recalculation.

#### Acceptance Criteria

1. WHEN the user adjusts the Scale_Adjuster, THE Scale_Adjuster SHALL write directly to `UiScaleState.FontScale`, bypassing `UiScaleService.InitializeFromWindow`.
2. WHEN `UiScaleService.DpiChanged` fires (monitor switch or OS scale change), THE Dashboard_Shell SHALL update the Scale_Adjuster slider position to reflect the new automatically computed scale value.
3. THE Scale_Adjuster slider position SHALL initialize to the current `UiScaleState.FontScale` value when the Dashboard_Shell loads.

### Requirement 7: Keyboard Accessibility for Sidebar Navigation

**User Story:** As a keyboard user, I want to navigate the sidebar items and scale controls using the keyboard, so that the dashboard is accessible without a mouse.

#### Acceptance Criteria

1. THE Sidebar Navigation_Items SHALL be focusable via Tab key navigation.
2. WHEN a Navigation_Item receives keyboard focus, THE Navigation_Item SHALL display the focus ring pattern (inner `#93C5FD` 2px, outer `#C1DEFE` 3px).
3. WHEN a focused Navigation_Item receives an Enter or Space key press, THE Dashboard_Shell SHALL activate that Navigation_Item and switch the Content_Area.
4. THE Scale_Adjuster slider SHALL be focusable and operable via arrow keys.
5. THE Scale_Adjuster preset buttons SHALL display the focus ring pattern when focused via keyboard.

### Requirement 8: Scaled Sidebar Dimensions

**User Story:** As a user, I want the sidebar layout to scale consistently with the rest of the application, so that the navigation remains proportional at any scale factor.

#### Acceptance Criteria

1. THE Sidebar padding SHALL scale using `ThicknessScaleConverter` bound to `UiScaleState.FontScale`.
2. THE Navigation_Item height and vertical spacing SHALL scale using `LayoutScaleConverter` bound to `UiScaleState.FontScale`.
3. THE Scale_Adjuster slider width SHALL scale using `LayoutScaleConverter` bound to `UiScaleState.FontScale`.

### Requirement 9: Content Root Border for Main Controls

**User Story:** As a developer, I want every main user control (not popups/dialogs) to be wrapped in a standardized root border, so that all navigated controls have consistent framing, rounded corners, scaled padding, and crisp rendering.

#### Acceptance Criteria

1. EVERY main user control displayed in the Content_Area SHALL use the Content_Root_Border as its outermost element (direct child of the `<UserControl>` tag).
2. THE Content_Root_Border SHALL have `BorderBrush="#E5E7EB"`, `BorderThickness="1"`, `Background="#F8FAFC"`, `SnapsToDevicePixels="True"`, and `ClipToBounds="True"`.
3. THE Content_Root_Border padding SHALL scale using `ThicknessScaleConverter` bound to `UiScaleState.FontScale` with `ConverterParameter='28,24,28,24'`.
4. THE Content_Root_Border corner radius SHALL use `CornerRadiusFromHeightConverter` bound to the border's own `ActualHeight` with `ConverterParameter="12"`.
5. THE Content_Root_Border margin SHALL scale using `ThicknessScaleConverter` bound to `UiScaleState.FontScale` with `ConverterParameter="0"`.
6. THE Content_Root_Border SHALL set `FocusVisualStyle="{x:Null}"` to suppress the default focus rectangle.
7. Popup and dialog controls (e.g., `UploadDocumentDialog`) SHALL NOT use the Content_Root_Border — they continue to use the existing popup/dialog root border pattern defined in §8 of the steering document.

### Requirement 10: Dialog Hosting from Navigated Controls

**User Story:** As a developer, I want controls loaded via navigation to be able to request dialog windows, so that existing dialog patterns (like UploadDocumentDialog) continue to work from within the shell.

#### Acceptance Criteria

1. THE Dashboard_Shell SHALL provide dialog-hosting callbacks to controls that require them (e.g., `DocumentRepositoryControl` receives `onRequestUploadDialog`).
2. WHEN a navigated control invokes a dialog callback, THE Dashboard_Shell SHALL create a modal `Window` with the dialog `UserControl` as content, set `Owner` to the `DashboardWindow`, and call `ShowDialog`.
3. THE Dashboard_Shell SHALL use the existing dialog hosting pattern: programmatically created `Window` with `SizeToContent.WidthAndHeight` and `WindowStartupLocation.CenterOwner`.
