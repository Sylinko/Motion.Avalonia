# Controllers and Poses

`MotionPanel` describes what each state looks like. A motion controller decides which state, or which blend of states, the panel presents now.

This separation keeps layout definitions independent from time, input, playback, and navigation. The same states can be driven by a button, a slider, a `ScrollViewer`, a timer, or an application-specific solver.

## The Controller Contract

The smallest controller contract is `IMotionController`:

```csharp
public interface IMotionController
{
    MotionPose CurrentPose { get; }

    event EventHandler? CurrentPoseChanged;
}
```

The panel listens to `CurrentPoseChanged` and evaluates `CurrentPose`. There is no required `Duration`, `Easing`, `State`, or playback API at this level.

For most custom controllers, derive from `MotionController` instead of implementing the interface directly. It adds:

- Avalonia property-system integration;
- safe attachment and detachment from a single `MotionPanel`;
- `SetCurrentPose` for publishing a pose;
- a controller-owned `Player` for controllers that consume playback progress;
- `PlayerProgressChanged` for opting into that player;
- `RequestAnimationFrame` for frame-aligned animation;
- lifecycle hooks for the panel and visual tree.

## Controller-Owned Playback

`MotionController.Player` is null until a derived controller subscribes to the protected
`PlayerProgressChanged` event. The first subscription creates one stable `MotionPlayer`; removing the last subscription
stops and releases it. Event subscription and every player operation are UI-thread-affine.

`MotionPlayer` is public so applications can use advanced transport controls without taking over ownership:

```csharp
controller.Player?.Pause();
controller.Player?.Resume();
controller.Player?.Seek(0.5);
```

`Play(duration)` always starts at progress zero. `Pause` and `Resume` preserve position, `Stop` leaves the current value,
and natural completion leaves progress at one. The player publishes linear progress; easing remains the responsibility
of edges, stops, poses, and interpolators.

## BasicMotionController

`BasicMotionController` is the general state-switching controller. It exposes a requested `State`, default timing, and optional directed edge overrides.

```xml
<motion:MotionPanel>
    <motion:MotionPanel.Controller>
        <motion:BasicMotionController
            State="{Binding CurrentState}"
            Duration="0:0:0.3">
            <motion:MotionEdge
                From="Compact"
                To="Expanded"
                Duration="0:0:0.5"
                Easing="CubicEaseOut" />
        </motion:BasicMotionController>
    </motion:MotionPanel.Controller>

    <!-- Targets and states -->
</motion:MotionPanel>
```

The controller uses `Duration` and `Easing` unless a matching edge supplies an override.

The defaults are:

| Property | Default | Meaning |
| --- | --- | --- |
| `State` | `null` | Requested state key |
| `Duration` | `00:00:00.300` | Default transition duration |
| `Easing` | `LinearEasing` | Default progress curve |

Set `Duration` to zero or `Easing` to `null` to snap to the requested state. The first state is also presented immediately; an initial page load does not animate in from an undefined layout.

## Directed Edges

A `MotionEdge` describes one direction only. `Compact → Expanded` and `Expanded → Compact` may therefore use different timing:

```xml
<motion:BasicMotionController
    State="{Binding CurrentState}"
    Duration="0:0:0.25">
    <motion:MotionEdge From="Compact"
                       To="Expanded"
                       Duration="0:0:0.5"
                       Easing="CubicEaseOut" />

    <motion:MotionEdge From="Expanded"
                       To="Compact"
                       Duration="0:0:0.18"
                       Easing="CubicEaseIn" />
</motion:BasicMotionController>
```

An omitted edge duration inherits `BasicMotionController.Duration`. An omitted edge easing inherits `BasicMotionController.Easing`.

Only one edge may match a given `(From, To)` pair. Duplicate matches are treated as configuration errors instead of relying on collection order.

## MotionPose

A controller does not push coordinates into the panel. It publishes a `MotionPose`, which is a live composition of state endpoints.

### One state

```csharp
MotionPose.State("Expanded")
```

This presents the current live layout of the `Expanded` state.

### A blend

```csharp
MotionPose.CreateBlend(
    MotionPose.State("Compact"),
    MotionPose.State("Expanded"),
    progress);
```

`progress` is finite and normalized to the range from `0` to `1`. The optional easing defaults to linear:

```csharp
MotionPose.CreateBlend(
    MotionPose.State("Compact"),
    MotionPose.State("Expanded"),
    progress,
    new CubicEaseInOut());
```

The optional `MotionEdge` argument associates the blend with a directed edge. This lets a custom controller participate
in edge-specific interpolator lookup without going through `BasicMotionController`:

```csharp
MotionPose.CreateBlend(
    MotionPose.State(edge.From),
    MotionPose.State(edge.To),
    progress,
    easing: edge.Easing,
    edge: edge);
```

Reuse the same edge instance that is registered with the controller or interpolator. Edge selection uses object
identity, not only the `From` and `To` values.

Poses can be nested. This is important when a transition is interrupted: `BasicMotionController` uses the currently presented pose as the next transition's source, so the target does not jump back to a previous state before changing direction.

### Nested Poses and Interruption

Suppose `Compact → Expanded` reaches 40% before the requested state changes back to `Compact`. The next transition is conceptually:

```text
Blend(
  Blend(Compact, Expanded, 0.40),
  Compact,
  newProgress)
```

The inner blend remains live. Its state layouts and property endpoints can still respond to resize and binding changes while the outer transition proceeds.

## SequenceMotionController

`SequenceMotionController` maps one normalized `Progress` value across an ordered collection of `MotionStop` objects. It
is useful for sliders, scroll-linked motion, gesture scrubbing, and any other input that already owns progress:

```xml
<motion:SequenceMotionController Progress="{Binding Progress}">
    <motion:MotionStop State="Immersive"
                       Offset="0" />
    <motion:MotionStop State="Reveal"
                       Offset="0.38"
                       Easing="CubicEaseOut" />
    <motion:MotionStop State="Discussion"
                       Offset="1"
                       Easing="CubicEaseInOut" />
</motion:SequenceMotionController>
```

`Progress` and every `Offset` use the range from `0` to `1`. Stops are evaluated in ascending offset order; declaration
order is preserved when offsets are equal. Before the first stop and after the last stop, the controller holds the
nearest endpoint. An empty collection publishes `MotionPose.Empty`, while one stop always presents that state.

At an exact stop offset, the controller publishes `MotionPose.State`, so a settled layout continues to respond directly
to resize and binding changes. Between two stops, it publishes a blend with local segment progress. `Easing` belongs to
the target stop and shapes the interval arriving at that stop; `null` means linear.

Duplicate offsets express an instantaneous cut. The interval on the left still approaches the first stop at that
offset, while the exact offset and its right side use the last stop declared at that offset.

The sequence controller has no clock, `Play`, or `Duration`, and it intentionally does not opt into a
controller-owned player. `Progress` is its single source of truth. Bind it to a `Slider`, scroll calculation, gesture,
solver, or any other external value that owns position.

## TimelineMotionController

`TimelineMotionController` evaluates absolute `Time` across an ordered collection of `MotionKeyFrame` objects. Use it
when authored timing matters independently of the eventual playback speed:

```xml
<motion:TimelineMotionController
    x:Name="Timeline"
    Time="{Binding PreviewPosition}">
    <motion:MotionKeyFrame
        State="Intro"
        KeyTime="0:0:0" />
    <motion:MotionKeyFrame
        State="Reveal"
        KeyTime="0:0:0.8"
        Easing="CubicEaseOut" />
    <motion:MotionKeyFrame
        State="Discussion"
        KeyTime="0:0:2.4"
        Easing="CubicEaseInOut" />
</motion:TimelineMotionController>
```

`Duration` is read-only and equals the latest `KeyTime`. Setting `Time` seeks the controller and its player. Values
outside the authored range hold the nearest endpoint. Start frame-aligned playback with:

```csharp
Timeline.Player?.Play(Timeline.Duration);
```

The duration passed to `Play` controls how long the player takes to traverse normalized playback progress. Passing
`Timeline.Duration` plays at the authored rate; a different positive duration changes the playback rate without
rewriting key times.

Keyframes are stable-sorted by `KeyTime`. At an exact key time the controller publishes a state pose. Between
keyframes it publishes a blend whose local progress is shaped by the destination keyframe's `Easing`. Duplicate key
times form an instantaneous cut, with the last declared keyframe winning at and after that time.

Changing the keyframe collection or a `KeyTime` while playback is active stops the player, preserves the current
absolute time, and seeks against the newly derived duration. Changing only `State` or `Easing`
reevaluates the current pose without stopping playback.

## A Progress-Driven Controller

Use `SequenceMotionController` for ordinary ordered state sequences. A custom controller remains useful when progress
selects poses through a different mapping, such as a solver, graph, or procedural rule:

```csharp
using Avalonia;
using Motion.Avalonia;

public sealed class ProgressMotionController : MotionController
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ProgressMotionController, double>(
            nameof(Progress),
            validate: static value => double.IsFinite(value),
            coerce: static (_, value) => Math.Clamp(value, 0, 1));

    static ProgressMotionController()
    {
        ProgressProperty.Changed.AddClassHandler<ProgressMotionController>(
            static (controller, _) => controller.PublishPose());
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public ProgressMotionController() => PublishPose();

    private void PublishPose()
    {
        SetCurrentPose(
            MotionPose.CreateBlend(
                MotionPose.State("Compact"),
                MotionPose.State("Expanded"),
                Progress));
    }
}
```

This controller has no `Play`, `Pause`, or `Duration` because progress is its only responsibility. A different custom
controller can subscribe to `PlayerProgressChanged` when time-based playback is part of its contract.

## Frame Scheduling

Most time-based custom controllers should subscribe to `PlayerProgressChanged` and translate `Player.Progress` into a
pose. The base class creates the player on the first subscription and schedules it through the owning panel's
`TopLevel`.

The protected `RequestAnimationFrame` method remains available for controllers whose progression cannot be represented
by linear player time. Do not keep an unrelated background timer that mutates UI state.

The relevant lifecycle hooks are:

| Hook | Purpose |
| --- | --- |
| `OnAttached` | The controller was assigned to a panel |
| `OnDetached` | The controller is about to lose its panel |
| `OnOwnerAttachedToVisualTree` | The panel entered a visual tree |
| `OnOwnerDetachedFromVisualTree` | The panel left a visual tree |
| `OnOwnerPresented` | The panel completed its first presentation |

`MotionPlayer` uses these hooks so active playback follows render frames and a detached panel does not retain a frame
loop. `BasicMotionController` additionally completes its active transition when the panel leaves the visual tree.

## Choosing a Controller

Use `BasicMotionController` when application state changes discretely and timing belongs to the transition.

Use `SequenceMotionController` when an external value owns position in an ordered sequence:

- scroll offset;
- slider value;
- pointer drag distance;
- gesture progress;

Use `TimelineMotionController` when absolute authored times own position, including playback, time seeking, or media
synchronization.

Create a custom controller when pose selection is not an ordered sequence, for example when it comes from a physics
solver or an application-specific graph.

Next: [Target Properties](target-properties.md)
