# Interpolators

An easing answers, “How far through the transition are we?” An interpolator answers, “What property value exists at that progress?”

Keeping those responsibilities separate allows one motion pose to use a general timing curve while selected target properties use a different curve or interpolation algorithm.

## Default Behavior

If no override matches a target property, `MotionPanel` uses `MotionAvaloniaInterpolator`.

It reuses Avalonia's registered `Animator<T>` for the property whenever possible. This provides the same strongly typed interpolation behavior as Avalonia animations without requiring Motion.Avalonia to maintain a parallel table for every supported value type.

It also handles several polymorphic properties explicitly:

| Values or property | Default behavior |
| --- | --- |
| Solid-color brushes | `ISolidColorBrushAnimator` |
| Gradient brushes | `GradientBrushAnimator` |
| Compatible solid/gradient pair | Solid brush normalized to a gradient |
| Blur effects | `BlurEffectAnimator` |
| Drop-shadow effects | `DropShadowEffectAnimator` |
| `RenderTransform` | `TransformOperationsAnimator` |
| Other registered properties | Avalonia's registered `Animator<T>` |
| No compatible animator | Discrete switch at progress `0.5` |

Animator resolution is cached by property and endpoint runtime types.

## Panel-Level Overrides

`MotionPanel.Interpolators` is a lookup collection. Each entry must identify:

- `Target`: the stable `x:Name` of a direct child;
- `Property`: the Avalonia property to interpolate;
- optionally, `Edges`: the directed transitions on which it applies;
- optionally, `Easing`: a property-specific progress curve.

```xml
<motion:MotionPanel.Interpolators>
    <motion:MotionAvaloniaInterpolator
        Target="Card"
        Property="Opacity"
        Easing="SineEaseInOut" />
</motion:MotionPanel.Interpolators>
```

The explicit `MotionAvaloniaInterpolator` still uses Avalonia's default value interpolation. Here it only overrides the easing for `Card.Opacity`.

## Lookup Order

For each `(Target, Property)` pair, the panel selects:

1. an override whose `Edges` collection contains the active `MotionEdge`;
2. a general panel override with an empty `Edges` collection;
3. the shared default `MotionAvaloniaInterpolator`.

An edge-specific match takes precedence over a general match. Multiple matches at the same level are configuration errors.

Edge filtering is available only when the pose carries a `MotionEdge`, as poses produced by `BasicMotionController` do. A custom controller that uses the public `MotionPose.Blend` API creates an unassociated blend and therefore uses general interpolators.

## Property-Specific Easing

When an interpolator has an `Easing`, it evaluates that easing against the blend's raw progress:

```xml
<motion:MotionAvaloniaInterpolator
    Target="Card"
    Property="Opacity"
    Easing="CubicEaseIn" />
```

When `Easing` is `null`, the property inherits the pose's already-eased progress.

This makes two useful levels available:

- controller or edge easing shapes the motion as a whole;
- interpolator easing reshapes one target property.

## A Strongly Typed Custom Interpolator

Derive from `MotionInterpolator<T>` when a property needs a different value path:

```csharp
using Motion.Avalonia;

public sealed class OvershootDoubleInterpolator : MotionInterpolator<double>
{
    public double Overshoot { get; set; } = 0.08;

    public override double Interpolate(
        double progress,
        double oldValue,
        double newValue)
    {
        var distance = newValue - oldValue;
        var wave = Math.Sin(progress * Math.PI) * Overshoot;
        return oldValue + (distance * (progress + wave));
    }
}
```

Register it for one target and property:

```xml
<motion:MotionPanel.Interpolators>
    <local:OvershootDoubleInterpolator
        Target="Card"
        Property="Opacity" />
</motion:MotionPanel.Interpolators>
```

The generic base checks both endpoint types and reports a descriptive error if the selected property does not produce `T`.

The example is intentionally small. A production interpolator should define how it handles overshoot, clamping, null endpoints, and values outside its natural domain.

## Restricting an Override to Edges

`MotionInterpolator.Edges` contains actual `MotionEdge` instances, not state-key pairs. This keeps lookup tied to the edge selected by the controller.

The relationship can be configured in code when that is clearer than XAML references:

```csharp
using Avalonia;
using Avalonia.Animation.Easings;
using Motion.Avalonia;

var expandEdge = new MotionEdge
{
    From = "Compact",
    To = "Expanded",
    Duration = TimeSpan.FromMilliseconds(500),
};

var opacityInterpolator = new MotionAvaloniaInterpolator
{
    Target = "Card",
    Property = Visual.OpacityProperty,
    Easing = new CubicEaseIn(),
};

opacityInterpolator.Edges.Add(expandEdge);
controller.Edges.Add(expandEdge);
panel.Interpolators.Add(opacityInterpolator);
```

The reverse edge can use the general interpolator or a second edge-specific override.

## Why Interpolators Belong to the Panel

The panel owns the mapping from a slot endpoint to a real target property. Therefore it is also the natural first lookup scope for interpolation policy.

This avoids ambiguity in state templates:

- an interpolator is not arbitrarily attached to the source or destination setter;
- forward and reverse transitions can be distinguished by edge;
- one state definition does not need to know every controller that may present it;
- the default case remains concise.

More advanced scopes may be added in the future, but the current fallback chain is intentionally small and deterministic.

## Discrete Values

Not every property has a meaningful continuous path. For cursors, unsupported object types, and incompatible polymorphic values, the default interpolator returns:

- the source value before progress `0.5`;
- the destination value at and after progress `0.5`.

This is deterministic and keeps unsupported properties usable. Define a custom interpolator when the halfway switch is not the desired policy.

Next: [Recipes](recipes.md)
