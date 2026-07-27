<div align="center">

# Motion.Avalonia

**Declarative, seekable, real-layout motion for Avalonia**

<p>
  <img alt=".NET 8, 9, and 10" src="https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4">
  <img alt="Avalonia 12" src="https://img.shields.io/badge/Avalonia-12-8B44AC">
  <img alt="Status: early preview" src="https://img.shields.io/badge/status-early%20preview-orange">
  <a href="https://github.com/Sylinko/Motion.Avalonia/blob/main/LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-blue"></a>
</p>

</div>

Motion.Avalonia is a motion and interaction framework for Avalonia applications. It lets you describe several live
layout states with ordinary Avalonia panels, keep the real controls alive, and move between those states with a
controller-provided pose.

The framework performs real `Measure` and `Arrange` work at intermediate sizes. Text reflows, nested controls lay out at
their current dimensions, bindings remain live, and a resize immediately changes the motion endpoints. Motion.Avalonia
does not render controls to bitmaps and does not use scale transforms to imitate layout.

## Why Motion.Avalonia?

Avalonia transitions are excellent when a control already has a property value to animate. More involved UI motion
often needs another level of description:

- the same control moves between unrelated panel layouts;
- several controls share one seekable progress value;
- state changes can be driven by time, scrolling, a slider, or a gesture;
- layout and target properties must stay on the same timeline;
- an interrupted motion must continue from the current visual pose.

Motion.Avalonia separates those concerns. `MotionState` describes endpoints, `MotionController` produces a pose, and
`MotionInterpolator` decides how individual values are interpolated.

## Features

- Live, persistent layout state templates
- Stable target control identity across every state
- Real intermediate measure and arrange
- Controller-driven and seekable motion
- Built-in normalized state sequences
- Absolute-time keyframe timelines with controller-owned playback
- Layout and styled-property interpolation on one timeline
- Directed `MotionEdge` timing overrides
- Strongly typed custom interpolators
- Reuse of Avalonia's built-in `Animator<T>` implementations
- Smooth interruption and retargeting
- Relative Z-order evaluation across nested state layouts
- Live bindings in state templates and setters
- Responsive endpoints when the panel size changes
- No bitmap snapshots or transform-based pseudo-layout

## Quick Start

Install the package:

```shell
dotnet add package Motion.Avalonia
```

Add the namespace and define stable controls, a controller, and two state templates:

```xml
<UserControl
  xmlns="https://github.com/avaloniaui"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
  xmlns:motion="using:Motion.Avalonia">

  <motion:MotionPanel>
    <motion:MotionPanel.Controller>
      <motion:BasicMotionController
        State="Quick"
        Duration="0:0:0.35"
        Easing="CubicEaseInOut"/>
    </motion:MotionPanel.Controller>

    <motion:MotionPanel.States>
      <motion:MotionState Key="Quick">
        <motion:MotionState.Template>
          <DataTemplate>
            <Grid>
              <motion:MotionSlot
                Target="Editor"
                MaxWidth="600"
                HorizontalAlignment="Center"/>
            </Grid>
          </DataTemplate>
        </motion:MotionState.Template>
      </motion:MotionState>

      <motion:MotionState Key="Advanced">
        <motion:MotionState.Template>
          <DataTemplate>
            <DockPanel HorizontalSpacing="24">
              <motion:MotionSlot
                Target="Editor"
                MaxWidth="420"
                DockPanel.Dock="Left"/>
              <motion:MotionSlot Target="Preview"/>
            </DockPanel>
          </DataTemplate>
        </motion:MotionState.Template>
      </motion:MotionState>
    </motion:MotionPanel.States>

    <Border x:Name="Editor"/>
    <Border x:Name="Preview"/>
  </motion:MotionPanel>
</UserControl>
```

Three rules define the relationship:

1. Real controls are stable direct children of `MotionPanel`.
2. Every real target has a unique `x:Name`.
3. Each `MotionSlot.Target` maps a state layout slot to that name.

Set `BasicMotionController.State` to `"Advanced"` to start the motion. Continue with
[Getting Started](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/getting-started.md) for a complete, step-by-step example.

## How It Works

```text
MotionController
      │
      ▼
  MotionPose
      │
      ▼
Required live MotionState templates
      │
      ▼
Layout and property endpoints
      │
      ▼
MotionInterpolator
      │
      ▼
Stable containers and real controls
```

State templates remain connected to the logical and visual trees. Only the states required by the current pose are
measured and arranged. Their resolved bounds, presence, relative Z-order, shortcut properties, and explicit setters are
then evaluated into the real controls.

## Documentation

| Chapter | Description |
|---|---|
| [Getting Started](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/getting-started.md) | Build a complete two-mode editor motion |
| [Core Concepts](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/core-concepts.md) | Learn the mental model behind states, poses, and stable targets |
| [States and Slots](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/states-and-slots.md) | Design live layout endpoints with ordinary Avalonia panels |
| [Controllers and Poses](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/controllers-and-poses.md) | Use time, progress, scrolling, or custom inputs |
| [Target Properties](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/target-properties.md) | Use shortcuts, setters, bindings, and relative Z-order |
| [Interpolators](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/interpolators.md) | Understand default resolution and write typed interpolators |
| [Recipes](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/recipes.md) | Apply the framework to common UI motion patterns |
| [Architecture](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/architecture.md) | Explore lifetime, layout, output channels, and performance |
| [Limitations and Troubleshooting](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/limitations-and-troubleshooting.md) | Understand current scope and diagnose configuration errors |
| [API Reference](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/api-reference.md) | Find the purpose and contract of each public type |

The [documentation index](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/README.md) also provides task-oriented links.

## Current Scope

Motion.Avalonia currently targets stable direct children of one `MotionPanel`. Cross-window, cross-page, and
cross-ancestor shared-element transitions are outside the current implementation. Direct Avalonia properties cannot be
animated. The public API is an early preview and may change while controllers, interpolation, physics, and interaction
components continue to evolve.

## Roadmap

The project is intended to grow beyond layout morphing. Planned areas include richer player behaviors, physics-based
motion, pointer and hover interactions, event and property tracks, richer interpolation policies, and higher-level
animation-state-machine components.

## Projects

- `Motion.Avalonia` — reusable controls, controllers, players, poses, and interpolators
- `Motion.Avalonia.Demo` — interactive desktop showcase
- `Motion.Avalonia.Tests` — automated behavior and lifecycle tests

## Contributing

Issues, design discussions, examples, and pull requests are welcome. Because the API is still being shaped, please
describe the user-facing motion problem as well as the proposed implementation.

## License

Motion.Avalonia is distributed under the [MIT License](https://github.com/Sylinko/Motion.Avalonia/blob/main/LICENSE).
