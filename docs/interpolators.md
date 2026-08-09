# Interpolators

[Documentation index](README.md)

Motion separates two operations:

- `Easing` remaps progress on the time axis;
- `MotionInterpolator` maps progress through a property's value space.

## Default Interpolation

`MotionAvaloniaInterpolator` is the fallback. It caches one value interpolator per `AvaloniaProperty` and reuses
Avalonia's registered `Animator<T>` where possible.

Brushes, effects, and `RenderTransform` receive specialized handling because Avalonia's corresponding base animators
normally choose a concrete implementation during Avalonia animation `Apply()`. Unsupported value types switch
discretely at halfway.

## MotionInterpolation

A `MotionInterpolation` selects one property channel:

```xml
<motion:SequenceMotion>
  <motion:Motion.Interpolations>
    <motion:MotionInterpolation
      Target="Card"
      Property="(Visual.Opacity)"
      Easing="SineEaseOut"/>
  </motion:Motion.Interpolations>
</motion:SequenceMotion>
```

Motion-level entries require `Target` for target channels. Omit `Target` for the panel-wide
`Motion.DesiredSize` channel. Slot-local entries belong to `MotionSlot.Interpolations`, where the
slot supplies the target identity. Motion-level entries are compile-time authoring data: changing
the collection or an entry does not alter an existing compiled motion.

## Layout Channels

Layout uses the same resolver as target properties:

| Property | Type | Destination |
|---|---|---|
| `Motion.DesiredSizeProperty` | `Size` | MotionPanel |
| `Motion.PositionProperty` | `Point` | Target container |
| `Motion.SizeProperty` | `Size` | Target container |
| `Motion.PresenceProperty` | `double` | Target container |

Example:

```xml
<motion:MotionInterpolation
  Target="Card"
  Property="(motion:Motion.Position)">
  <motion:MotionInterpolation.Interpolator>
    <motion:CubicBezierMotionInterpolator
      SourceControlOffset="120,-80"
      TargetControlOffset="-96,64"/>
  </motion:MotionInterpolation.Interpolator>
</motion:MotionInterpolation>
```

The Bézier control points are offsets from the live source and destination positions. Resizing therefore moves the
entire curve with its endpoints.

## Resolution Order

For a channel arriving at an endpoint, metadata resolves from most specific to broadest:

1. setter or slot endpoint;
2. matching `MotionEdge.Interpolations` for `StateMotion`;
3. matching `Motion.Interpolations`;
4. default linear easing and `MotionAvaloniaInterpolator`.

Easing and interpolator fall back independently. An explicit null easing means linear. An explicit null interpolator
resets to the default Avalonia interpolator.

For Sequence and Timeline tracks, destination keyframe easing supplies the inherited segment easing before the
property-specific chain is applied.

## Strongly Typed Custom Interpolator

```csharp
public sealed class OvershootDoubleInterpolator : MotionInterpolator<double>
{
    protected override double Interpolate(
        double progress,
        double oldValue,
        double newValue)
    {
        var overshoot = Math.Sin(progress * Math.PI) * 0.08;
        return oldValue + ((newValue - oldValue) * (progress + overshoot));
    }
}
```

Use it through `MotionInterpolation`:

```xml
<motion:MotionInterpolation
  Target="Card"
  Property="(Visual.Opacity)">
  <motion:MotionInterpolation.Interpolator>
    <local:OvershootDoubleInterpolator/>
  </motion:MotionInterpolation.Interpolator>
</motion:MotionInterpolation>
```

The generic type must match both endpoint values. Type mismatches fail with a configuration exception that identifies
the property and endpoint.

## Easing Is Not a Spatial Path

Easing changes only the scalar progress parameter. A position path requires a value-space interpolator such as
`CubicBezierMotionInterpolator`. Other property types can define entirely different value-space mappings without
changing MotionPanel.
