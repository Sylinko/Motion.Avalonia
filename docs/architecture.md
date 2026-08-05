# Architecture

[Documentation index](README.md)

Motion.Avalonia separates authoring, live layout, evaluation, interpolation, and property application.

## Ownership

`MotionPanel` owns:

- stable direct targets and their containers;
- hidden hosts for live layout sources;
- the current `MotionRuntime` and its disposal.

A `Motion` owns:

- state or keyframe authoring data;
- the current evaluation coordinate;
- motion-level interpolation policy;
- an optional `MotionPlayer`.

Layout source types own their `DataTemplate`; `MotionSlot` owns endpoint definitions for one named target.

## Initialization Boundary

AXAML collection and property assignment order is not a valid topology boundary. MotionPanel waits until
`OnInitialized` before the first complete compilation. Changes before initialization only make the topology dirty.

After initialization, collections remain dynamic. A later structure change recompiles the affected graph during
the next layout pass.

## Compile Phase

Each motion implements the public `Compile` operation:

```text
Motion + MotionPanel topology
           │
           ▼
  MotionCompilationContext
           │
           ▼
      MotionRuntime
           │
           ▼
one MotionTrack<T> per discovered channel
```

Compilation resolves target destinations, builds interpolation lookup tables, discovers channels, and creates strongly
typed tracks. If compilation fails partway through, already-created bindings and subscriptions are disposed before the
exception escapes. Custom motions use the same public context, runtime, and track types as the built-ins.
Motion-level interpolation entries are copied into the runtime's lookup table; later changes to their collection or
properties do not affect the existing runtime.

After MotionPanel stores the complete runtime, it activates the runtime. Activation connects
runtime publishers only after every track and binding exists. Observable sources may therefore
publish their current value synchronously during subscription without observing a partially built
graph.

`StateMotion` compiles the union of complete-state channels into state-specific tracks.
`SequenceMotion` and `TimelineMotion` compile independent sparse channels directly into the same
`KeyFrameMotionTrack<T>` type. Each keyframe track owns its typed `Keys`; every
`KeyFrameMotionTrack<T>.Key` retains its source keyframe and latest live endpoint value.
Both motions implement `IKeyFrameMotion`: the interface publishes normalized progress and maps
each authored keyframe to an Avalonia `Cue`. Timeline converts absolute `Time` and `KeyTime`
against its current `Duration` during this boundary.

## Track Binding

Every track owns one `IObservable<T>` binding at `BindingPriority.Animation`. The track:

- reads the destination's base value;
- watches lower-priority value changes;
- evaluates its current value when requested;
- skips publishing equal consecutive values;
- restores the current base value when disposed.

The runtime's private type bridge is cached by the property's declared type. The specific destination
property still identifies the channel, while `MotionTrack<T>` retains the resolved Motion-level
interpolation fallback for its lifetime.

## Live Layout Frames

Every state or keyframe has a persistent hidden `MotionPanelHost`. A host builds the source template, measures its slots
against real targets, and captures one reusable `MotionLayoutFrame` containing:

- source desired size;
- target position, size, presence, and endpoint metadata;
- target and nested-target property endpoints.

The frame owns reusable dictionaries. Capturing updates them in place; it is not an immutable snapshot and contains no
bitmap data.

Only layout sources required by the current state blend or active sparse track segments are measured and arranged.

## Layout Cycle

During measure:

1. ensure the current `MotionRuntime`;
2. collect required layout sources;
3. measure their live hosts;
4. publish panel desired-size tracks;
5. measure target containers at their evaluated sizes.

During arrange:

1. arrange and capture required hosts at the final panel size;
2. publish all tracks against the current frames;
3. arrange target containers at `Motion.Position` and `Motion.Size`;
4. notify the runtime that the result was presented.

This is why resizing a settled state remains responsive.

## Runtime Publication

State, progress, time, player, and live endpoint changes reuse the runtime graph. Sequence and
Timeline publish current progress directly through `IKeyFrameMotion`; their runtime owns
that subscription and forwards each value to its keyframe tracks. `MotionRuntime` publishes each
track directly into its animation-priority binding. Avalonia then decides whether the changed
destination property invalidates measure, arrange, render, or none of them.

Motion does not attempt to classify arbitrary Avalonia properties as layout-affecting. Avalonia controls can register
their own property-change handlers, so the framework itself is the source of truth.

`MotionPanel` coordinates only values whose evaluation depends on a live layout template. If a sparse segment first
references a source that has not yet been measured and arranged for the current panel size, the panel queues layout,
captures that endpoint, and republishes the existing tracks. Pure property changes do not enter layout merely because
they belong to a motion.

## State Evaluation

StateMotion keeps an internal recursive state composition only to preserve interruption:

```text
State A
   ╲
    Blend(A, B, 0.42)
                  ╲
                   Blend(previous result, C, new progress)
```

The composition is an implementation detail, not the public authoring model. Each state track
evaluates the same recursive blend and applies endpoint, edge, and motion interpolation policy.

## Sparse Track Evaluation

Sequence and Timeline compile one ordered key list per channel. Segment lookup is binary. A property omitted from an
intermediate keyframe is not visited during evaluation because it was never inserted into that channel's track.

Duplicate cues replace the previous key for channels authored by the later declaration.

## TargetStyles

Target styles use Avalonia's selector and AXAML compilation syntax without applying setters to arbitrary proxy objects.
Motion creates an isolated shadow tree that mirrors the real target logical subtree and registers shadow properties for
the source Avalonia properties.

This avoids assigning properties such as `IsEnabledProperty` to an incompatible generic proxy. Effective shadow setter
values are captured as endpoint definitions and later applied to the real registered destination.

Style endpoint values are cached until a binding changes. Value changes refresh the reusable frame and publish the
existing tracks; channel additions or removals invalidate the compiled topology.

## Invalidation Levels

- **Evaluation change**: progress, time, player tick, or active state blend changed. Publish existing tracks directly.
- **Endpoint value change**: a live binding changed. Refresh the frame and publish existing tracks.
- **Endpoint topology change**: a shortcut, setter, target style, or target subtree changed. Recompile tracks.
- **Template change**: rebuild only that layout host, then recompile.
- **Target collection or motion change**: dispose the complete runtime graph and rebind topology.

## Lifetime

Motion is a logical child of MotionPanel. Compiled tracks are disposed when the motion changes or the panel leaves the
visual tree. PlayerDrivenMotion attaches its player to a rooted visual through an `IDisposable` lease. Visual-tree
detachment suspends frame requests and excludes detached time without destroying the player state.

## Per-frame Work

The steady path performs:

- a required-source readiness check;
- binary segment lookup for sparse tracks;
- easing and interpolation;
- equality-filtered observer publication;
- Avalonia property invalidation when a published value actually changed.

Live template measure, arrange, capture, and target layout run only when Avalonia or a newly required layout source
invalidates them.

It does not recreate bindings, scan all interpolators, rebuild style proxy trees, or allocate final layout snapshots
each frame.
