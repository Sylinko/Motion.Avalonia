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

Motion.Avalonia lets stable Avalonia controls move between unrelated live layouts. You author ordinary panel trees with
`MotionSlot` placeholders, then choose a motion model that fits the input:

- `StateMotion` transitions between complete named states;
- `SequenceMotion` samples sparse keyframes from normalized progress;
- `TimelineMotion` samples sparse keyframes at absolute times and provides playback.

The framework performs real `Measure` and `Arrange` work. Text reflows at intermediate widths, bindings remain live,
and resizing immediately changes the endpoints. It does not render controls to bitmaps or use scale transforms to
imitate layout.

## Features

- Stable target control identity across every layout
- Live, responsive `DataTemplate` layout sources
- Real intermediate measure and arrange
- Smooth interruption and retargeting for named states
- Seekable progress and absolute-time keyframes
- Sparse per-property tracks
- Layout and styled properties on one interpolation pipeline
- Directed state-transition timing overrides
- Strongly typed custom interpolators
- Reuse of Avalonia's built-in `Animator<T>` implementations
- Nested target styling through Avalonia selector syntax
- Relative Z-order evaluation
- Animation-priority bindings that restore underlying values when motion ends
- No bitmap snapshots or transform-based pseudo-layout

## Quick Start

Install the package:

```shell
dotnet add package Motion.Avalonia
```

Define a `StateMotion` and keep the real controls as direct children:

```xml
<UserControl
  xmlns="https://github.com/avaloniaui"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
  xmlns:motion="using:Motion.Avalonia">

  <motion:MotionPanel>
    <motion:MotionPanel.Motion>
      <motion:StateMotion
        x:Name="EditorMotion"
        State="Quick"
        Duration="0:0:0.35"
        Easing="CubicEaseInOut">

        <motion:MotionState Key="Quick">
          <DataTemplate>
            <Grid>
              <motion:MotionSlot
                x:Name="Editor"
                MaxWidth="600"
                HorizontalAlignment="Center"/>
            </Grid>
          </DataTemplate>
        </motion:MotionState>

        <motion:MotionState Key="Advanced">
          <DataTemplate>
            <DockPanel HorizontalSpacing="24">
              <motion:MotionSlot
                x:Name="Editor"
                MaxWidth="420"
                DockPanel.Dock="Left"/>
              <motion:MotionSlot x:Name="Preview"/>
            </DockPanel>
          </DataTemplate>
        </motion:MotionState>
      </motion:StateMotion>
    </motion:MotionPanel.Motion>

    <Border x:Name="Editor"/>
    <Border x:Name="Preview"/>
  </motion:MotionPanel>
</UserControl>
```

Change `EditorMotion.State` to `"Advanced"` to begin the transition.

Three rules connect templates to real controls:

1. Every target is a stable direct child of `MotionPanel`.
2. Every target has a unique, non-empty `x:Name`.
3. A `MotionSlot` uses the same `x:Name` as the target it represents.

Continue with [Getting Started](docs/getting-started.md) for a complete example.

## Choose a Motion

### Named application states

Use `StateMotion` when the application selects complete layouts such as compact/expanded, quick/advanced, or
editing/review. Missing slots are absent from that state.

### Scroll, slider, or gesture progress

Use `SequenceMotion` when another component owns normalized progress:

```xml
<motion:SequenceMotion Progress="{Binding #ProgressSlider.Value}">
  <motion:SequenceMotionKeyFrame Cue="0%">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" HorizontalAlignment="Left"/>
    </DataTemplate>
  </motion:SequenceMotionKeyFrame>
  <motion:SequenceMotionKeyFrame Cue="100%" Easing="CubicEaseOut">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" HorizontalAlignment="Right"/>
    </DataTemplate>
  </motion:SequenceMotionKeyFrame>
</motion:SequenceMotion>
```

Keyframes are sparse: an omitted property receives no key at that Cue.

### Authored time and playback

Use `TimelineMotion` for absolute `KeyTime` values. Bind or assign `Time` to scrub, or call
`TimelineMotion.Player.Play()` for frame-aligned playback. `Loop` controls wrapping and `Duration` is derived from the
latest keyframe.

## How It Works

```text
MotionPanel targets + live layout sources
                  │
                  ▼
         Motion.Compile(context)
                  │
                  ▼
     Long-lived per-property tracks
                  │
                  ▼
Easing → MotionInterpolator → Animation-priority binding
                  │
                  ▼
      Stable containers and real controls
```

Compiling a motion discovers its layout and property channels once. Progress or time changes update the existing tracks;
they do not rebuild the target controls. `MotionPanel` remains responsible for measuring live layout sources and
supplying their current bounds to layout tracks.

## Documentation

| Chapter | Description |
|---|---|
| [Getting Started](docs/getting-started.md) | Build a complete two-mode editor transition |
| [Core Concepts](docs/core-concepts.md) | Learn targets, layout sources, channels, and presence |
| [States and Slots](docs/states-and-slots.md) | Author responsive complete-state layouts |
| [Motions and Keyframes](docs/motions-and-keyframes.md) | Choose State, Sequence, or Timeline motion |
| [Target Properties](docs/target-properties.md) | Use shortcuts, setters, bindings, and target styles |
| [Interpolators](docs/interpolators.md) | Customize timing and value-space interpolation |
| [Recipes](docs/recipes.md) | Apply the primitives to common UI patterns |
| [Architecture](docs/architecture.md) | Understand Apply, tracks, layout frames, and lifetime |
| [Limitations and Troubleshooting](docs/limitations-and-troubleshooting.md) | Diagnose scope and configuration errors |
| [API Reference](docs/api-reference.md) | Look up public types and properties |

## Current Scope

Motion.Avalonia currently targets stable direct children of one `MotionPanel`. Cross-window, cross-page, and
cross-ancestor shared-element transitions are outside the current implementation. Direct Avalonia properties cannot be
animated. The API is an early preview and may change as physics, interaction, and higher-level animation systems are
added.

## Projects

- `Motion.Avalonia` — reusable motion, layout, player, and interpolation primitives
- `Motion.Avalonia.Demo` — shared cross-platform showcase UI
- `Motion.Avalonia.Demo.Desktop` — Windows, macOS, and Linux host
- `Motion.Avalonia.Demo.Android` — Android host
- `Motion.Avalonia.Demo.iOS` — iPhone and iPad host
- `Motion.Avalonia.Demo.Browser` — WebAssembly host
- `Motion.Avalonia.Tests` — behavior and lifecycle tests

Run the desktop showcase:

```shell
dotnet run --project src/Motion.Avalonia.Demo.Desktop
```

## Contributing

Issues, design discussions, examples, and pull requests are welcome. Describe the user-facing motion problem as well as
the proposed implementation.

## License

Motion.Avalonia is distributed under the [MIT License](LICENSE).
