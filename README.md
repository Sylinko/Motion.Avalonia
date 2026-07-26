# Motion.Avalonia

A declarative motion and interaction framework for Avalonia.

Motion.Avalonia targets .NET 8, .NET 9, and .NET 10, and is built on Avalonia 12.

## LayoutMorphPanel

`LayoutMorphPanel` keeps its real content controls as stable direct children. Each
`LayoutMorphState` owns one persistent layout template containing `LayoutMorphSlot`
proxies. The proxies are measured and arranged using ordinary Avalonia panels, then their live layout bounds are applied
to the corresponding real controls. Targets are matched to direct children through `Control.Name`, normally set with
`x:Name`.

The implementation performs real `Measure` and `Arrange` calls at intermediate sizes. It does not render controls to
bitmaps and does not use scale transforms to imitate layout.

```xml

<motion:LayoutMorphPanel
  State="{Binding EditorMode}"
  StateTransitionDuration="0:0:0.35"
  StateTransitionEasing="CubicEaseInOut">
  <motion:LayoutMorphPanel.States>
    <motion:LayoutMorphState Key="Quick">
      <DataTemplate>
        <Grid>
          <motion:LayoutMorphSlot
            Target="Editor"
            MaxWidth="600"
            HorizontalAlignment="Stretch"/>
        </Grid>
      </DataTemplate>
    </motion:LayoutMorphState>

    <motion:LayoutMorphState Key="Advanced">
      <DataTemplate>
        <DockPanel HorizontalSpacing="24">
          <motion:LayoutMorphSlot
            Target="Editor"
            DockPanel.Dock="Left"
            MaxWidth="420"/>
          <motion:LayoutMorphSlot
            Target="Preview"/>
        </DockPanel>
      </DataTemplate>
    </motion:LayoutMorphState>
  </motion:LayoutMorphPanel.States>

  <local:EditorControl x:Name="Editor"/>
  <local:PreviewControl x:Name="Preview"/>
</motion:LayoutMorphPanel>
```

State templates are instantiated once and remain connected to the logical and visual trees. Bindings, resources, child
desired sizes, and panel size changes therefore continue to update their layout. Only states needed for the current
frame are measured and arranged.

`StateTransitionEasing` controls changes to `State`.
`LayoutTransitionEasing` independently controls layout changes while the state key remains unchanged, such as a parent
resize. A null easing disables the corresponding animation and applies the live layout immediately.

Active transitions are advanced by `TopLevel.RequestAnimationFrame`. The panel caches its current top level while
attached, invalidates measure once per frame, and pauses its elapsed animation time while detached from the visual tree.

The first implementation intentionally supports stable direct children of one
`LayoutMorphPanel`. Cross-ancestor transitions, page transitions, bitmap snapshots, and transform-based pseudo-layout
are outside its scope.

## Projects

- `Motion.Avalonia` — reusable controls and motion primitives.
- `Motion.Avalonia.Demo` — interactive desktop showcase.
- `Motion.Avalonia.Tests` — automated tests.
