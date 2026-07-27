# Architecture

Motion.Avalonia treats animation as live layout evaluation, not as a rendered snapshot moving between rectangles.

This chapter describes the current implementation. Internal types are not public API, but understanding their roles helps when diagnosing layout, lifetime, and performance behavior.

## Ownership Model

One `MotionPanel` owns:

- stable logical target controls;
- one internal item container per target;
- persistent hosts for its `MotionState` templates;
- the currently attached controller;
- panel-level interpolator overrides;
- property-output channels for the real targets.

A target control remains the same object throughout all states. State templates contain only layout proxies and ordinary layout controls.

## Evaluation Pipeline

```text
Controller publishes MotionPose
        │
        ▼
Collect states required by the pose
        │
        ▼
Measure and arrange their live templates
        │
        ▼
Capture slot bounds, presence, properties, and paint order
        │
        ▼
Recursively evaluate the pose
        │
        ▼
Arrange stable target containers
        │
        ▼
Publish target properties at Animation priority
```

The pipeline runs through Avalonia's normal layout invalidation. A controller pose change invalidates measure, and the next layout pass captures the required endpoints.

## Persistent State Hosts

Each `MotionState` has an internal `MotionPanelHost`. The host builds the state's `DataTemplate`, keeps it connected to the logical and visual trees, and subscribes to its slots.

The host is invisible, non-interactive, and non-focusable. It exists to let Avalonia perform real layout:

- bindings remain active;
- resources and inherited values resolve normally;
- text and nested controls measure at the current constraints;
- a panel resize changes endpoint geometry;
- endpoint setter bindings can notify the owner.

The template is not rebuilt every frame. Replacing `MotionState.Template` rebuilds its host; ordinary layout and binding changes reuse it.

## Required-State Evaluation

The panel traverses the current pose and collects only the referenced states:

- `State(A)` requires `A`;
- `Blend(State(A), State(B), p)` requires `A` and `B`;
- an interrupted nested blend requires every distinct state reachable from that pose.

Only required states are measured and arranged for the current pass. This prevents unrelated state templates from taking part in every frame.

## Slot Capture

After a required state host is arranged, each `MotionSlot` contributes:

- bounds relative to the state host;
- presence `1`;
- explicitly set shortcut values;
- evaluated setter values;
- normalized relative `ZIndex`.

Slots can be nested under `Grid`, `DockPanel`, `StackPanel`, or custom panels. The host accumulates ancestor positions to convert their final bounds into its coordinate space.

A state that does not contain a target contributes no entry for it.

## Presence

When a target exists on only one side of a blend, the missing endpoint is synthesized from the existing bounds and properties with presence `0`.

The result is:

- stable geometry while entering or leaving;
- container opacity driven by presence;
- hit testing disabled near zero presence;
- no `IsVisible` layout jump.

When a pose contains no entry for a target, its container is arranged at zero size and made non-interactive.

## Stable Item Containers

Each target is hosted by an internal `MotionPanelItemContainer`. The container owns the layout-facing animated values:

- position;
- width;
- height;
- presence opacity;
- presence hit testing;
- relative Z order.

Changing its direct `Position` property invalidates parent arrange. Changing width or height invalidates parent measure. This follows the same general mechanism used by layout panels such as `Canvas`: animated container properties participate directly in Avalonia layout invalidation.

The target is arranged to fill its current container size. This is real intermediate layout, so its contents can reflow.

## Pose Evaluation

A state pose returns its latest captured layout.

A blend pose recursively evaluates its source and target, applies its easing, then interpolates:

- desired panel size;
- target rectangles;
- presence;
- target-property endpoints.

Because source and target are themselves poses, interruption does not require a bitmap or a frozen property snapshot. A currently presented blend can become the source of another blend.

## Property Outputs

Target-property values are published through a binding at `BindingPriority.Animation`.

For every active property output, the panel also observes non-animation changes on the destination and retains the latest base value. This allows a missing endpoint to blend against the current base and allows the base to reappear when motion stops supplying that property.

`ZIndex` is the exception in destination: it is published to the internal item container because the container, not the child, participates in the panel's paint order.

## Interpolator Resolution

For each target property in a blend, lookup is:

1. matching edge-specific panel interpolator;
2. matching general panel interpolator;
3. the shared `MotionAvaloniaInterpolator`.

The default implementation queries Avalonia's internal animator registration, creates a typed adapter, and caches it by property and endpoint runtime types. Brush, effect, and transform properties have specialized dispatch because their concrete value types determine the correct animator.

The project uses `IgnoresAccessChecksToGenerator` and a narrowly scoped `UnsafeAccessor` to reuse Avalonia's internal animator lookup. This removes the earlier reflection-style property interpolation path while preserving Avalonia's own type-aware behavior.

## Frame Scheduling

`MotionController.RequestAnimationFrame` delegates to the owning panel, which schedules through the current `TopLevel`.

The panel verifies controller identity both when scheduling and when the callback runs. A detached or replaced controller therefore cannot continue to mutate that panel.

`BasicMotionController` tracks a frame generation so stale callbacks from an earlier animation are ignored after interruption or detachment.

## Threading

The layout, controller callbacks, endpoint capture, and property publication are UI-thread operations. The current implementation does not introduce background layout work or cross-thread synchronization.

Custom controllers should publish poses on the UI thread. External physics or solver work may run elsewhere, but its results must be marshalled to Avalonia before changing a controller property or calling `SetCurrentPose`.

## Performance Characteristics

The design avoids several expensive or fragile techniques:

- no bitmap capture;
- no visual-tree cloning of targets;
- no per-frame reflection-based property dispatch;
- no background timer competing with rendering;
- no measuring of states unrelated to the current pose.

There is still real work on an active frame:

- required state templates are measured and arranged when layout is invalid;
- captured layouts and blended property maps are created;
- real targets are measured at their current sizes;
- animated styled-property values are published.

The cost therefore depends on the complexity of required templates, target subtrees, nesting depth, and number of animated properties. Keep state templates layout-focused, avoid duplicating expensive visual content inside them, and profile realistic scenes.

## Lifetime Summary

| Object | Lifetime |
| --- | --- |
| Real target | Stable while it remains in `MotionPanel.Children` |
| Item container | Stable for the corresponding target |
| State host | Stable while its `MotionState` remains in `States` |
| State template content | Stable until the template is replaced or state is removed |
| Controller attachment | From assignment to replacement/removal |
| Property output | While the evaluated pose supplies that property |

Next: [Limitations and Troubleshooting](limitations-and-troubleshooting.md)
