# Core Concepts

[Documentation index](README.md)

## Stable Targets

The visible controls are direct `MotionPanel.Children`. Motion never replaces them, so text, selection, focus,
validation, event handlers, and control-specific state survive every layout change.

Each target must have a unique `x:Name`. That name is the identity shared with `MotionSlot`.

## Live Layout Sources

`MotionState`, `SequenceMotionKeyFrame`, and `TimelineMotionKeyFrame` each own a `DataTemplate`. MotionPanel builds and retains a
live hidden host for every source owned by the current motion.

These templates are not snapshots. They participate in normal Avalonia measure and arrange, inherit data context and
resources, and respond to bindings and panel size changes.

## MotionSlot

`MotionSlot` is a proxy with two roles:

1. its bounds describe the target's layout endpoint;
2. its shortcuts, setters, and target styles describe property endpoints.

The slot is not the displayed control. The target is measured to supply the slot's desired size, then the evaluated
layout is applied to the stable target container.

## Complete States and Sparse Keyframes

`StateMotion` treats every `MotionState` as a complete description. A missing target is absent and receives
`Presence=0`.

`SequenceMotion` and `TimelineMotion` treat their keyframes as sparse. If a keyframe omits a target or property, no key
is added to that channel. The channel interpolates between its own nearest authored keyframes.

This distinction is intentional:

- states answer “what does the complete interface look like now?”;
- keyframes answer “which channels receive keys at this coordinate?”.

## Channels and Tracks

A channel is one property on one destination. Built-in layout channels are:

- `Motion.DesiredSize` for the panel;
- `Motion.Position` for a target container;
- `Motion.Size` for a target container;
- `Motion.Presence` for a target container.

Target properties such as `Opacity`, `Background`, and `RenderTransform` use the same channel model.

When a motion is applied, each discovered channel becomes one long-lived track and one Avalonia animation-priority
binding. Evaluation updates those bindings; it does not search for an interpolator or recreate a binding every frame.

## Presence

Presence is a convenience envelope for entering and leaving targets. It controls container opacity and hit testing.
It is separate from target `Opacity`, so both can be authored:

```xml
<motion:MotionSlot
  x:Name="Preview"
  motion:Motion.Presence="0.5"
  Opacity="0.8"/>
```

In a complete state, omitting the slot is equivalent to absence. In a sparse keyframe, omission means “no key”.

## Easing and Interpolation

Easing remaps temporal progress. A `MotionInterpolator` maps that progress through the property's value space:

```text
raw segment progress
        ↓
      Easing
        ↓
MotionInterpolator(source, destination)
        ↓
 evaluated property value
```

The default interpolator reuses Avalonia's typed `Animator<T>` implementations where possible. Overrides may be
attached to a setter, a slot, a directed edge, a motion, or the panel.

## Ownership

`MotionPanel.Motion` is a logical child. A motion therefore receives normal data context, resource lookup, and logical
tree lifetime.

`StateMotion` and `TimelineMotion` derive from `PlayerDrivenMotion` and own a stable `MotionPlayer`. `SequenceMotion`
deliberately has no player because its progress belongs to an external slider, scroll viewer, gesture, or solver.

---

[Next: States and Slots →](states-and-slots.md)
