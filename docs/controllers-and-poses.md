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
- `RequestAnimationFrame` for frame-aligned animation;
- lifecycle hooks for the panel and visual tree.

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
MotionPose.Blend(
    MotionPose.State("Compact"),
    MotionPose.State("Expanded"),
    progress);
```

`progress` is finite and normalized to the range from `0` to `1`. The optional easing defaults to linear:

```csharp
MotionPose.Blend(
    MotionPose.State("Compact"),
    MotionPose.State("Expanded"),
    progress,
    new CubicEaseInOut());
```

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

## A Progress-Driven Controller

A controller can expose normalized progress instead of time. That progress can be bound to a `Slider`, scroll position, gesture recognizer, or any other source.

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
            MotionPose.Blend(
                MotionPose.State("Compact"),
                MotionPose.State("Expanded"),
                Progress));
    }
}
```

This controller has no `Play`, `Pause`, or `Duration` because progress is its only responsibility. A separate player can advance `Progress` over time when playback is needed.

## More Than Two Stops

For an ordered sequence, map a continuous value to two neighboring states:

```csharp
var scaled = Math.Clamp(progress, 0, 1) * (states.Count - 1);
var sourceIndex = Math.Min((int)scaled, states.Count - 2);
var localProgress = scaled - sourceIndex;

SetCurrentPose(
    MotionPose.Blend(
        MotionPose.State(states[sourceIndex]),
        MotionPose.State(states[sourceIndex + 1]),
        localProgress));
```

This is useful for:

- a four-state slider showcase;
- scroll-linked storytelling;
- scrubbers and timelines;
- gesture-controlled transitions.

The state stops, input mapping, and time source remain independent concepts. A timer-based player can drive the same progress controller without changing the panel or its state templates.

## Frame Scheduling

Time-based custom controllers should call the protected `RequestAnimationFrame` method. The callback is scheduled through the owning panel's `TopLevel`, follows the visual-tree lifetime, and receives Avalonia's frame timestamp.

Do not keep an unrelated background timer that mutates UI state. Start or resume frame requests from the controller lifecycle, stop producing frames after detachment, and publish a new pose only when its value changes.

The relevant lifecycle hooks are:

| Hook | Purpose |
| --- | --- |
| `OnAttached` | The controller was assigned to a panel |
| `OnDetached` | The controller is about to lose its panel |
| `OnOwnerAttachedToVisualTree` | The panel entered a visual tree |
| `OnOwnerDetachedFromVisualTree` | The panel left a visual tree |
| `OnOwnerPresented` | The panel completed its first presentation |

`BasicMotionController` uses these hooks so that initial presentation snaps, active animation follows render frames, and a detached panel does not retain a frame loop.

## Choosing a Controller

Use `BasicMotionController` when application state changes discretely and timing belongs to the transition.

Create a progress-driven controller when an external value owns position in the motion:

- scroll offset;
- slider value;
- pointer drag distance;
- gesture progress;
- physics solver output.

Keep playback in a separate object when the same sequence should support both manual scrubbing and timed playback.

Next: [Target Properties](target-properties.md)
