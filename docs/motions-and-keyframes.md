# Motions and Keyframes

[Documentation index](README.md)

`MotionPanel.Motion` selects how authored layout sources are evaluated. The built-in motions cover discrete
application states, externally owned normalized progress, and absolute time.

## StateMotion

Use `StateMotion` for complete named states:

```xml
<motion:StateMotion
  State="{Binding EditorMode}"
  Duration="0:0:0.3"
  Easing="CubicEaseInOut">
  <motion:MotionState Key="Compact">
    <DataTemplate>
      <motion:MotionSlot x:Name="Editor" MaxWidth="600"/>
    </DataTemplate>
  </motion:MotionState>
  <motion:MotionState Key="Expanded">
    <DataTemplate>
      <motion:MotionSlot x:Name="Editor"/>
    </DataTemplate>
  </motion:MotionState>
</motion:StateMotion>
```

The initial presentation is immediate. Subsequent changes animate when `Duration` is positive. Interrupted transitions
continue from the current nested blend rather than restarting from an old state.

### Directed Edges

```xml
<motion:StateMotion.Edges>
  <motion:MotionEdge
    From="Compact"
    To="Expanded"
    Duration="0:0:0.5"
    Easing="CubicEaseOut"/>
  <motion:MotionEdge
    From="Expanded"
    To="Compact"
    Duration="0:0:0.18"
    Easing="CubicEaseIn"/>
</motion:StateMotion.Edges>
```

Edges are directional. `Duration="{x:Null}"` inherits the motion duration. An omitted easing inherits the motion
easing; an explicitly null easing resolves to linear.

## SequenceMotion

`SequenceMotion.Progress` is normalized to zero through one. The motion has no player and is designed for bindings:

```xml
<motion:SequenceMotion Progress="{Binding #ProgressSlider.Value}">
  <motion:SequenceMotionKeyFrame Cue="0%">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" Opacity="0"/>
    </DataTemplate>
  </motion:SequenceMotionKeyFrame>
  <motion:SequenceMotionKeyFrame Cue="45%" Easing="CubicEaseOut">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" Opacity="1"/>
    </DataTemplate>
  </motion:SequenceMotionKeyFrame>
  <motion:SequenceMotionKeyFrame Cue="100%">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" HorizontalAlignment="Right"/>
    </DataTemplate>
  </motion:SequenceMotionKeyFrame>
</motion:SequenceMotion>
```

Each property has its own sparse track. In this example, opacity completes at 45%, while position continues to 100%.
A track with one key holds that value for the entire sequence. At duplicate Cue positions, the later declaration wins
for channels it defines.

Bind `Progress` to:

- a `Slider.Value`;
- normalized `ScrollViewer.Offset`;
- gesture displacement;
- media progress;
- a reverse solver or custom interaction.

## TimelineMotion

`TimelineMotion` uses absolute `KeyTime` values:

```xml
<motion:TimelineMotion
  x:Name="IntroMotion"
  Loop="True">
  <motion:TimelineMotionKeyFrame KeyTime="0:0:0">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" Opacity="0"/>
    </DataTemplate>
  </motion:TimelineMotionKeyFrame>
  <motion:TimelineMotionKeyFrame
    KeyTime="0:0:0.8"
    Easing="CubicEaseOut">
    <DataTemplate>
      <motion:MotionSlot x:Name="Card" Opacity="1"/>
    </DataTemplate>
  </motion:TimelineMotionKeyFrame>
</motion:TimelineMotion>
```

`Duration` is read-only and derived from the latest keyframe. `Time` can be assigned or bound for scrubbing. The
inherited `Player` exposes `Play`, `Pause`, `Reset`, and `Stop`.

Internally, Timeline normalizes `Time` and every `KeyTime` against `Duration`. Sequence and
Timeline therefore compile to the same zero-to-one Cue axis even though Timeline retains its
absolute-time authoring API.

```csharp
IntroMotion.Player.Play();
IntroMotion.Player.Pause();
IntroMotion.Time = TimeSpan.FromMilliseconds(400);
```

`Stop` pauses and resets player time. `TimelineMotion.Time` is the authored timeline position; player time is only the
frame-aligned clock used to advance it.

## Destination Keyframe Easing

Easing belongs to the segment arriving at a keyframe. Resolution is:

1. property endpoint metadata;
2. destination keyframe easing;
3. motion easing;
4. linear.

Setting a keyframe's `Easing` explicitly to null resets the incoming segment to linear instead of inheriting the motion.

## State or Keyframe?

Use `StateMotion` when omission means absence. Use `SequenceMotion` or `TimelineMotion` when omission means “this
channel has no key here”.

Do not add a player to `SequenceMotion` merely to make it automatic. When time owns the coordinate, use
`TimelineMotion`; when an external value owns it, use `SequenceMotion`.

---

[Next: Target Properties →](target-properties.md)
