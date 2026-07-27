# API Reference

This is a compact reference for the current public surface. Motion.Avalonia is an early preview; consult the XML documentation and source when version-specific precision is required.

All types below are in the `Motion.Avalonia` namespace.

## MotionPanel

```csharp
public sealed class MotionPanel : Control
```

Projects live state templates onto stable direct child controls.

| Member | Type | Description |
| --- | --- | --- |
| `Children` | `Controls` | Stable logical targets; also the XAML content collection |
| `States` | `AvaloniaList<MotionState>` | Persistent state definitions |
| `Controller` | `IMotionController?` | Current pose producer |
| `Interpolators` | `AvaloniaList<MotionInterpolator>` | Target-property interpolation overrides |
| `ControllerProperty` | `StyledProperty<IMotionController?>` | Avalonia property backing `Controller` |

Every child must have a unique non-empty `x:Name`.

## MotionState

```csharp
public sealed class MotionState : AvaloniaObject
```

Defines one live layout endpoint.

| Member | Type | Description |
| --- | --- | --- |
| `Key` | `object?` | Key referenced by controller poses |
| `Template` | `IDataTemplate?` | Persistent layout template; also the XAML content property |
| `KeyProperty` | `StyledProperty<object?>` | Avalonia property backing `Key` |
| `TemplateProperty` | `StyledProperty<IDataTemplate?>` | Avalonia property backing `Template` |

State keys use ordinary object equality. More than one state matching the same requested key is an error.

## MotionSlot

```csharp
public sealed class MotionSlot : Control
```

Represents one real target inside a state layout.

| Member | Type | Description |
| --- | --- | --- |
| `Target` | `string?` | Exact name of the represented direct child |
| `Setters` | `AvaloniaList<Setter>` | Additional target-property endpoints; also the XAML content collection |
| `TargetProperty` | `StyledProperty<string?>` | Avalonia property backing `Target` |

The supported direct shortcuts are:

```text
Opacity
ClipToBounds
Clip
OpacityMask
Effect
RenderTransform
RenderTransformOrigin
ZIndex
IsEnabled
IsHitTestVisible
Cursor
Focusable
IsTabStop
TabIndex
```

Layout properties on the slot are resolved by the state template. Shortcut and setter values become properties on the real target, except `ZIndex`, which is applied to its stable container.

## MotionPose

```csharp
public abstract class MotionPose
```

Describes the current live composition requested by a controller.

| Member | Signature | Description |
| --- | --- | --- |
| `Empty` | `static MotionPose` | Presents no state |
| `State` | `static MotionPose State(object? key)` | Presents one state |
| `CreateBlend` | `static MotionPose CreateBlend(MotionPose source, MotionPose target, double progress, Easing? easing = null, MotionEdge? edge = null)` | Blends two poses and optionally associates a directed edge |

`CreateBlend` requires non-null source and target poses, finite progress, and clamps progress to `[0, 1]`. A null easing
argument selects `LinearEasing`. The optional edge is available to edge-specific interpolator lookup.

The concrete state, blend, and empty pose representations are internal.

## IMotionController

```csharp
public interface IMotionController
```

The minimal pose-producer contract.

| Member | Type | Description |
| --- | --- | --- |
| `CurrentPose` | `MotionPose` | Currently presented pose |
| `CurrentPoseChanged` | `event EventHandler?` | Raised after the current pose changes |

## MotionController

```csharp
public abstract class MotionController : StyledElement, IMotionController
```

Base class for controllers that need Avalonia integration and frame scheduling.

### Public members

| Member | Type | Description |
| --- | --- | --- |
| `CurrentPose` | `MotionPose` | Current read-only pose |
| `CurrentPoseChanged` | `event EventHandler?` | Pose-change notification |
| `CurrentPoseProperty` | `DirectProperty<MotionController, MotionPose>` | Avalonia property for `CurrentPose` |

### Protected members

| Member | Description |
| --- | --- |
| `IsAttached` | Whether this controller currently owns a panel attachment |
| `SetCurrentPose(MotionPose)` | Publishes a non-null pose |
| `RequestAnimationFrame(Action<TimeSpan>)` | Schedules through the owning panel's `TopLevel`; returns whether scheduling succeeded |
| `OnAttached()` | Called after assignment to a panel |
| `OnDetached()` | Called before removal from a panel |
| `OnOwnerAttachedToVisualTree()` | Called when the panel enters a visual tree |
| `OnOwnerDetachedFromVisualTree()` | Called when the panel leaves a visual tree |
| `OnOwnerPresented()` | Called after the panel first presents its current pose |

A controller instance can be attached to only one panel at a time.

## BasicMotionController

```csharp
public sealed class BasicMotionController : MotionController
```

Animates discrete state requests with default timing and directed edge overrides.

| Member | Type | Default | Description |
| --- | --- | --- | --- |
| `State` | `object?` | `null` | Requested state key |
| `Duration` | `TimeSpan` | 300 ms | Default duration; must be non-negative |
| `Easing` | `Easing?` | `LinearEasing` | Default easing; null disables animation |
| `Edges` | `AvaloniaList<MotionEdge>` | Empty | Directed overrides; also the XAML content collection |

The corresponding styled-property fields are `StateProperty`, `DurationProperty`, and `EasingProperty`.

The initial request snaps. Later animated changes start from `CurrentPose`, which preserves interruption continuity.

## SequenceMotionController

```csharp
public sealed class SequenceMotionController : MotionController
```

Maps normalized external progress across ordered state stops. It provides no clock or playback commands.

| Member | Type | Default | Description |
| --- | --- | --- | --- |
| `Progress` | `double` | `0` | Current sequence position, coerced to `[0, 1]`; must be finite |
| `Stops` | `AvaloniaList<MotionStop>` | Empty | State stops; also the XAML content collection |

The corresponding styled-property field is `ProgressProperty`.

Stops are stable-sorted by `Offset`. Before or after the defined range, the nearest endpoint is held. An exact offset
publishes a state pose; an in-between value blends adjacent states. For duplicate offsets, the last stop declared at
that offset is the exact and right-side result.

## MotionStop

```csharp
public sealed class MotionStop : AvaloniaObject
```

Defines one endpoint in a `SequenceMotionController`.

| Member | Type | Default | Description |
| --- | --- | --- | --- |
| `State` | `object?` | `null` | State key presented at this stop |
| `Offset` | `double` | `0` | Normalized location in `[0, 1]`; must be finite |
| `Easing` | `Easing?` | `null` | Easing for the interval arriving at this stop; null means linear |

The corresponding styled-property fields are `StateProperty`, `OffsetProperty`, and `EasingProperty`.

## MotionEdge

```csharp
public sealed class MotionEdge : AvaloniaObject
```

Identifies one directed state-key pair and can override its timing in `BasicMotionController`.

| Member | Type | Description |
| --- | --- | --- |
| `From` | `object?` | Source state key |
| `To` | `object?` | Destination state key |
| `Duration` | `TimeSpan?` | Override; null inherits controller duration |
| `Easing` | `Easing?` | Override; null inherits controller easing |

The corresponding styled-property fields are `FromProperty`, `ToProperty`, `DurationProperty`, and `EasingProperty`.

`Duration` must be null or non-negative. More than one matching edge in a controller is an error.

## MotionInterpolator

```csharp
public abstract class MotionInterpolator : AvaloniaObject
```

Selects an interpolation policy for one target property.

| Member | Type | Description |
| --- | --- | --- |
| `Target` | `string?` | Stable target name |
| `Property` | `AvaloniaProperty?` | Target property |
| `Easing` | `Easing?` | Override; null inherits pose easing |
| `Edges` | `AvaloniaList<MotionEdge>` | Active directed edges; empty matches every edge |

The corresponding styled-property fields are `TargetProperty`, `PropertyProperty`, and `EasingProperty`.

Panel collection entries must set both `Target` and `Property`.

## MotionInterpolator\<T\>

```csharp
public abstract class MotionInterpolator<T> : MotionInterpolator
```

Strongly typed base for custom property interpolation.

```csharp
public abstract T Interpolate(
    double progress,
    T oldValue,
    T newValue);
```

The base validates source and destination endpoint types before calling the override.

## MotionAvaloniaInterpolator

```csharp
public sealed class MotionAvaloniaInterpolator : MotionInterpolator
```

Uses Avalonia's registered `Animator<T>` implementations plus specialized brush, effect, and transform dispatch.

Create an instance in `MotionPanel.Interpolators` when default value interpolation is desired but target/property selection, edge filtering, or property-specific easing must be explicit.

When no panel override matches, `MotionPanel` uses an internal shared instance automatically.

## Internal Implementation Types

The following concepts are intentionally internal and are not extension points:

- persistent state hosts;
- target item containers;
- property-output channels;
- concrete pose nodes;
- animator adapters.

Use `MotionController`, `MotionPose`, and `MotionInterpolator<T>` for public extensibility.

---

[Documentation index](README.md) · [Project README](../README.md)
