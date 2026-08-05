# API Reference

[Documentation index](README.md)

This is a compact map of the current public authoring surface.

## MotionPanel

The visual and layout host.

| Member | Purpose |
|---|---|
| `Motion` | Current `Motion` logical child |
| `Children` | Stable direct target controls |

Every child requires a unique non-empty `x:Name`.

## Motion

Abstract base for declarative motion.

| Member | Purpose |
|---|---|
| `Easing` | Default segment easing; null resolves to linear |
| `Interpolations` | Motion-wide interpolation overrides |
| `Compile(MotionCompilationContext)` | Build the live track graph for one panel topology |

Attached members:

| Member | Owner | Purpose |
|---|---|---|
| `Motion.DesiredSize` | MotionPanel | Panel desired-size channel |
| `Motion.Position` | Target container | Evaluated position |
| `Motion.Size` | Target container | Evaluated size |
| `Motion.Presence` | MotionSlot / container | Enter/leave envelope |
| `Motion.Easing` | Setter | Incoming property easing |
| `Motion.Interpolator` | Setter | Incoming value interpolator |
| `Motion.Interpolations` | MotionSlot | Slot-local property metadata |

## Compilation

`MotionPanel` creates one `MotionRuntime` for a stable host topology. Custom motions can use the supplied
`MotionCompilationContext` to:

- synchronize `IMotionLayoutSource` instances;
- inspect reusable `MotionLayoutFrame` values;
- resolve a `MotionChannel` and create a custom typed track through `CreateTrack(MotionChannel)`;
- create a shared `KeyFrameMotionTrack<T>` and publish normalized progress into it;
- apply the same interpolation fallback policy as built-in motions.

`MotionRuntime.Tracks` exposes the resulting non-generic `MotionTrack` collection. Derived runtimes
create known typed tracks with `AddTrack` and heterogeneous channels with `CreateTrack` plus
`CreateTrackCore<T>`. The runtime base owns preparation, activation, publication, and disposal;
the panel activates the graph only after taking ownership of the completed runtime.

## MotionTrack

`MotionTrack<T>` is the abstract base for a typed `IObservable<T>` binding at
`BindingPriority.Animation`. Derived tracks implement `Evaluate(T baseValue)` and may override
the public `InterpolateValue` overloads when they own a different value-space algorithm.

The public track lifecycle is:

```text
Prepare → Activate → ValidatePreparedValue → EnablePublication → Publish* → Dispose
```

`DisablePublication` is used when a candidate is rolled back. The runtime calls these methods in
that order, but they are public so custom runtimes can participate directly. The typed base class
provides the normal binding and publication implementation; a fully custom lifecycle can derive
from the non-generic `MotionTrack` instead.

| Member | Purpose |
|---|---|
| `Target` | Object receiving the animation-priority value |
| `Property` | Strongly typed Avalonia property |
| `Publish()` | Evaluate and publish the current value if it changed |
| `Dispose()` | Remove the binding and restore the current base value |

The base class follows the property's non-animation base value and suppresses equal consecutive
publications. `MotionRuntime.CreateTrack(MotionChannel)` resolves the channel's declared value
type through a private cached bridge before calling the derived runtime's `CreateTrackCore<T>`.
The metadata overload of public `InterpolateValue` is virtual; when a custom track does not
override it, the default implementation falls back through endpoint, transition, Motion-level, and
finally Avalonia's registered animator.

## KeyFrameMotionTrack

`KeyFrameMotionTrack<T>` is the common concrete sparse track used directly by both
`SequenceMotion` and `TimelineMotion`.

| Member | Purpose |
|---|---|
| `Progress` | Current normalized progress |
| `Keys` | Ordered sparse `KeyFrameMotionTrack<T>.Key` collection |
| `Publish(progress)` | Set normalized progress and publish the evaluated value |

Each nested `Key` exposes its optional source keyframe, immutable `Cue`, initialized value,
and endpoint metadata. Segment lookup is binary. A channel gets a key only when that channel is
explicitly authored at the corresponding keyframe.

## IKeyFrameMotion

The shared runtime protocol implemented by `SequenceMotion` and `TimelineMotion`.

| Member | Purpose |
|---|---|
| `IObservable<double>` | Immediately publish current normalized progress, then later changes |
| `KeyFrames` | Keyframes in ascending Cue order |
| `GetCue(keyFrame)` | Map an authored keyframe to the same normalized axis |

Timeline keeps absolute `Time` and `KeyTime` in its authoring API, but normalizes both against
`Duration` before publishing or compiling tracks.

## StateMotion

Complete named-state motion. Derives from `PlayerDrivenMotion`.

| Member | Purpose |
|---|---|
| `State` | Requested state key |
| `Duration` | Default transition duration |
| `Easing` | Default transition easing |
| `States` | `MotionState` collection and XAML content |
| `Edges` | Directed `MotionEdge` overrides |
| `Player` | Internal transition clock, also available for inspection |

## MotionState

| Member | Purpose |
|---|---|
| `Key` | State identity |
| `Template` | Complete live layout template and XAML content |

A missing target has zero presence in that state.

## MotionEdge

| Member | Purpose |
|---|---|
| `From`, `To` | Directed state keys |
| `Duration` | Optional duration override |
| `Easing` | Optional easing override |
| `Interpolations` | Edge-specific property overrides |

## SequenceMotion

Sparse normalized-progress motion.

| Member | Purpose |
|---|---|
| `Progress` | Clamped zero-to-one evaluation coordinate |
| `Easing` | Default incoming keyframe easing |
| `KeyFrames` | `SequenceMotionKeyFrame` collection and XAML content |
| `Interpolations` | Motion-wide property overrides |

SequenceMotion intentionally has no player.

## TimelineMotion

Sparse absolute-time motion. Derives from `PlayerDrivenMotion`.

| Member | Purpose |
|---|---|
| `Time` | Current authored timeline position |
| `Duration` | Read-only latest key time |
| `Loop` | Whether playback wraps |
| `Player` | Frame-aligned playback clock |
| `KeyFrames` | `TimelineMotionKeyFrame` collection and XAML content |

## MotionKeyFrame

Abstract shared base for sparse keyframes.

| Member | Purpose |
|---|---|
| `Template` | Sparse live layout template and XAML content |
| `Easing` | Default easing for the segment arriving here |

Concrete types:

- `SequenceMotionKeyFrame.Cue` uses normalized Avalonia `Cue`;
- `TimelineMotionKeyFrame.KeyTime` uses non-negative `TimeSpan`.

## MotionSlot

Named layout and property proxy.

| Member | Purpose |
|---|---|
| `Name` | Target identity |
| `Setters` | Root target endpoint setters and XAML content |
| `TargetStyles` | Selector-based endpoint styles for the target subtree |

See [Target Properties](target-properties.md) for shortcut properties.

## MotionInterpolation

| Member | Purpose |
|---|---|
| `Target` | Target name for motion or edge scope |
| `Property` | Target or built-in layout property |
| `Easing` | Temporal progress mapping |
| `Interpolator` | Value-space interpolation algorithm |

## MotionInterpolator

`MotionInterpolator` is the non-generic base. Derive from `MotionInterpolator<T>` and override:

```csharp
protected abstract T Interpolate(
    double progress,
    T oldValue,
    T newValue);
```

Built-ins:

- `MotionAvaloniaInterpolator` — Avalonia animator-based fallback;
- `CubicBezierMotionInterpolator` — point interpolation using endpoint-relative control offsets.

## PlayerDrivenMotion

Abstract motion with one stable `MotionPlayer`. Derived built-ins are `StateMotion` and `TimelineMotion`.

## MotionPlayer

| Member | Purpose |
|---|---|
| `Time` | Absolute elapsed clock time |
| `State` | `Stopped`, `Playing`, or `Paused` |
| `Tick` | Frame-aligned `(time, deltaTime)` event |
| `Attach(Visual)` | Attach to a TopLevel clock and return the owning lease |
| `Play()` | Start or resume |
| `Pause()` | Pause without resetting |
| `Reset()` | Reset time without changing state |
| `Stop()` | Pause and reset |

The `IDisposable` returned by `Attach` must be released before attaching the same player to another visual.

## MotionTickEventHandler

```csharp
public delegate void MotionTickEventHandler(
    MotionPlayer sender,
    TimeSpan time,
    TimeSpan deltaTime);
```
