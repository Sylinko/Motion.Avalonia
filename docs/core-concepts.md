# Core Concepts

[← Getting Started](getting-started.md) · [Documentation](README.md) · [Next: States and Slots →](states-and-slots.md)

Motion.Avalonia is easier to reason about when layout description, motion progress, interpolation, and real content are
treated as separate concerns.

## Stable Targets

A target is a direct `MotionPanel` child with a unique `x:Name`:

```xml
<motion:MotionPanel>
  ...
  <TextBox x:Name="SearchEditor"/>
  <Border x:Name="Preview"/>
</motion:MotionPanel>
```

The target remains the same control instance in every state. MotionPanel inserts a stable internal container between
itself and each target to own animated position, size, presence, and relative Z-order. The target itself keeps its
logical parent, bindings, input state, focus state, and event subscriptions.

## Live State Templates

A `MotionState` owns an `IDataTemplate` that describes one layout endpoint:

```xml
<motion:MotionState Key="Centered">
  <motion:MotionState.Template>
    <DataTemplate>
      <Grid>
        <motion:MotionSlot
          x:Name="SearchEditor"
          MaxWidth="600"
          HorizontalAlignment="Center"/>
      </Grid>
    </DataTemplate>
  </motion:MotionState.Template>
</motion:MotionState>
```

The template is instantiated once while the state belongs to the panel. It remains connected to the panel's data
context, resources, and visual lifetime. A state is therefore not a frozen snapshot. Bindings can change, targets can
change their desired sizes, and a panel resize can produce a different endpoint.

Only states referenced by the current pose are measured and arranged for the current frame.

## Motion Slots

`MotionSlot` is a proxy, not the content that the user sees. It serves three purposes:

1. It measures the real target to obtain an accurate desired size.
2. It participates in the state template's ordinary Avalonia layout.
3. It exposes selected property endpoints for the target.

The slot's resolved rectangle becomes the target container's rectangle. Slot layout properties such as `Width`,
`MaxWidth`, `Margin`, alignment, and attached panel properties configure that rectangle.

## Motion Poses

A `MotionPose` describes what the panel should present now. It contains no timer:

```csharp
var settled = MotionPose.State("Centered");

var halfway = MotionPose.CreateBlend(
    MotionPose.State("Centered"),
    MotionPose.State("Wide"),
    0.5,
    new CubicEaseInOut());
```

`MotionPose.State` references one state key. `MotionPose.CreateBlend` recursively combines two poses with normalized
progress and easing. It can also carry a `MotionEdge` selected by the controller. Because blends can contain other
blends, an interrupted transition can use the exact current pose as its new source.

## Controllers

An `IMotionController` publishes the current pose. `MotionPanel` does not know whether progress came from elapsed time,
a scrollbar, a slider, pointer movement, a physics solver, or an external synchronization source.

`BasicMotionController` provides the common time-based state workflow. `SequenceMotionController` is a pure sampler
that maps externally owned normalized progress across ordered state stops. `TimelineMotionController` maps absolute
time across authored keyframes and derives from `PlayerDrivenMotionController`. Custom externally driven controllers
derive from `MotionController` and call `SetCurrentPose`; custom time-based controllers derive from
`PlayerDrivenMotionController` and override `OnPlayerTick`.

## Presence

Presence is an internal panel-owned value used when a target exists in only one endpoint:

```text
Absent ── presence 0.0
Halfway ─ presence 0.5
Present ─ presence 1.0
```

It is applied to the stable target container. It is intentionally separate from `Visual.OpacityProperty`, which belongs
to the target and can be declared through a shortcut or setter.

For a target with `Opacity="0.8"` at half presence, the effective rendered opacity is conceptually:

```text
container presence × target opacity = 0.5 × 0.8
```

Separating the values allows a target to preserve its own opacity semantics while still entering and leaving the state
layout.

## Property Endpoints

Each slot can contribute target property values in two ways:

- a documented shortcut such as `Opacity`, `ClipToBounds`, or `IsEnabled`;
- an explicit Avalonia `Setter`.

Both forms become the same property endpoint internally and are evaluated with the layout pose. Outputs use
`BindingPriority.Animation`, so the target's underlying local, styled, inherited, or bound value is not replaced. When
the motion no longer defines that property, the animation-priority output is removed and the latest base value becomes
effective again.

## Interpolators

An interpolator converts progress and two endpoint values into the current value:

```text
value = Interpolate(progress, source, target)
```

The default `MotionAvaloniaInterpolator` reuses Avalonia's built-in `Animator<T>` implementations where possible.
Panel-level interpolators can override one target/property pair, optionally only for selected directed edges. The
typed `MotionLayout` properties bring panel desired size, target position, target size, and target presence through the
same lookup path.

Easing and interpolation are different operations:

- easing maps progress to progress;
- interpolation maps two typed values and progress to a value.

## Interruption and Retargeting

When `BasicMotionController` is interrupted, it does not guess the current rectangle and does not force every property
into a detached snapshot. It retains its current pose as the source of the next blend. Required source states remain
live, so a resize during the interrupted motion still updates their layout endpoints.

## Real Layout, Not Visual Simulation

MotionPanel applies intermediate width and height to a stable container and performs real layout on the child. This
means:

- text wraps at its current width;
- nested grids and panels receive current constraints;
- hit testing follows the real bounds;
- rendering remains crisp;
- layout-dependent controls behave naturally.

This is more expensive than a render-only transform, so render transforms remain preferable for purely visual movement
that does not need layout participation. MotionPanel exists for cases where real layout is the behavior being animated.

---

[← Getting Started](getting-started.md) · [Documentation](README.md) · [Next: States and Slots →](states-and-slots.md)
