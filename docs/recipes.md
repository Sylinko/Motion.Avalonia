# Recipes

These recipes combine the same small set of primitives in different ways. They are patterns rather than additional framework types.

## Two-Mode Editor

Use one stable editor control, an optional preview target, and two state templates:

- `Quick`: center the editor with `MaxWidth="600"` and omit the preview slot;
- `Advanced`: place the editor on the left with `MaxWidth="420"` and add the preview slot on the right.

Drive the change with `BasicMotionController.State`.

This is the complete example in [Getting Started](getting-started.md). The important detail is that neither state hard-codes the editor's left coordinate. Each template uses ordinary responsive layout, and Motion.Avalonia interpolates the resulting bounds.

## Enter and Leave

To make a target enter or leave, include its slot in one state and omit it from the other:

```xml
<motion:MotionState Key="Closed">
    <DataTemplate>
        <Grid>
            <motion:MotionSlot Target="Content" />
        </Grid>
    </DataTemplate>
</motion:MotionState>

<motion:MotionState Key="Open">
    <DataTemplate>
        <Grid ColumnDefinitions="*,320">
            <motion:MotionSlot Target="Content" />
            <motion:MotionSlot Target="Inspector"
                               Grid.Column="1" />
        </Grid>
    </DataTemplate>
</motion:MotionState>
```

The `Inspector` keeps one real control instance. Its container presence moves from zero to one while its endpoint bounds and target properties come from the `Open` state.

Use explicit target `Opacity` in addition to presence only when the desired effect requires it.

## Four-State Slider

For ordered states such as `Overview`, `Details`, `Compare`, and `Complete`, expose a controller `Progress` from `0` through `3`.

```csharp
var sourceIndex = Math.Min((int)Progress, StateKeys.Length - 1);
var targetIndex = Math.Min(sourceIndex + 1, StateKeys.Length - 1);
var localProgress = Progress - sourceIndex;

SetCurrentPose(
    sourceIndex == targetIndex
        ? MotionPose.State(StateKeys[sourceIndex])
        : MotionPose.Blend(
            MotionPose.State(StateKeys[sourceIndex]),
            MotionPose.State(StateKeys[targetIndex]),
            localProgress));
```

Bind a `Slider.Value` to `Progress`. Previous, next, and random buttons can set or animate the same value without changing the state templates.

See the demo's [FourStateMotionController](../src/Motion.Avalonia.Demo/Controls/FourStateMotionController.cs) and [ControllerPosePage](../src/Motion.Avalonia.Demo/Views/ControllerPosePage.axaml).

## Scroll-Driven Storytelling

Convert scroll offset to a normalized progress value:

```csharp
var scrollableHeight =
    Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
var progress =
    scrollableHeight <= 0
        ? 0
        : Math.Clamp(scrollViewer.Offset.Y / scrollableHeight, 0, 1);

controller.Progress = progress;
```

The controller can divide that range into unequal segments. For example:

- `0.00`: immersive video;
- `0.38`: transition layout with comments revealed;
- `1.00`: discussion layout with a compact “Continue playing” surface.

The scroll bar may be hidden without changing this mapping. A slider can write to the same scroll offset for precise scrubbing.

See [ShowcaseSequenceController](../src/Motion.Avalonia.Demo/Controls/ShowcaseSequenceController.cs) and [OverviewPage](../src/Motion.Avalonia.Demo/Views/OverviewPage.axaml).

## Animate Background and Shape

Use setters when the state changes styled properties:

```xml
<motion:MotionSlot Target="Card"
                   Opacity="0.8">
    <Setter Property="Background"
            Value="#FF203B56" />
    <Setter Property="BorderBrush"
            Value="#FF315E86" />
    <Setter Property="CornerRadius"
            Value="16" />
    <Setter Property="BorderThickness"
            Value="2" />
</motion:MotionSlot>
```

Avalonia's animator registrations determine the default interpolation. Brushes interpolate continuously when compatible. A property without a compatible animator switches discretely at the halfway point.

If one property needs a different curve, add a panel-level `MotionInterpolator`.

See [TargetPropertiesPage](../src/Motion.Avalonia.Demo/Views/TargetPropertiesPage.axaml).

## Change Paint Order

Express paint order in each state template using normal visual-tree order and `ZIndex`:

```xml
<!-- State A -->
<Grid>
    <motion:MotionSlot Target="Image" />
    <motion:MotionSlot Target="Caption"
                       ZIndex="1" />
</Grid>

<!-- State B -->
<Grid>
    <motion:MotionSlot Target="Caption" />
    <motion:MotionSlot Target="Image"
                       ZIndex="1" />
</Grid>
```

The panel flattens nested state layouts into relative target order and applies it to the stable item containers.

For a strict one-moment swap instead of default integer interpolation, register a custom interpolator for the target's `ZIndex`.

## Responsive Motion

Use layout constraints rather than coordinates:

```xml
<Grid ColumnDefinitions="*,Auto,*">
    <motion:MotionSlot Target="Card"
                       Grid.Column="1"
                       Width="320"
                       MaxWidth="600" />
</Grid>
```

When the panel changes size, required state templates are measured and arranged again. Their current bounds become the new endpoints, including while a progress-driven pose remains between states.

This works well for:

- window resizing;
- split-view layouts;
- localization and text reflow;
- dynamic sidebars;
- bound state-template sizes.

## Smooth Retargeting

`BasicMotionController` starts an interrupted transition from its current `MotionPose`, not from the last requested state.

For example, if `Compact → Expanded` reaches 40% and the state changes back to `Compact`, the new pose can be understood as:

```text
Blend(
  Blend(Compact, Expanded, 0.40),
  Compact,
  newProgress)
```

No manual snapshot is needed. The nested pose preserves the current visual layout and target-property values as the new source.

## Disable Animation for a State Change

To snap every transition handled by a basic controller:

```xml
<motion:BasicMotionController
    State="{Binding CurrentState}"
    Easing="{x:Null}" />
```

To snap one directed edge while keeping the defaults for other changes:

```xml
<motion:MotionEdge From="Loading"
                   To="Error"
                   Duration="0" />
```

State templates remain live after the snap. Future resize or binding changes update the presented state without a secondary layout transition.

Next: [Architecture](architecture.md)
