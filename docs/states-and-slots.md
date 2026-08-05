# States and Slots

[Documentation index](README.md)

This chapter focuses on complete named layouts owned by `StateMotion`. The same slot syntax is also used inside sparse
keyframes.

## A State Is a Live Template

```xml
<motion:MotionState Key="Centered">
  <DataTemplate>
    <Grid>
      <motion:MotionSlot
        x:Name="Card"
        MaxWidth="480"
        HorizontalAlignment="Center"/>
    </Grid>
  </DataTemplate>
</motion:MotionState>
```

Use ordinary Avalonia panels and attached layout properties. The resolved slot bounds are the endpoint; the panel tree
itself is not copied into the visible UI.

## Match by x:Name

```xml
<motion:MotionPanel>
  <motion:MotionPanel.Motion>
    <motion:StateMotion State="Centered">
      <motion:MotionState Key="Centered">
        <DataTemplate>
          <motion:MotionSlot x:Name="Card"/>
        </DataTemplate>
      </motion:MotionState>
    </motion:StateMotion>
  </motion:MotionPanel.Motion>

  <Border x:Name="Card"/>
</motion:MotionPanel>
```

Names must be non-empty and unique within both the direct targets and each layout source.

## Layout Properties Stay on the Slot

Properties used by the parent panel arrange the slot:

- `Margin`
- `HorizontalAlignment` and `VerticalAlignment`
- `Width`, `Height`, `MinWidth`, `MaxWidth`, `MinHeight`, `MaxHeight`
- panel-specific attached properties such as `Grid.Row`, `DockPanel.Dock`, or `Canvas.Left`

These values are not copied to the target. Their calculated result becomes `Motion.Position` and `Motion.Size`.

## Responsive Endpoints

Avoid hard-coded animated offsets when a panel can express the relationship:

```xml
<DataTemplate>
  <DockPanel HorizontalSpacing="16">
    <motion:MotionSlot
      x:Name="Editor"
      MaxWidth="420"
      DockPanel.Dock="Left"/>
    <motion:MotionSlot x:Name="Preview"/>
  </DockPanel>
</DataTemplate>
```

When the host width changes, the live `DockPanel` produces new endpoints automatically.

## Missing Targets

In `StateMotion`, every state is complete. A slot omitted from the destination fades through `Presence` and becomes
non-hit-testable. The target control remains alive.

If a control should retain layout but become visually transparent, keep its slot and set target `Opacity="0"`.

## Relative Z-order

Slots are collected in template paint order, including nested panels and their `ZIndex` values. The resulting relative
order is applied to target containers. `ZIndex` also participates as a target property channel and currently uses
integer interpolation.

## Desired Size

MotionPanel's desired size is interpolated from the required live layout sources. A settled state continues to respond
directly to new constraints. Motion does not retain a frozen state snapshot.

## Template Lifetime

Changing a state's `Template` rebuilds only that layout source. Changing state keys, edges, easing, Cue, or KeyTime
recompiles motion tracks without rebuilding unrelated template trees.

Do not reuse one `MotionState` or keyframe instance in multiple collections. Reuse the `DataTemplate` resource instead:

```xml
<UserControl.Resources>
  <DataTemplate x:Key="SharedCardLayout">
    <motion:MotionSlot x:Name="Card"/>
  </DataTemplate>
</UserControl.Resources>

<motion:MotionState
  Key="Compact"
  Template="{StaticResource SharedCardLayout}"/>
```

---

[Next: Motions and Keyframes →](motions-and-keyframes.md)
