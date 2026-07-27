# Motion.Avalonia Documentation

[Project README](../README.md) · [Start the tutorial](getting-started.md)

Motion.Avalonia describes motion as a composition of live layout states, controller-produced poses, and property
interpolators. This documentation starts with a complete example, then separates the system into concepts that can be
used independently.

## Learning Path

1. [Getting Started](getting-started.md) — build and control a complete two-state layout motion.
2. [Core Concepts](core-concepts.md) — understand stable targets, live state templates, poses, and presence.
3. [States and Slots](states-and-slots.md) — create responsive layout endpoints with ordinary Avalonia panels.
4. [Controllers and Poses](controllers-and-poses.md) — drive motion from time, sliders, scrolling, or custom logic.
5. [Target Properties](target-properties.md) — animate target properties with shortcuts and setters.
6. [Interpolators](interpolators.md) — customize how values move between endpoints.
7. [Recipes](recipes.md) — assemble the primitives into practical interface patterns.
8. [Architecture](architecture.md) — learn the implementation, lifetime, and performance model.
9. [Limitations and Troubleshooting](limitations-and-troubleshooting.md) — diagnose common configuration problems.
10. [API Reference](api-reference.md) — look up the contract of each public type.

## Choose by Goal

| Goal | Start here |
|---|---|
| Build a first layout transition | [Getting Started](getting-started.md) |
| Move one control between unrelated panel layouts | [States and Slots](states-and-slots.md) |
| Preserve text input, selection, and local state | [Core Concepts: Stable Targets](core-concepts.md#stable-targets) |
| Use different timing from A → B and B → A | [Directed Edges](controllers-and-poses.md#directed-edges) |
| Bind motion to a slider or scroll offset | [Progress-driven Controllers](controllers-and-poses.md#a-progress-driven-controller) |
| Animate `CornerRadius`, `Background`, or another styled property | [Target Properties](target-properties.md) |
| See the complete shortcut list | [Shortcut Properties](target-properties.md#shortcuts) |
| Define a custom path or interpolation rule | [Strongly Typed Interpolators](interpolators.md#a-strongly-typed-custom-interpolator) |
| Understand interruption behavior | [Nested Poses and Interruption](controllers-and-poses.md#nested-poses-and-interruption) |
| Understand allocations and per-frame work | [Architecture](architecture.md) |
| Investigate a configuration exception | [Limitations and Troubleshooting](limitations-and-troubleshooting.md) |

## Documentation Conventions

- Examples use the `motion` XAML namespace: `xmlns:motion="using:Motion.Avalonia"`.
- State keys are ordinary objects. Examples use strings for readability.
- “Target” means a stable direct child of `MotionPanel`.
- “Slot” means the live layout proxy inside a state template.
- “Presence” means the panel-owned enter/leave weight; it is not the target's `Opacity` property.
- Current behavior is documented separately from planned features.

---

[Next: Getting Started →](getting-started.md)
