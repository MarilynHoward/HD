# Steering Document — WPF UserControl Framework

This document describes the design patterns, conventions, and shared resources used throughout the WPF UserControl Framework. Follow these conventions when adding new user controls.

---

## 1. Shared Stylesheet (`PeoplePosTheme.xaml`)

The shared `ResourceDictionary` lives at the project root (`WpfUserControlFramework/PeoplePosTheme.xaml`) — not in a `Themes/` subfolder. It is merged into application-level resources via `App.xaml`:

```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="PeoplePosTheme.xaml" />
</ResourceDictionary.MergedDictionaries>
```

Because it is merged at the `Application.Resources` level, every control in the project can reference its keys with `{StaticResource KeyName}` without any additional merging.

### What belongs in the shared stylesheet

- Colors and brushes used across multiple controls (e.g. `MainForeground`, `DimmedForeground`, category badge brushes)
- All converters from the `Converters/` folder (see §10 below for the full list)
- The `UiScaleState` binding source
- Reusable control styles (see §5 below)
- The focus ring template (see §2 below)

### What does NOT belong in the shared stylesheet

- Styles that are only used by a single control — those go in the control's inline stylesheet (see §6)

---

## 2. Focus Ring Pattern

Interactive elements display a two-border focus ring when they receive keyboard focus. The ring uses two colors:

| Border | Color   | Thickness | Role        |
|--------|---------|-----------|-------------|
| Outer  | `#C1DEFE` (`FocusOuter`) | 3px | Soft outer glow |
| Inner  | `#93C5FD` (`FocusInner`) | 2px | Crisp inner ring |

### When to apply

Apply the focus ring to every interactive element that can receive keyboard focus: buttons, text boxes, combo boxes, and any custom focusable control.

### Implementation

The focus ring is built into each control's `ControlTemplate` as two collapsed `Border` elements that become visible on `IsKeyboardFocused`:

```xml
<Grid>
    <!-- Main content border -->
    <Border x:Name="BackgroundBorder" ... />

    <!-- Focus ring (collapsed by default) -->
    <Border x:Name="FocusOuterRing"
            BorderBrush="{StaticResource FocusOuter}"
            BorderThickness="3"
            CornerRadius="8"
            Margin="-2"
            Visibility="Collapsed" />
    <Border x:Name="FocusInnerRing"
            BorderBrush="{StaticResource FocusInner}"
            BorderThickness="2"
            CornerRadius="7"
            Visibility="Collapsed" />
</Grid>

<!-- In ControlTemplate.Triggers -->
<Trigger Property="IsKeyboardFocused" Value="True">
    <Setter TargetName="FocusOuterRing" Property="Visibility" Value="Visible" />
    <Setter TargetName="FocusInnerRing" Property="Visibility" Value="Visible" />
</Trigger>
```

A standalone `FocusRingTemplate` is also available in the shared stylesheet for use with custom controls:

```xml
<ControlTemplate x:Key="FocusRingTemplate" TargetType="Control">
    <Grid>
        <Border BorderBrush="{StaticResource FocusOuter}" BorderThickness="3" CornerRadius="6" Margin="-3" />
        <Border BorderBrush="{StaticResource FocusInner}" BorderThickness="2" CornerRadius="5" Margin="-1" />
    </Grid>
</ControlTemplate>
```

---

## 3. Root Border Pattern

Every `UserControl` wraps its content in a root `Border` that provides consistent framing and supports UI scaling via `UiScaleState` bindings. There are two variants depending on whether the control is a main page/panel or a popup/dialog.

### Main controls (pages, panels — displayed in the Content_Area)

Main user controls use the Content Root Border. This is the standard for any control navigated to via the sidebar.

```xml
<Border x:Name="RootBorder"
        BorderBrush="#E5E7EB"
        FocusVisualStyle="{x:Null}"
        BorderThickness="1"
        Background="#F8FAFC"
        SnapsToDevicePixels="True"
        ClipToBounds="True"
        Padding="{Binding Source={StaticResource UiScaleState},
                         Path=FontScale,
                         Converter={StaticResource ThicknessScaleConverter},
                         ConverterParameter='28,24,28,24'}">
    <Border.CornerRadius>
        <Binding RelativeSource="{RelativeSource Self}"
                 Path="ActualHeight"
                 Converter="{StaticResource CornerRadiusFromHeightConverter}"
                 ConverterParameter="12"/>
    </Border.CornerRadius>
    <Border.Margin>
        <Binding Source="{StaticResource UiScaleState}"
                 Path="FontScale"
                 Converter="{StaticResource ThicknessScaleConverter}"
                 ConverterParameter="0"/>
    </Border.Margin>

    <!-- Control content goes here -->
</Border>
```

Key properties:
- `Background="#F8FAFC"` — light off-white background
- `BorderBrush="#E5E7EB"` with `BorderThickness="1"` — subtle border
- Padding scales via `ThicknessScaleConverter` with base `28,24,28,24`
- Corner radius adapts to height via `CornerRadiusFromHeightConverter` with base `12`
- `SnapsToDevicePixels="True"` and `ClipToBounds="True"` for crisp rendering
- `FocusVisualStyle="{x:Null}"` suppresses the default focus rectangle

### Popup/dialog controls

Popup and dialog controls use a fully scalable root border with `ThicknessScaleConverter` for padding and `CornerRadiusFromHeightConverter` for rounded corners:

```xml
<Border Background="White"
        BorderBrush="#E5E7EB"
        BorderThickness="1"
        Padding="{Binding Source={StaticResource UiScaleState},
                         Path=FontScale,
                         Converter={StaticResource ThicknessScaleConverter},
                         ConverterParameter='24,16,24,20'}">
    <Border.CornerRadius>
        <Binding RelativeSource="{RelativeSource Self}"
                 Path="ActualHeight"
                 Converter="{StaticResource CornerRadiusFromHeightConverter}"
                 ConverterParameter="14"/>
    </Border.CornerRadius>

    <!-- Content goes here -->
</Border>
```

### Rules

- The root `Border` is always the direct child of the `<UserControl>` element.
- Standard controls use `ScaleTransform` binding to `UiScaleState.FontScale`.
- Popup/dialog controls use `ThicknessScaleConverter` for padding and `CornerRadiusFromHeightConverter` for corner radius.
- Do not nest a second root border — one per control is enough.

---

## 4. Parent-Child Data Passing

Data flows between controls using a simple, explicit pattern:

### Data in: Constructor injection

The parent passes all required data to the child via the child's constructor parameters. Every dependency is declared up front — no service locators, no DI containers, no property setters after construction.

```csharp
// Parent creates child with explicit dependencies
var detail = new DocumentDetailView(
    document: selectedDocument,
    onBack: () => ContentArea.Content = _repositoryControl
);
```

### Data out: `Action<T>` callbacks

The child accepts `Action<T>` delegates in its constructor and invokes them to push results back to the parent.

```csharp
// Child constructor signature
public UploadDocumentDialog(
    IEnumerable<string> categories,
    Action<DocumentModel> onDocumentUploaded,
    Action onCancel)
```

### Null guards

All constructor parameters are validated with null checks. Required parameters throw `ArgumentNullException`:

```csharp
_onNavigateToDetail = onNavigateToDetail ?? throw new ArgumentNullException(nameof(onNavigateToDetail));
```

### Dialog hosting

Dialog-style controls are `UserControl` instances hosted inside a programmatically created `Window`:

```csharp
var dialog = new UploadDocumentDialog(categories, onUploaded, onCancel);
var window = new Window
{
    Content = dialog,
    SizeToContent = SizeToContent.WidthAndHeight,
    WindowStartupLocation = WindowStartupLocation.CenterOwner,
    Owner = this,
    WindowStyle = WindowStyle.None,
    AllowsTransparency = true,
    Background = Brushes.Transparent,
    ResizeMode = ResizeMode.NoResize
};
window.ShowDialog();
```

This keeps the dialog reusable as a `UserControl` while getting modal behavior from the `Window`.

> **Note:** Any `CommonDialog.ShowDialog()` call inside a popup (e.g. `OpenFileDialog`, `SaveFileDialog`) must pass `Window.GetWindow(this)` as the owner to avoid z-order issues with modal windows. Without an explicit owner, the OS may place the common dialog behind the popup, making it unreachable.
>
> ```csharp
> var dlg = new OpenFileDialog();
> dlg.ShowDialog(Window.GetWindow(this));
> ```

---

## 5. Shared Style References

All shared styles are defined in `PeoplePosTheme.xaml` and referenced via `{StaticResource}`.

| Style Key | Target Type | Usage |
|-----------|-------------|-------|
| `VcPrimaryButtonStyle` | `Button` | Primary action buttons (e.g. "Upload Document"). Blue background, white text, hover/pressed states, focus ring. |
| `VcSecondaryButtonStyle` | `Button` | Secondary/cancel buttons (e.g. "Cancel", "← Back"). White background, gray border, hover state, focus ring. |
| `IcEnabledTextBoxStyle` | `TextBox` | Editable text fields. Inherits from `IcBaseTextBoxStyle`, adds `IsReadOnly="False"`. Includes focus ring. |
| `IcEnabledComboBoxStyle` | `ComboBox` | Enabled dropdown selectors. Inherits from `IcBaseComboBoxStyle`, adds `IsEnabled="True"`. |
| `IcBaseTextBoxStyle` | `TextBox` | Base text box style with border, padding, corner radius, and focus ring template. Not used directly — use `IcEnabledTextBoxStyle` instead. |
| `IcBaseComboBoxStyle` | `ComboBox` | Base combo box style with border, padding, and font settings. Not used directly — use `IcEnabledComboBoxStyle` instead. |

### Shared brushes

| Brush Key | Color | Usage |
|-----------|-------|-------|
| `MainForeground` | `#111827` | Primary text color |
| `DimmedForeground` | `#687280` | Secondary/muted text, labels, column headers |
| `FocusInner` | `#93C5FD` | Inner focus ring border |
| `FocusOuter` | `#C1DEFE` | Outer focus ring border |
| `ComplianceBadgeBrush` / `ComplianceBadgeTextBrush` | `#DBEAFE` / `#1E40AF` | Compliance category badge |
| `LegalBadgeBrush` / `LegalBadgeTextBrush` | `#FEF3C7` / `#92400E` | Legal category badge |
| `BankingBadgeBrush` / `BankingBadgeTextBrush` | `#D1FAE5` / `#065F46` | Banking category badge |

### Example usage in XAML

```xml
<Button Content="Upload Document" Style="{StaticResource VcPrimaryButtonStyle}" />
<TextBox Style="{StaticResource IcEnabledTextBoxStyle}" Height="{StaticResource BaseTextBoxHeight}" />
<TextBlock Foreground="{StaticResource DimmedForeground}" />
```

---

## 6. Inline Stylesheet Convention

When a control needs styles that are specific to itself and not shared across the project, define them in the control's `<UserControl.Resources>` section.

```xml
<UserControl.Resources>
    <SolidColorBrush x:Key="FieldLabelBrush" Color="#374151" />
    <SolidColorBrush x:Key="DropZoneBorderBrush" Color="#D1D5DB" />
    <!-- ... control-specific resources only -->
</UserControl.Resources>
```

### Rules

- Only put styles here that are used exclusively by this control.
- Never duplicate keys from `PeoplePosTheme.xaml` — reference the shared version instead.
- If a style starts being used by a second control, promote it to the shared stylesheet.

---

## 7. Demo Data Convention

All models, constants, records, and demo data reside in the owning control's code-behind (`.xaml.cs`). There are no separate `Models/`, `DemoData/`, or `ViewModels/` folders.

### What goes in the code-behind

- Model classes (e.g. `DocumentModel`)
- Static constant classes (e.g. `DocumentCategories`)
- Records (e.g. `SummaryCardData`, `ValidationResult`)
- Static demo data methods (e.g. `GetDemoDocuments()`)
- All control logic

### Example structure

```csharp
// DocumentRepositoryControl.xaml.cs

namespace RestaurantPosWpf
{
    // Models and constants
    public class DocumentModel { ... }
    public static class DocumentCategories { ... }
    public record SummaryCardData(string Title, int Count, string ColorKey);

    // Control
    public partial class DocumentRepositoryControl : UserControl
    {
        // Constructor with injection + callbacks
        // Event handlers
        // Demo data method
        public static List<DocumentModel> GetDemoDocuments() => new() { ... };
    }
}
```

### Why

When you open a control's `.xaml.cs`, you see the full picture: data shape, sample data, and behavior. No hunting across folders. Each control is self-contained.

---

## 8. Popup / Dialog Layout Pattern

All popup and dialog controls follow a consistent five-row Grid layout with scalable dimensions.

### Structure

```
Row 0: Header (title + subtitle + close button)
Row 1: Divider line
Row 2: Form fields
Row 3: Divider line
Row 4: Footer (Cancel + Primary action buttons)
```

### Header

The header uses a two-column Grid: title/subtitle on the left, close button (✕) on the right.

```xml
<Grid Grid.Row="0"
      Margin="{Binding Source={StaticResource UiScaleState},
                       Path=FontScale,
                       Converter={StaticResource ThicknessScaleConverter},
                       ConverterParameter='0,0,0,6'}">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>

    <StackPanel Grid.Column="0">
        <TextBlock Text="Dialog Title"
                   Foreground="#0F172A"
                   FontWeight="SemiBold"
                   FontSize="{Binding Source={StaticResource UiScaleState},
                                      Path=FontScale,
                                      Converter={StaticResource FontScaleConverter},
                                      ConverterParameter=16}"/>
        <TextBlock Foreground="#6B7280"
                   Text="Subtitle description"
                   FontSize="{Binding Source={StaticResource UiScaleState},
                                      Path=FontScale,
                                      Converter={StaticResource FontScaleConverter},
                                      ConverterParameter=14}"/>
    </StackPanel>

    <Button Grid.Column="1"
            Click="CloseButton_Click"
            Background="Transparent"
            BorderThickness="0"
            Foreground="#64748B"
            Padding="6"
            Content="✕"
            VerticalAlignment="Top"/>
</Grid>
```

### Dividers

Horizontal dividers separate header, form, and footer:

```xml
<Rectangle Height="1"
           Fill="#FFE5E7EB"
           Margin="{Binding Source={StaticResource UiScaleState},
                            Path=FontScale,
                            Converter={StaticResource ThicknessScaleConverter},
                            ConverterParameter='0,0,0,10'}"/>
```

### Form fields

Use a two-column Grid with a 16px gutter for side-by-side fields. Full-width fields span both columns with `Grid.ColumnSpan="3"`.

### Scalable dimensions

All font sizes, margins, padding, and layout dimensions use converter bindings:

| Dimension | Converter | Example |
|-----------|-----------|---------|
| Font size | `FontScaleConverter` | `ConverterParameter=12` |
| Margin/Padding | `ThicknessScaleConverter` | `ConverterParameter='0,0,0,14'` |
| Height/Width | `LayoutScaleConverter` | `ConverterParameter=200` |

### Placeholder / hint text pattern

Use an overlaid `TextBlock` with a `DataTrigger` to show placeholder text when the input is empty. The placeholder must also hide when the `TextBox` has keyboard focus, so the hint disappears as soon as the user clicks into the field.

**Important rules:**
- The `TextBlock` overlay must have `IsHitTestVisible="False"` so clicks pass through to the `TextBox`.
- Use a `MultiDataTrigger` to hide the placeholder when the `TextBox` has focus OR has text.
- The placeholder `Foreground` is `#9CA3AF` (light gray) and uses the same scaled `FontSize` as the `TextBox`.
- The placeholder `Margin` must account for the `TextBox` padding plus its `BorderThickness` (typically `Margin="12,0,0,0"` for single-line, `Margin="12,8,0,0"` + `VerticalAlignment="Top"` for multi-line).

#### Single-line TextBox with placeholder

```xml
<Grid>
    <TextBox x:Name="txtField"
             Style="{StaticResource IcEnabledTextBoxStyle}"
             Padding="10,8,10,8"/>
    <TextBlock Text="e.g., placeholder hint"
               IsHitTestVisible="False"
               Foreground="#9CA3AF"
               Margin="12,0,0,0"
               VerticalAlignment="Center"
               FontSize="{Binding Source={StaticResource UiScaleState},
                                  Path=FontScale,
                                  Converter={StaticResource FontScaleConverter},
                                  ConverterParameter=13}">
        <TextBlock.Style>
            <Style TargetType="TextBlock">
                <Setter Property="Visibility" Value="Visible"/>
                <Style.Triggers>
                    <DataTrigger Binding="{Binding ElementName=txtField, Path=Text}" Value="">
                        <Setter Property="Visibility" Value="Visible"/>
                    </DataTrigger>
                    <DataTrigger Binding="{Binding ElementName=txtField, Path=IsKeyboardFocused}" Value="True">
                        <Setter Property="Visibility" Value="Collapsed"/>
                    </DataTrigger>
                    <DataTrigger Binding="{Binding ElementName=txtField, Path=Text.Length}">
                        <DataTrigger.Value>
                            <system:Int32 xmlns:system="clr-namespace:System;assembly=mscorlib">0</system:Int32>
                        </DataTrigger.Value>
                    </DataTrigger>
                </Style.Triggers>
            </Style>
        </TextBlock.Style>
    </TextBlock>
</Grid>
```

**Simplified approach** (recommended): Use a default `Visibility` of `Visible` and collapse when the `TextBox` has text (non-empty) OR has keyboard focus:

```xml
<Grid>
    <TextBox x:Name="txtField"
             Style="{StaticResource IcEnabledTextBoxStyle}"
             Padding="10,8,10,8"/>
    <TextBlock Text="e.g., placeholder hint"
               IsHitTestVisible="False"
               Foreground="#9CA3AF"
               Margin="12,0,0,0"
               VerticalAlignment="Center"
               FontSize="{Binding Source={StaticResource UiScaleState},
                                  Path=FontScale,
                                  Converter={StaticResource FontScaleConverter},
                                  ConverterParameter=13}">
        <TextBlock.Style>
            <Style TargetType="TextBlock">
                <Setter Property="Visibility" Value="Collapsed"/>
                <Style.Triggers>
                    <MultiDataTrigger>
                        <MultiDataTrigger.Conditions>
                            <Condition Binding="{Binding ElementName=txtField, Path=Text}" Value=""/>
                            <Condition Binding="{Binding ElementName=txtField, Path=IsKeyboardFocused}" Value="False"/>
                        </MultiDataTrigger.Conditions>
                        <Setter Property="Visibility" Value="Visible"/>
                    </MultiDataTrigger>
                </Style.Triggers>
            </Style>
        </TextBlock.Style>
    </TextBlock>
</Grid>
```

#### Multi-line TextBox with placeholder

For `AcceptsReturn="True"` text boxes, use `VerticalAlignment="Top"` and adjust the top margin:

```xml
<Grid>
    <TextBox x:Name="txtNotes"
             Style="{StaticResource IcEnabledTextBoxStyle}"
             AcceptsReturn="True"
             TextWrapping="Wrap"
             VerticalContentAlignment="Top"
             Padding="10,8,10,8"
             Height="{Binding Source={StaticResource UiScaleState},
                              Path=FontScale,
                              Converter={StaticResource LayoutScaleConverter},
                              ConverterParameter=80}"/>
    <TextBlock Text="Add any additional notes..."
               IsHitTestVisible="False"
               Foreground="#9CA3AF"
               Margin="12,8,0,0"
               VerticalAlignment="Top"
               FontSize="{Binding Source={StaticResource UiScaleState},
                                  Path=FontScale,
                                  Converter={StaticResource FontScaleConverter},
                                  ConverterParameter=13}">
        <TextBlock.Style>
            <Style TargetType="TextBlock">
                <Setter Property="Visibility" Value="Collapsed"/>
                <Style.Triggers>
                    <MultiDataTrigger>
                        <MultiDataTrigger.Conditions>
                            <Condition Binding="{Binding ElementName=txtNotes, Path=Text}" Value=""/>
                            <Condition Binding="{Binding ElementName=txtNotes, Path=IsKeyboardFocused}" Value="False"/>
                        </MultiDataTrigger.Conditions>
                        <Setter Property="Visibility" Value="Visible"/>
                    </MultiDataTrigger>
                </Style.Triggers>
            </Style>
        </TextBlock.Style>
    </TextBlock>
</Grid>
```

### Validation error labels

Each required field has a collapsed error `TextBlock` below it:

```xml
<TextBlock x:Name="lblFieldError"
           Visibility="Collapsed"
           Foreground="#DC2626"
           Margin="{Binding Source={StaticResource UiScaleState},
                            Path=FontScale,
                            Converter={StaticResource ThicknessScaleConverter},
                            ConverterParameter='0,6,0,0'}"
           FontSize="{Binding Source={StaticResource UiScaleState},
                              Path=FontScale,
                              Converter={StaticResource FontScaleConverter},
                              ConverterParameter=11}"/>
```

### Footer buttons

Right-aligned using a Grid with spacer columns:

```xml
<Grid Grid.Row="4">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="12"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>

    <Button Grid.Column="1"
            FocusVisualStyle="{x:Null}"
            Style="{StaticResource VcSecondaryButtonStyle}"
            Content="Cancel"/>
    <Button Grid.Column="3"
            FocusVisualStyle="{x:Null}"
            Style="{StaticResource VcPrimaryButtonStyle}"
            Content="Confirm"/>
</Grid>
```

---

## 9. Control Folder Organization — Category / Sub-Category

Controls are grouped into a two-level hierarchy under `Controls/`:

```
Controls/
└── {Category}/
    └── {SubCategory}/
        └── {ControlName}/
            ├── ControlName.xaml
            └── ControlName.xaml.cs
```

### Current categories

| Category | Sub-Category | Controls |
|----------|-------------|----------|
| Suppliers | Documents | `DocumentRepositoryControl`, `DocumentDetailView`, `UploadDocumentDialog` |

### Rules

- Every control must belong to exactly one category and one sub-category.
- Category and sub-category folder names use PascalCase (e.g. `Suppliers`, `Documents`).
- C# namespaces remain flat (`RestaurantPosWpf`) — the folder hierarchy is for organization only, not namespace mapping.
- When adding a new control, place it in an existing category/sub-category if it fits. Create a new one only when no existing grouping applies.

---

## Quick Reference: Adding a New Control

1. Identify the category and sub-category for your control (see §9).
2. Create a folder under `Controls/{Category}/{SubCategory}/` named after your control.
3. Add `YourControl.xaml` and `YourControl.xaml.cs` in that folder.
4. Define models, constants, and demo data in the `.xaml.cs` file.
5. Use the Root Border pattern with `UiScaleState` bindings as the outermost element (see §3).
6. For popup/dialog controls, follow the Popup/Dialog Layout Pattern (see §8).
7. Reference shared styles from `PeoplePosTheme.xaml` via `{StaticResource}`.
8. Put control-specific styles in `<UserControl.Resources>`.
9. Accept data via constructor parameters; return data via `Action<T>` callbacks.
10. Null-guard all constructor parameters with `ArgumentNullException`.
11. Apply the focus ring pattern to any custom focusable elements.
12. Use the correct converters from the `Converters/` folder (see §10) — never inline scaling math or write ad-hoc converters when one already exists.


---

## 10. Converter Reference

All converters live in `WpfUserControlFramework/Converters/` as individual `.cs` files. They are registered in `PeoplePosTheme.xaml` and available via `{StaticResource ConverterName}`. The shared helper class `UiScaleRead` (in `UiScaleRead.cs` at the project root) provides safe scale-value parsing used by the scaling converters.

### Scaling infrastructure

`UiScaleState` is a sealed bindable class with two properties:
- `FontScale` — the primary scale factor, driven by `UiScaleService`. All scaling converters and `ScaleTransform` bindings use this.
- `FooterAlignHeight` — bindable height for footer alignment in split-panel layouts.

`UiScaleService` is the static engine that computes the correct `FontScale` based on DPI and screen width:
- Call `UiScaleService.InitializeFromWindow(window)` in your main window constructor.
- Subscribe to `UiScaleService.DpiChanged` to re-initialize when the user moves between monitors or changes OS scaling.
- The service writes the computed scale to `UiScaleState.FontScale` (via the `UiScaleState` resource in `PeoplePosTheme.xaml`) and to the legacy `UiFontScale` resource.
- Baseline profiles are configurable via `UiRenderProfilePercent` in `App.config` (100, 125, or 150; default 150).

`UiScaleRead` is the internal helper used by all scaling converters. It resolves the scale value through a fallback chain:
1. The bound value (usually `UiScaleState.FontScale`)
2. The `UiScaleState` application resource
3. `UiScaleService.FontScale`
4. Legacy `UiFontScale` resource
5. Fallback: `1.0`

### UI Scaling Converters

| Converter | Key | Input (value) | Parameter | Output | Usage |
|-----------|-----|---------------|-----------|--------|-------|
| `FontScaleConverter` | `FontScaleConverter` | `FontScale` (double) | Base font size (e.g. `14`) | Scaled double, clamped 6–96 | Font sizes |
| `LayoutScaleConverter` | `LayoutScaleConverter` | `FontScale` (double) | Base dimension (e.g. `200`) | Scaled double | Heights, widths, spacing |
| `ThicknessScaleConverter` | `ThicknessScaleConverter` | `FontScale` (double) | `'L,T,R,B'` or `'uniform'` or `'H,V'` | Scaled `Thickness`, clamped 0–500 | Margins, padding |
| `ScaledDoubleConverter` | `ScaledDoubleConverter` | `FontScale` (double) | Base value (e.g. `1.5`) | Scaled double | Generic scaled dimensions |
| `CornerRadiusFromHeightConverter` | `CornerRadiusFromHeightConverter` | `ActualHeight` (double) | Desired base radius (e.g. `8`) | `CornerRadius`, geometry-clamped | Rounded corners |
| `WidthMultiplierConverter` | `WidthMultiplierConverter` | Width (double) | Multiplier factor | `width × multiplier` | Proportional widths |
| `WidthToHeightRatioConverter` | `WidthToHeightRatioConverter` | Width (double) | Ratio (default 1.0) | `width × ratio` | Aspect-ratio heights |

### Scaling converter XAML examples

```xml
<!-- Font size -->
FontSize="{Binding Source={StaticResource UiScaleState},
                  Path=FontScale,
                  Converter={StaticResource FontScaleConverter},
                  ConverterParameter=14}"

<!-- Margin -->
Margin="{Binding Source={StaticResource UiScaleState},
                Path=FontScale,
                Converter={StaticResource ThicknessScaleConverter},
                ConverterParameter='0,0,0,14'}"

<!-- Height -->
Height="{Binding Source={StaticResource UiScaleState},
                Path=FontScale,
                Converter={StaticResource LayoutScaleConverter},
                ConverterParameter=200}"

<!-- Corner radius from actual height -->
<Border.CornerRadius>
    <Binding RelativeSource="{RelativeSource Self}"
             Path="ActualHeight"
             Converter="{StaticResource CornerRadiusFromHeightConverter}"
             ConverterParameter="14"/>
</Border.CornerRadius>
```

### Visibility Converters

| Converter | Key | Input | Parameter | Output |
|-----------|-----|-------|-----------|--------|
| `BoolToVisibilityConverter` | `BoolToVisibilityConverter` | `bool` | `"Invert"` (optional) | `Visible` / `Collapsed` |
| `CountToVisibilityConverter` | `CountToVisibilityConverter` | `int` | — | `Visible` if > 0, else `Collapsed` |
| `NullOrEmptyToVisibilityConverter` | `NullOrEmptyToVisibilityConverter` | `object` / `string` | — | `Collapsed` if null/empty/whitespace |

```xml
<!-- Show element only when count > 0 -->
Visibility="{Binding ItemCount, Converter={StaticResource CountToVisibilityConverter}}"

<!-- Show/hide based on bool, with optional invert -->
Visibility="{Binding IsEditing, Converter={StaticResource BoolToVisibilityConverter}}"
Visibility="{Binding IsEditing, Converter={StaticResource BoolToVisibilityConverter}, ConverterParameter=Invert}"

<!-- Collapse when string is null/empty -->
Visibility="{Binding Description, Converter={StaticResource NullOrEmptyToVisibilityConverter}}"
```

### Text / Data Converters

| Converter | Key | Input | Parameter | Output |
|-----------|-----|-------|-----------|--------|
| `BadgeListConverter` | `BadgeListConverter` | Pipe-delimited string (`"A|B|C"`) | — | `List<string>` |
| `ContactSplitterConverter` | `ContactSplitterConverter` | Newline-delimited string | Line index (`0` or `1`) | Single line string |
| `CountToTextConverter` | `CountToTextConverter` | `int` | Singular noun (e.g. `"product"`) | `"3 products selected"` |
| `IsNullOrWhiteConverter` | `IsNullOrWhiteConverter` | `object` | — | `true` if null/empty/whitespace |
| `LastIndexConverter` | `LastIndexConverter` | `int` (count) | — | `count - 1` (min 0) |

```xml
<!-- Badge list from pipe-delimited string -->
ItemsSource="{Binding Categories, Converter={StaticResource BadgeListConverter}}"

<!-- Split contact into name (line 0) and phone (line 1) -->
Text="{Binding Contact, Converter={StaticResource ContactSplitterConverter}, ConverterParameter=0}"
Text="{Binding Contact, Converter={StaticResource ContactSplitterConverter}, ConverterParameter=1}"

<!-- Selection count text -->
Text="{Binding SelectedCount, Converter={StaticResource CountToTextConverter}, ConverterParameter=product}"
```

### Color / Brush Converters

| Converter | Key | Input | Output |
|-----------|-----|-------|--------|
| `StringToBrushConverter` | `StringToBrushConverter` | Color string (`"#22C55E"`, `"Red"`) | `Brush` (falls back to green) |
| `TrendColorConverter` | `TrendColorConverter` | `int` trend value | Pastel green (>0), red (<0), or grey (0) |
| `StatusToBrushConverter` | `StatusToBrushConverter` | Status string (`"Active"`, `"Suspended"`, etc.) | Background `Brush` for status pill |
| `StatusToBorderConverter` | `StatusToBorderConverter` | Status string | Border `Brush` for status pill |
| `StatusToTextBrushConverter` | `StatusToTextBrushConverter` | Status string | White text `Brush` |

```xml
<!-- Dynamic color from model property -->
Background="{Binding ColorHex, Converter={StaticResource StringToBrushConverter}}"

<!-- Trend indicator background -->
Background="{Binding TrendValue, Converter={StaticResource TrendColorConverter}}"

<!-- Status pill -->
<Border Background="{Binding Status, Converter={StaticResource StatusToBrushConverter}}"
        BorderBrush="{Binding Status, Converter={StaticResource StatusToBorderConverter}}">
    <TextBlock Text="{Binding Status}"
               Foreground="{Binding Status, Converter={StaticResource StatusToTextBrushConverter}}"/>
</Border>
```

### Layout Converters

| Converter | Key | Input (MultiBinding) | Parameter | Output |
|-----------|-----|----------------------|-----------|--------|
| `VersionGridMaxHeightConverter` | `VersionGridMaxHeightConverter` | `[0]` panel height, `[1]` footer height, `[2]` fontScale | Base overhead (double) | Clamped max height for scrollable grid |

```xml
<DataGrid.MaxHeight>
    <MultiBinding Converter="{StaticResource VersionGridMaxHeightConverter}" ConverterParameter="60">
        <Binding ElementName="RightPanel" Path="ActualHeight"/>
        <Binding ElementName="Footer" Path="ActualHeight"/>
        <Binding Source="{StaticResource UiScaleState}" Path="FontScale"/>
    </MultiBinding>
</DataGrid.MaxHeight>
```

### Rules

- Never write inline scaling math — use the appropriate scaling converter.
- Never create a new converter if one already exists in the `Converters/` folder.
- All converters use the `RestaurantPosWpf` namespace (flat namespace convention).
- Scaling converters depend on `UiScaleRead` for safe value parsing — do not duplicate that logic.
- If a new converter is needed, add it as a separate `.cs` file in `Converters/`, register it in `PeoplePosTheme.xaml`, and document it in this section.
