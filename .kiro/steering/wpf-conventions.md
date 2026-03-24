---
inclusion: auto
---

# WPF UserControl Framework Conventions

Follow the conventions documented in #[[file:WpfUserControlFramework/Docs/SteeringDocument.md]].

Key rules for UI scaling:
- `UiScaleService.InitializeFromWindow(window)` must be called in the main window constructor
- Subscribe to `UiScaleService.DpiChanged` to re-initialize on monitor/DPI changes
- All scaling converters bind to `UiScaleState.FontScale` via `{StaticResource UiScaleState}`
- `ScaleTransform` on root borders also binds to `FontScale` (not `ScaleFactor` — that property no longer exists)
- `UiScaleRead` resolves scale through a fallback chain: bound value → UiScaleState resource → UiScaleService.FontScale → legacy UiFontScale → 1.0

Key rules for converters:
- All converters are in `WpfUserControlFramework/Converters/` as individual `.cs` files
- They are registered in `PeoplePosTheme.xaml` and available via `{StaticResource ConverterName}`
- Never write inline scaling math — always use the appropriate converter
- Never create a new converter if one already exists in the `Converters/` folder
- `CornerRadiusFromHeightConverter` binds to `ActualHeight`, not `FontScale`
- `WidthMultiplierConverter` and `WidthToHeightRatioConverter` bind to width values, not `FontScale`

Key rules for WinForms interop:
- `UseWindowsForms=true` is enabled in the csproj for `Screen.FromHandle` in `UiScaleService`
- `GlobalUsings.cs` resolves type ambiguities (Application, UserControl, Brush, Binding, ColorConverter, etc.)
- Always prefer WPF types; if you need a WinForms type, use the fully qualified name (e.g. `System.Windows.Forms.Screen`)
