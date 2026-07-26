# Motion.Avalonia

A declarative motion and interaction framework for Avalonia.

Motion.Avalonia targets .NET 8, .NET 9, and .NET 10, and is built on Avalonia 12.

## TransitionPanel

`TransitionPanel` keeps its real content controls as stable direct children. Each
`TransitionPanelState` owns one persistent layout template containing `TransitionPanelSlot`
proxies. The proxies are measured and arranged using ordinary Avalonia panels, then their live layout bounds are applied
to the corresponding real controls. Targets are matched to direct children through `Control.Name`, normally set with
`x:Name`.

The implementation performs real `Measure` and `Arrange` calls at intermediate sizes. It does not render controls to
bitmaps and does not use scale transforms to imitate layout.

```xml

<motion:TransitionPanel
  State="{Binding EditorMode}"
  StateTransitionDuration="0:0:0.35"
  StateTransitionEasing="CubicEaseInOut">
  <motion:TransitionPanel.States>
    <motion:TransitionPanelState Key="Quick">
      <DataTemplate>
        <Grid>
          <motion:TransitionPanelSlot
            Target="Editor"
            MaxWidth="600"
            HorizontalAlignment="Stretch"/>
        </Grid>
      </DataTemplate>
    </motion:TransitionPanelState>

    <motion:TransitionPanelState Key="Advanced">
      <DataTemplate>
        <DockPanel HorizontalSpacing="24">
          <motion:TransitionPanelSlot
            Target="Editor"
            DockPanel.Dock="Left"
            MaxWidth="420"/>
          <motion:TransitionPanelSlot
            Target="Preview"/>
        </DockPanel>
      </DataTemplate>
    </motion:TransitionPanelState>
  </motion:TransitionPanel.States>

  <local:EditorControl x:Name="Editor"/>
  <local:PreviewControl x:Name="Preview"/>
</motion:TransitionPanel>
```

State templates are instantiated once and remain connected to the logical and visual trees. Bindings, resources, child
desired sizes, and panel size changes therefore continue to update their layout. Only states needed for the current
frame are measured and arranged.

Slots can also declare values for styled properties on their targets by using Avalonia setters. Use
`x:SetterTargetType` to give the XAML compiler and IDE the target type:

```xml
<motion:TransitionPanelSlot
  x:SetterTargetType="Border"
  Target="Preview">
  <Setter
    Property="Background"
    Value="#FF243D59"/>
</motion:TransitionPanelSlot>
```

Setter bindings remain live while their state template exists. Property values are interpolated by Avalonia's built-in
animation system and applied at animation priority, so removing a target from the panel restores its underlying local,
styled, or bound value. Direct properties are not supported.

`StateTransitionEasing` controls changes to `State`.
`LayoutTransitionEasing` independently controls layout changes while the state key remains unchanged, such as a parent
resize. A null easing disables the corresponding animation and applies the live layout immediately.

Active transitions are advanced by `TopLevel.RequestAnimationFrame`. The panel caches its current top level while
attached, invalidates measure once per frame, and pauses its elapsed animation time while detached from the visual tree.

The first implementation intentionally supports stable direct children of one
`TransitionPanel`. Cross-ancestor transitions, page transitions, bitmap snapshots, and transform-based pseudo-layout
are outside its scope.

## Projects

- `Motion.Avalonia` — reusable controls and motion primitives.
- `Motion.Avalonia.Demo` — interactive desktop showcase.
- `Motion.Avalonia.Tests` — automated tests.
