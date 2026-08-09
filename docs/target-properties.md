# Target Properties

[Documentation index](README.md)

MotionSlot can author properties on the stable target in addition to layout bounds.

## Shortcut Properties

The following properties may be written directly on `MotionSlot` and are projected onto the represented target:

- `Opacity`
- `ClipToBounds`
- `Clip`
- `OpacityMask`
- `Effect`
- `RenderTransform`
- `RenderTransformOrigin`
- `ZIndex`
- `IsEnabled`
- `IsHitTestVisible`
- `Cursor`
- `Focusable`
- `IsTabStop`
- `TabIndex`

```xml
<motion:MotionSlot
  x:Name="Card"
  Opacity="0.8"
  IsHitTestVisible="True"
  RenderTransform="scale(0.96) rotate(-2deg)"
  RenderTransformOrigin="50%,50%"/>
```

Width, height, alignment, margin, and panel attached properties remain layout inputs for the slot itself.

## Setters

Use native Avalonia `Setter` syntax for other styled properties:

```xml
<motion:MotionSlot
  x:Name="Card"
  x:SetterTargetType="Border">
  <Setter Property="Background" Value="#FF203B56"/>
  <Setter Property="BorderBrush" Value="#FF315E86"/>
  <Setter Property="CornerRadius" Value="16"/>
</motion:MotionSlot>
```

`x:SetterTargetType` provides compiled XAML property resolution and IDE completion. A property cannot be defined both as
a shortcut and a setter on the same slot.

Direct Avalonia properties are not supported because animation-priority bindings apply only to styled and attached
properties.

## Bindings

Setter values may remain live:

```xml
<Setter
  Property="Background"
  Value="{Binding AccentBrush}"/>
```

The binding is evaluated against the real target context, then its current value becomes the authored endpoint. One-way,
compiled, template, and multi-binding expressions are supported when Avalonia exposes an observable expression.
`OneWayToSource` is not meaningful and is rejected.

## TargetStyles

Use `TargetStyles` when one slot must address descendants of the represented control:

```xml
<motion:MotionSlot x:Name="Card">
  <motion:MotionSlot.TargetStyles>
    <Style Selector="Border#Card TextBlock.title">
      <Setter Property="Opacity" Value="1"/>
      <Setter Property="RenderTransform" Value="translate(0px, 0px)"/>
    </Style>
    <Style Selector="Border#Card Border.details">
      <Setter Property="Opacity" Value="0.8"/>
    </Style>
  </motion:MotionSlot.TargetStyles>
</motion:MotionSlot>
```

Selectors run against an isolated shadow tree that mirrors the target logical subtree. Native `Style` entries, nested
styles, resources, and native setters are supported. Avalonia animations, transitions, property-value selectors, and
`/template/` traversal are rejected because they require state not represented by that tree.

Use `TargetStyles`, not `MotionSlot.Styles`. The latter is an obsolete diagnostic alias.

## Presence

```xml
<motion:MotionSlot
  x:Name="Preview"
  motion:Motion.Presence="0.5"/>
```

Presence is applied to the target container, not the target property store. It clamps to zero through one for opacity
and disables container hit testing near zero.

In complete states, a missing slot supplies zero presence automatically. In sparse keyframes, omission supplies no key.

## Relative ZIndex

MotionPanel derives target container order from slot paint order and slot `ZIndex`. A root target's animated `ZIndex`
is redirected to its container so it can move relative to sibling targets.

## Animation Priority and Base Values

Every track owns one Avalonia binding at `BindingPriority.Animation`. Lower-priority local, binding, and style values
remain intact underneath it. If those base values change while motion is applied, Motion tracks the new base. Disposing
or replacing the motion reveals the latest base value.

## Property-level Easing and Interpolation

Attach metadata directly to a setter:

```xml
<Setter
  Property="CornerRadius"
  Value="24"
  motion:Motion.Easing="CubicEaseOut"/>
```

Or use slot-local interpolation metadata for shortcut and layout channels:

```xml
<motion:MotionSlot x:Name="Card">
  <motion:MotionSlot.Interpolations>
    <motion:MotionInterpolation
      Property="(Visual.Opacity)"
      Easing="SineEaseOut"/>
  </motion:MotionSlot.Interpolations>
</motion:MotionSlot>
```

See [Interpolators](interpolators.md) for the complete fallback chain.
