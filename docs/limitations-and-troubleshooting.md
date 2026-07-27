# Limitations and Troubleshooting

Motion.Avalonia is an early preview. Its current scope is deliberately narrower than a general shared-element or animation-authoring system.

## Current Scope

### Targets must be direct children

Every motion target must be a direct logical child of the same `MotionPanel`.

Targets in different ancestors, clipping regions, pages, windows, or top levels are not supported by the current layout-morphing mechanism.

### Targets need stable unique names

Each direct child must have a non-empty, unique `x:Name`. `MotionSlot.Target` uses that exact case-sensitive name.

Names are the identity contract. Do not replace a target control merely to represent another state.

### State templates describe layout

State templates are live, but they are not alternate copies of the real content. Put `MotionSlot` proxies and inexpensive layout structure in a state template.

Do not put a second text editor, media player, or other stateful replacement control in the template and expect its state to transfer to the target.

### Styled properties only

Target-property setters currently support Avalonia styled properties. Direct properties are rejected.

Properties without a compatible Avalonia animator use a discrete halfway switch unless a custom `MotionInterpolator<T>` is registered.

### No bitmap or pseudo-layout fallback

Motion.Avalonia does not capture target bitmaps and does not scale a snapshot to disguise layout changes.

This preserves real layout behavior, but it also means complex target trees may be measured at intermediate sizes during motion.

### Focus migration is not yet a policy

When presence reaches zero, the target container becomes non-hit-testable and is arranged at zero size. The current panel does not define a complete focus-transfer policy for a descendant that already owns keyboard focus.

Applications with keyboard-heavy flows should explicitly move focus when changing to a state that removes the focused target.

## Troubleshooting Table

| Symptom | Likely cause | What to check |
| --- | --- | --- |
| State changes immediately | Zero duration, null easing, or first presentation | Check controller timing; initial presentation intentionally snaps |
| Nothing is visible | Controller pose refers to no matching state | Compare `State` or pose keys with `MotionState.Key` |
| A target is missing | State intentionally or accidentally omits its slot | Check `MotionSlot.Target` spelling and template branch |
| “No direct child has that name” | Slot target cannot be resolved | Add a direct child with the matching `x:Name` |
| Duplicate target exception | Repeated child name or repeated slot in one state | Keep child names and per-state slot targets unique |
| Property jumps at halfway | No compatible animator | Add a typed interpolator or accept discrete behavior |
| Setter value appears ignored | Property is invalid, direct, duplicated, or not registered on the target | Read the complete exception and inspect the target type |
| Resize changes the target abruptly | The controller currently presents a state pose | A static state follows live layout immediately by design |
| Resize has a damped motion | A custom controller continues publishing a blend | Present `MotionPose.State` when the motion should be at rest |
| Slider motion feels stepped | Input is quantized or rounded before reaching the controller | Bind a continuous `double` and map local segment progress |
| Scroll progress is unstable | Scrollable range changes during the motion | Recompute from current `Extent` and `Viewport`; avoid feedback loops |
| Target remains keyboard-focused after leaving | Focus migration is application-owned | Move focus before selecting the state that omits the target |

## The First State Does Not Animate

`BasicMotionController` waits until the panel has completed its first presentation before animating state changes. Before that point it publishes the requested state directly.

This avoids an artificial transition from an empty, zero-sized layout during page load.

If a deliberate entrance animation is required, present a real initial state first, then request the destination after the panel is loaded and presented.

## No Frame Updates

For a custom time-based controller:

1. Derive from `MotionController`.
2. Schedule through protected `RequestAnimationFrame`.
3. Keep requesting the next frame while animation is active.
4. Call `SetCurrentPose` with a new pose on each meaningful frame.
5. Resume scheduling when the owner enters the visual tree.
6. Ignore or cancel stale work after detachment.

`RequestAnimationFrame` returns `false` when the controller is not attached to a panel with a current `TopLevel`.

Changing ordinary fields without publishing a pose does not notify the panel.

## Layout Changes Only After Pointer Movement

This symptom usually means a custom implementation changed coordinates without entering Avalonia's layout invalidation path.

Motion.Avalonia's internal item container solves this by exposing layout-affecting properties:

- position changes invalidate parent arrange;
- width or height changes invalidate parent measure;
- controller pose changes invalidate panel measure.

If the symptom appears in a custom panel or controller, verify that the property change raises through Avalonia and invalidates the correct layout phase. Calling `InvalidateMeasure` on an already-invalid element is intentionally a no-op; the important part is changing a registered property or invalidating from the element that owns the layout dependency.

## Duplicate Interpolator Errors

For one target and property, the panel permits:

- at most one general interpolator;
- at most one interpolator for each active edge.

An edge-specific override and a general fallback may coexist. Two general entries, or two entries that both contain the same active edge, are ambiguous and throw.

Every interpolator in `MotionPanel.Interpolators` must set both `Target` and `Property`, even when it is not selected by the current pose.

## `IsVisible` Versus Presence

`IsVisible` participates in Avalonia layout as a boolean. Changing it can therefore remove a control from layout immediately.

For a motion target:

- omit or include its slot to animate presence;
- use explicit `Opacity` when visual alpha is part of the state;
- use an `IsVisible` setter only when a discrete visibility change is intentional.

Presence is applied to the target container and does not overwrite the target's own opacity.

## Clipping

`MotionPanel` only controls its own targets and containers. Ancestor clipping still applies.

If a moving target is cut off:

- inspect `ClipToBounds` on the panel and its ancestors;
- inspect explicit `Clip` values;
- confirm the intended bounds remain inside the supported single-panel scene.

Crossing unrelated clipping ancestors is a shared-element problem and is outside the current implementation.

## Binding and Resource Updates

State template and setter bindings stay live. If an endpoint does not update:

- confirm the binding resolves in the state template's data context;
- confirm the bound value is valid for the property;
- check that the state is required by the current pose;
- check that the change occurs on the UI thread;
- inspect whether a local template binding is attached to the slot rather than the real target.

Remember that the state template determines endpoint values. Ordinary bindings that belong to the real control should remain on the real control.

## Reporting an Issue

A useful reproduction includes:

- the smallest `MotionPanel` and state templates that show the problem;
- the controller and exact pose or state sequence;
- whether resize, interruption, or target-property setters are involved;
- the full exception and stack trace;
- Avalonia, .NET, and Motion.Avalonia versions;
- whether the behavior reproduces in the demo project.

Next: [API Reference](api-reference.md)
