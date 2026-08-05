# Recipes

[Documentation index](README.md)

## Responsive Two-mode Editor

Use `StateMotion`, center the quick editor with a live panel, and use a `DockPanel` for advanced mode. Omit preview from
the quick state so Presence handles entry and exit.

See the complete example in [Getting Started](getting-started.md).

## Scroll-driven Product Story

Normalize scroll offset and assign it to `SequenceMotion.Progress`:

```csharp
var scrollable = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
motion.Progress = scrollable == 0
    ? 0
    : Math.Clamp(viewer.Offset.Y / scrollable, 0, 1);
```

Author `SequenceMotionKeyFrame` entries at meaningful reveal points. Because tracks are sparse, title opacity, image layout,
and call-to-action properties can use different Cue coordinates in the same sequence.

## Slider Scrubbing

```xml
<Slider
  x:Name="ProgressSlider"
  Minimum="0"
  Maximum="1"/>

<motion:SequenceMotion
  Progress="{Binding #ProgressSlider.Value}">
  <!-- keyframes -->
</motion:SequenceMotion>
```

No player or code-behind is required.

## Playable Timeline

```xml
<motion:TimelineMotion
  x:Name="Timeline"
  Loop="{Binding #LoopCheckBox.IsChecked}">
  <!-- TimelineMotionKeyFrame entries -->
</motion:TimelineMotion>

<Button Command="{Binding #Timeline.Player.Play}"/>
<Button Command="{Binding #Timeline.Player.Pause}"/>
```

Bind a slider to `Timeline.Time` with a `TimeSpan` converter to add scrubbing.

## Preserve Form State

Keep the `TextBox`, `ComboBox`, and validation controls as MotionPanel targets. Put only `MotionSlot` proxies in
templates. Use slot setters to change the surrounding border's background or corner radius.

The form controls remain the same instances, so their current values and focus state survive layout changes.

## Animate Nested Details

```xml
<motion:MotionSlot x:Name="Card">
  <motion:MotionSlot.TargetStyles>
    <Style Selector="Border#Card TextBlock.details">
      <Setter Property="Opacity" Value="1"/>
    </Style>
  </motion:MotionSlot.TargetStyles>
</motion:MotionSlot>
```

Author a different value in another state or keyframe. The descendant property receives its own track.

## Different Open and Close Timing

Use directed edges:

```xml
<motion:StateMotion.Edges>
  <motion:MotionEdge
    From="Closed" To="Open"
    Duration="0:0:0.45"
    Easing="CubicEaseOut"/>
  <motion:MotionEdge
    From="Open" To="Closed"
    Duration="0:0:0.18"
    Easing="CubicEaseIn"/>
</motion:StateMotion.Edges>
```

## Curved Layout Motion

Apply `CubicBezierMotionInterpolator` only to `Motion.Position`. Size, presence, transform, and target properties retain
their normal interpolators.

## Reuse a Layout Template

Store a `DataTemplate` in resources and assign it to multiple distinct keyframes. Do not reuse the keyframe instance
itself.

```xml
<DataTemplate x:Key="RestLayout">
  <motion:MotionSlot x:Name="Card"/>
</DataTemplate>

<motion:TimelineMotionKeyFrame
  KeyTime="0:0:0"
  Template="{StaticResource RestLayout}"/>
<motion:TimelineMotionKeyFrame
  KeyTime="0:0:4"
  Template="{StaticResource RestLayout}"/>
```
