# Target Properties

Layout morphing already animates each target's position, width, height, and presence. A state can also provide endpoint values for Avalonia properties on the real target control.

There are two equivalent authoring forms:

- a direct `MotionSlot` shortcut for common properties;
- a `Setter` for any supported styled property.

Both forms enter the same property-evaluation and interpolation pipeline.

## Shortcuts

The following properties are recognized as direct `MotionSlot` shortcuts:

| Shortcut | Applied to | Typical behavior |
| --- | --- | --- |
| `Opacity` | Target control | Continuous interpolation |
| `ClipToBounds` | Target control | Avalonia's registered behavior, commonly discrete |
| `Clip` | Target control | Registered animator or discrete fallback |
| `OpacityMask` | Target control | Brush interpolation when compatible |
| `Effect` | Target control | Blur and drop-shadow interpolation when compatible |
| `RenderTransform` | Target control | Transform-operations interpolation |
| `RenderTransformOrigin` | Target control | Continuous interpolation |
| `ZIndex` | Internal target container | Relative state paint order |
| `IsEnabled` | Target control | Avalonia's registered behavior, commonly discrete |
| `IsHitTestVisible` | Target control | Avalonia's registered behavior, commonly discrete |
| `Cursor` | Target control | Discrete fallback unless overridden |
| `Focusable` | Target control | Avalonia's registered behavior, commonly discrete |
| `IsTabStop` | Target control | Avalonia's registered behavior, commonly discrete |
| `TabIndex` | Target control | Registered integer interpolation |

Only explicitly set shortcut values become motion endpoints. An inherited or default value on the slot is not copied accidentally.

```xml
<motion:MotionSlot Target="Card"
                   Opacity="0.72"
                   ClipToBounds="True"
                   IsHitTestVisible="False"
                   ZIndex="10" />
```

## Setters

Use setters for properties outside the shortcut list:

```xml
<motion:MotionSlot Target="Card"
                   x:SetterTargetType="Border"
                   Opacity="0.72">
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

Specify the target type on `MotionSlot` so that the XAML compiler and IDE can resolve unqualified properties:

```xml
<motion:MotionSlot Target="Card"
                   x:SetterTargetType="Border">
    <Setter Property="CornerRadius"
            Value="16" />
</motion:MotionSlot>
```

The exact target type depends on the real control and the property owner.

## Shortcuts and Setters Are Equivalent

These two endpoint declarations have the same motion semantics:

```xml
<motion:MotionSlot Target="Card"
                   Opacity="0.5" />
```

```xml
<motion:MotionSlot Target="Card">
    <Setter Property="Opacity"
            Value="0.5" />
</motion:MotionSlot>
```

Do not use both forms for the same property on one slot. Motion.Avalonia reports the duplicate as a configuration error.

Shortcuts exist for readability and tooling, not as a separate animation system.

## Live Binding Values

A setter value may be a binding:

```xml
<motion:MotionSlot Target="Card">
    <Setter Property="Background"
            Value="{Binding ActiveCardBrush}" />
</motion:MotionSlot>
```

The slot keeps the binding alive. When its value changes, the state endpoint is recaptured and the currently presented property value is reevaluated.

This has the same live-state intent as layout templates: state definitions are not one-time snapshots.

## Base Values

Motion values are published at Avalonia's `BindingPriority.Animation`. The target's ordinary local value, style, binding, or inherited value remains its base value.

When a property is no longer supplied by the active pose, Motion.Avalonia removes its animation-priority output. Avalonia then exposes the current base value again.

If only one side of a blend defines a property, the other endpoint is the target's current base value:

```xml
<!-- Compact defines no Background endpoint. -->
<motion:MotionState Key="Compact">
    <DataTemplate>
        <motion:MotionSlot Target="Card" />
    </DataTemplate>
</motion:MotionState>

<!-- Expanded blends from the target's base Background. -->
<motion:MotionState Key="Expanded">
    <DataTemplate>
        <motion:MotionSlot Target="Card">
            <Setter Property="Background"
                    Value="#FF3F294F" />
        </motion:MotionSlot>
    </DataTemplate>
</motion:MotionState>
```

Changes to the underlying base value are tracked while the motion output is active.

## Presence and Opacity

Presence and target `Opacity` are distinct:

- presence belongs to whether a target has a slot in a state;
- opacity is an explicit visual property of a target that exists in that state.

If a target exists on only one side of a blend, Motion.Avalonia keeps its endpoint bounds and properties while its container presence fades between zero and one.

If the target exists in both states and only its `Opacity` changes, its layout remains present throughout the transition.

This distinction lets a control enter or leave without overloading its own opacity value.

## ZIndex

`ZIndex` is evaluated as relative paint order inside each state template.

Motion.Avalonia walks the template's visual tree in paint order, including nested panels and their `ZIndex` values, then assigns a normalized relative index to each real target container. The real controls remain direct logical children of `MotionPanel`.

This means a state can express ordering naturally:

```xml
<Grid>
    <motion:MotionSlot Target="Backdrop"
                       ZIndex="0" />
    <motion:MotionSlot Target="Dialog"
                       ZIndex="10" />
</Grid>
```

During a blend, the normalized integer values use the selected interpolator. The default integer animator may pass through intermediate indices. A future or custom interpolator can choose a different switching policy.

## Properties That Are Not Shortcuts

Layout-facing properties such as these normally belong to the state template, not to the real target:

- `HorizontalAlignment`;
- `VerticalAlignment`;
- `Margin`;
- grid row and column;
- dock position;
- panel-specific attached layout properties.

They determine where the `MotionSlot` is arranged. Motion.Avalonia then transfers the resulting bounds to the target container.

Copying those properties onto the target would mix two layout systems and produce surprising results.

`IsVisible` is deliberately not a shortcut. It may still be expressed as a setter when an immediate/discrete visibility change is truly intended, but entry and exit motion should normally use slot presence.

## Current Constraints

Target-property motion currently supports styled properties. Direct Avalonia properties are rejected because they do not participate in the same priority-based value pipeline.

Motion.Avalonia also rejects:

- a setter without `Property`;
- multiple setters for the same property on one slot;
- a shortcut and setter for the same property on one slot;
- a value invalid for the selected Avalonia property;
- a property not registered on the real target.

These failures are explicit so that a state cannot silently animate a different property than the one its author intended.

Next: [Interpolators](interpolators.md)
