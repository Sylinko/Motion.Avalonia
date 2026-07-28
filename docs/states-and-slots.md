# States and Slots

[← Core Concepts](core-concepts.md) · [Documentation](README.md) ·
[Next: Controllers and Poses →](controllers-and-poses.md)

`MotionState` and `MotionSlot` describe responsive endpoints. They deliberately reuse Avalonia's existing layout system
instead of introducing a second layout language.

## Defining a MotionState

A state has a `Key` and a persistent `IDataTemplate`:

```xml
<motion:MotionState Key="Cards">
  <motion:MotionState.Template>
    <DataTemplate>
      <UniformGrid Columns="3">
        <motion:MotionSlot Margin="8" x:Name="Alpha"/>
        <motion:MotionSlot Margin="8" x:Name="Beta"/>
        <motion:MotionSlot Margin="8" x:Name="Gamma"/>
      </UniformGrid>
    </DataTemplate>
  </motion:MotionState.Template>
</motion:MotionState>
```

Keys are ordinary objects and are compared with `Equals`. A panel must not contain multiple states with equal keys.

## Matching Targets by Name

Every real child must have a unique name:

```xml
<Border x:Name="Alpha"/>
<Border x:Name="Beta"/>
<Border x:Name="Gamma"/>
```

Every slot must specify a non-empty `Target` that matches one of those names. A state cannot contain two slots for the
same target.

Names provide a stable identity boundary:

```text
MotionSlot.Name == Control.Name
```

Avalonia prevents changing a styled element's `Name`. Motion therefore resolves each slot-to-target mapping once for
the current panel structure and rebuilds it only when the panel children or state template change.

## Use Ordinary Avalonia Panels

The state template can use normal Avalonia layout:

```xml
<DataTemplate>
  <Grid
    ColumnDefinitions="2*,*"
    RowDefinitions="Auto,*"
    ColumnSpacing="16"
    RowSpacing="16">
    <motion:MotionSlot
      Grid.RowSpan="2"
      x:Name="Primary"/>
    <motion:MotionSlot
      Grid.Column="1"
      x:Name="Toolbar"/>
    <motion:MotionSlot
      Grid.Row="1"
      Grid.Column="1"
      x:Name="Details"/>
  </Grid>
</DataTemplate>
```

Useful choices include:

- `Grid` for shared rows and columns;
- `DockPanel` for one or more docked regions and a fill region;
- `StackPanel` for natural vertical or horizontal sequences;
- `UniformGrid` for equal cells;
- nested panels when one target group must move as a unit.

Use the simplest panel that expresses the endpoint. MotionPanel interpolates resolved rectangles, not the panel-specific
constraints themselves.

## Layout Properties Belong to the Slot

These properties configure state layout and are not copied to the target:

- `Width`, `Height`, `MinWidth`, `MaxWidth`, `MinHeight`, and `MaxHeight`
- `Margin`
- `HorizontalAlignment` and `VerticalAlignment`
- `UseLayoutRounding`
- `FlowDirection`
- Grid, DockPanel, Canvas, and other attached layout properties

For example:

```xml
<motion:MotionSlot
  Grid.Column="1"
  Margin="8"
  MaxWidth="420"
  HorizontalAlignment="Stretch"
  x:Name="Editor"/>
```

The endpoint uses the slot's resolved bounds, not the raw `MaxWidth` or alignment values. `double.NaN`/Auto sizing and
constraints therefore behave like normal Avalonia layout.

## Live Bindings inside State Templates

State templates remain connected to the panel's data context:

```xml
<motion:MotionSlot
  x:Name="Inspector"
  MaxWidth="{Binding InspectorMaximumWidth}"
  Margin="{Binding InspectorMargin}"/>
```

When a binding changes, the state layout is invalidated. A settled state updates immediately; an active blend evaluates
the changed endpoint with its current progress.

The same rule applies when the real target's desired size changes. The slot measures the real control instead of a
duplicate placeholder.

## Missing Targets and Presence

A target does not need a slot in every state:

```xml
<!-- Compact contains only Content. -->
<DataTemplate>
  <Grid>
    <motion:MotionSlot x:Name="Content"/>
  </Grid>
</DataTemplate>
```

```xml
<!-- Detail adds Inspector. -->
<DataTemplate>
  <Grid ColumnDefinitions="*,320" ColumnSpacing="16">
    <motion:MotionSlot x:Name="Content"/>
    <motion:MotionSlot Grid.Column="1" x:Name="Inspector"/>
  </Grid>
</DataTemplate>
```

The panel keeps `Inspector` alive. During Compact → Detail, its presence moves from zero to one at the Detail bounds.
During Detail → Compact, it fades at its last available bounds and its property outputs are released after it becomes
absent.

## Responsive State Layouts

A state template is arranged against the current `MotionPanel` size on every required layout pass. A centered slot
therefore remains centered when the window changes width:

```xml
<Grid>
  <motion:MotionSlot
    x:Name="Card"
    Width="320"
    HorizontalAlignment="Center"/>
</Grid>
```

If the panel width changes from 800 to 1000 during a 50% blend, MotionPanel recalculates both endpoints at width 1000
before evaluating the current frame.

## Relative Z-order

Slots can use `ZIndex` directly or with a setter. MotionPanel walks each state template in its effective nested paint
order:

1. siblings are ordered by their state Z-index;
2. equal values preserve visual-child order;
3. nested subtrees are traversed recursively;
4. encountered targets receive normalized relative values `0, 1, 2, ...`.

Those normalized values are applied to the targets' internal containers. This is necessary because raw Z-index values
from different ancestors cannot be compared directly.

```xml
<Grid>
  <motion:MotionSlot
    x:Name="Backdrop"
    ZIndex="0"/>
  <motion:MotionSlot
    x:Name="Dialog"
    ZIndex="10"/>
</Grid>
```

Z-order currently uses the default integer interpolation between state-relative values.

## `IsVisible` Is Not a Shortcut

Setting `IsVisible="False"` directly on a slot would remove the slot from its own state layout and make its endpoint
unavailable. To describe the target's `IsVisible`, use an explicit setter:

```xml
<motion:MotionSlot x:Name="OptionalAction">
  <Setter Property="IsVisible" Value="False"/>
</motion:MotionSlot>
```

The setter is evaluated on a separate endpoint object, so it does not hide the layout proxy.

## Configuration Errors

MotionPanel reports configuration errors rather than choosing ambiguous behavior:

- a direct child has no name;
- direct children have duplicate names;
- a slot has no target;
- a slot references an unknown target;
- one state contains duplicate slots for a target;
- states have duplicate keys.

See [Limitations and Troubleshooting](limitations-and-troubleshooting.md) for a diagnostic checklist.

---

[← Core Concepts](core-concepts.md) · [Documentation](README.md) ·
[Next: Controllers and Poses →](controllers-and-poses.md)
