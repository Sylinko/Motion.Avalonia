# Motion.Avalonia

Declarative, seekable, real-layout motion for Avalonia (`net8.0`, `net10.0`, `net11.0` / Avalonia `12.0.0+`).

`Motion.Avalonia` lets stable Avalonia controls move smoothly between unrelated live layouts (`StateMotion`, `SequenceMotion`, `TimelineMotion`) using real `Measure` and `Arrange` passes—without bitmap snapshots or scale transforms to imitate layout.

## Install

```shell
dotnet add package Motion.Avalonia
```

## Quick Start

Declare the XML namespace in AXAML:

```xml
xmlns:motion="using:Motion.Avalonia"
```

Keep target controls as direct children of `MotionPanel` with matching `x:Name` values on each `MotionSlot`:

```xml
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
```

Switch `EditorMotion.State` to `"Advanced"` to trigger a smooth transition with interruption and retargeting support.

## Motion Models

- **`StateMotion`**: Named state transitions with directed `MotionEdge` timing/easing overrides and smooth mid-flight interruption.
- **`SequenceMotion`**: Scrubbable normalized `Progress` (`0..1` / `0%..100%` `Cue` keyframes) driven by `ScrollViewer`, `Slider`, or gestures.
- **`TimelineMotion`**: Authored absolute `KeyTime` keyframes with a frame-aligned `Player` (`Play`, `Pause`, `Loop`, scrubbing `Time`).

## Documentation & License

- [Full Documentation & Guides (GitHub)](https://github.com/Sylinko/Motion.Avalonia)
- [Getting Started](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/getting-started.md)
- [API Reference](https://github.com/Sylinko/Motion.Avalonia/blob/main/docs/api-reference.md)
- [MIT License](https://github.com/Sylinko/Motion.Avalonia/blob/main/LICENSE)
