# Getting Started

[Documentation index](README.md) · [Project README](../README.md)

This tutorial moves an editor between a centered quick layout and a two-column advanced layout. The controls remain
the same instances throughout the transition.

## 1. Add MotionPanel

```xml
<motion:MotionPanel>
  <Border x:Name="Editor"/>
  <Border x:Name="Preview"/>
</motion:MotionPanel>
```

Targets must be direct children with unique, non-empty names. Their order in XAML does not determine their animated
position; the active layout source does.

## 2. Add StateMotion

`StateMotion` owns both the requested state and the complete state definitions:

```xml
<motion:MotionPanel>
  <motion:MotionPanel.Motion>
    <motion:StateMotion
      x:Name="EditorMotion"
      State="Quick"
      Duration="0:0:0.35"
      Easing="CubicEaseInOut">
      <!-- states go here -->
    </motion:StateMotion>
  </motion:MotionPanel.Motion>

  <Border x:Name="Editor"/>
  <Border x:Name="Preview"/>
</motion:MotionPanel>
```

`Duration="0"` applies changes immediately. A null or omitted `Easing` is linear.

## 3. Author the Quick state

```xml
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
```

The template is live. If the panel resizes, the centered destination changes immediately. `Preview` is omitted, so it
is absent from this complete state.

## 4. Author the Advanced state

```xml
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
```

Only endpoint constraints such as `MaxWidth` are fixed. The actual position is calculated by `DockPanel`; there is no
hard-coded animated margin.

## 5. Switch state

```csharp
EditorMotion.State =
    Equals(EditorMotion.State, "Quick")
        ? "Advanced"
        : "Quick";
```

The first state presented by a newly attached motion is immediate. Later changes use `Duration` and `Easing`. If a
transition is interrupted, the next transition begins from the exact result currently on screen.

## 6. Add target properties

Layout properties on `MotionSlot` arrange the slot itself. Selected visual properties are shortcuts for the real
target:

```xml
<motion:MotionSlot
  x:Name="Preview"
  Opacity="0.85"
  RenderTransform="translate(0px, 8px)"/>
```

Use `Setter` for other styled properties:

```xml
<motion:MotionSlot
  x:Name="Editor"
  x:SetterTargetType="Border">
  <Setter Property="CornerRadius" Value="16"/>
  <Setter Property="Background" Value="#FF203B56"/>
</motion:MotionSlot>
```

The values are applied at Avalonia animation priority. Removing or replacing the motion restores the target's
underlying local, style, or binding value.

## 7. Customize one direction

```xml
<motion:StateMotion
  State="Quick"
  Duration="0:0:0.35"
  Easing="CubicEaseInOut">
  <motion:StateMotion.Edges>
    <motion:MotionEdge
      From="Quick"
      To="Advanced"
      Duration="0:0:0.55"
      Easing="CubicEaseOut"/>
    <motion:MotionEdge
      From="Advanced"
      To="Quick"
      Duration="0:0:0.2"
      Easing="CubicEaseIn"/>
  </motion:StateMotion.Edges>

  <!-- MotionState definitions -->
</motion:StateMotion>
```

Edges are directed. An omitted edge value inherits the `StateMotion` default.

## Complete Example

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

Next:

- [Core Concepts](core-concepts.md)
- [States and Slots](states-and-slots.md)
- [Motions and Keyframes](motions-and-keyframes.md)
