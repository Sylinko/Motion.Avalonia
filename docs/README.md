# Motion.Avalonia Documentation

[Project README](../README.md) · [Start the tutorial](getting-started.md)

Motion.Avalonia combines stable target controls, live layout templates, independently evaluated property tracks, and
strongly typed interpolation. Start with named states, then move to progress or time only when the interaction requires
it.

## Learning Path

1. [Getting Started](getting-started.md) — build a complete two-state layout transition.
2. [Core Concepts](core-concepts.md) — understand targets, layout sources, channels, and presence.
3. [States and Slots](states-and-slots.md) — author responsive complete-state layouts.
4. [Motions and Keyframes](motions-and-keyframes.md) — choose `StateMotion`, `SequenceMotion`, or `TimelineMotion`.
5. [Target Properties](target-properties.md) — animate properties with shortcuts, setters, and target styles.
6. [Interpolators](interpolators.md) — customize easing and value-space interpolation.
7. [Recipes](recipes.md) — assemble practical interaction patterns.
8. [Architecture](architecture.md) — learn Apply, track lifetime, and per-frame work.
9. [Limitations and Troubleshooting](limitations-and-troubleshooting.md) — diagnose configuration problems.
10. [API Reference](api-reference.md) — look up public contracts.

## Choose by Goal

| Goal | Start here |
|---|---|
| Build a first layout transition | [Getting Started](getting-started.md) |
| Move controls between unrelated panel layouts | [States and Slots](states-and-slots.md) |
| Preserve text input, selection, and local state | [Stable Targets](core-concepts.md#stable-targets) |
| Use different timing from A → B and B → A | [Directed Edges](motions-and-keyframes.md#directed-edges) |
| Bind motion to a slider or scroll offset | [SequenceMotion](motions-and-keyframes.md#sequencemotion) |
| Author motion at absolute times and play it | [TimelineMotion](motions-and-keyframes.md#timelinemotion) |
| Animate `CornerRadius`, `Background`, or nested content | [Target Properties](target-properties.md) |
| See the shortcut property list | [Shortcut Properties](target-properties.md#shortcut-properties) |
| Define a custom interpolation rule | [Interpolators](interpolators.md) |
| Understand allocations and per-frame work | [Architecture](architecture.md) |
| Investigate a configuration exception | [Troubleshooting](limitations-and-troubleshooting.md) |

## Terminology

- A **target** is a stable direct child of `MotionPanel`.
- A **layout source** is a live `DataTemplate` owned by a state or keyframe.
- A **slot** is a named proxy inside a layout source.
- A **channel** is one target/property pair, including panel and container layout properties.
- **Presence** is the container-level enter/leave envelope; it is distinct from target `Opacity`.
- A **track** is the ordered endpoint sequence for one channel.

---

[Next: Getting Started →](getting-started.md)
