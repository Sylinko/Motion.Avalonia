# Getting Started

[← Documentation](README.md) · [Next: Core Concepts →](core-concepts.md)

This tutorial builds a two-mode editor. In Quick mode, the editor is centered with a maximum width of 600. In Advanced
mode, the editor moves left, becomes narrower, and reveals a preview on the right. The same controls survive the entire
motion.

## Prerequisites

- An Avalonia 12 application
- .NET 8, .NET 9, or .NET 10
- Basic familiarity with Avalonia controls, data templates, and bindings

## 1. Install Motion.Avalonia

```shell
dotnet add package Motion.Avalonia
```

Add the namespace to the view that owns the panel:

```xml
xmlns:motion="using:Motion.Avalonia"
```

Motion.Avalonia does not require a theme include or application-level service registration.

## 2. Create Stable Target Controls

Start with the real editor and preview controls:

```xml
<motion:MotionPanel x:Name="EditorMotion">
  <Border
    x:Name="Editor"
    Padding="24"
    Background="#FF203B56"
    CornerRadius="16">
    <TextBox
      AcceptsReturn="True"
      Text="Write a prompt..."/>
  </Border>

  <Border
    x:Name="Preview"
    Padding="24"
    Background="#FF2D2F36"
    CornerRadius="16">
    <TextBlock Text="Preview"/>
  </Border>
</motion:MotionPanel>
```

These controls are the actual interactive UI. They are not copied into state templates and are not recreated when the
mode changes. Their text, focus, selection, validation, subscriptions, and local state remain attached to the same
instances.

Every direct child must have a non-empty, unique `x:Name`.

## 3. Add a BasicMotionController

Add a controller before the content controls:

```xml
<motion:MotionPanel.Controller>
  <motion:BasicMotionController
    x:Name="EditorController"
    State="Quick"
    Duration="0:0:0.4"
    Easing="CubicEaseInOut"/>
</motion:MotionPanel.Controller>
```

`BasicMotionController` owns the requested state and the clock used to move to it. `Duration` and `Easing` are defaults
for every state change unless a matching directed edge overrides them.

## 4. Define the Quick State

Add a state collection:

```xml
<motion:MotionPanel.States>
  <motion:MotionState Key="Quick">
    <motion:MotionState.Template>
      <DataTemplate>
        <Grid>
          <motion:MotionSlot
            Target="Editor"
            MaxWidth="600"
            HorizontalAlignment="Center"
            VerticalAlignment="Stretch"/>
        </Grid>
      </DataTemplate>
    </motion:MotionState.Template>
  </motion:MotionState>
</motion:MotionPanel.States>
```

The `Grid` is a real, persistent layout tree for this state. `MotionSlot.Target="Editor"` asks the panel to measure the
real `Editor` control and place its resolved bounds at the slot's position.

Properties such as `MaxWidth`, `HorizontalAlignment`, `Margin`, and attached Grid or DockPanel properties configure the
slot's layout. They are not copied to the real control.

## 5. Define the Advanced State

Add a second state to the same collection:

```xml
<motion:MotionState Key="Advanced">
  <motion:MotionState.Template>
    <DataTemplate>
      <Grid ColumnDefinitions="420,*" ColumnSpacing="24">
        <motion:MotionSlot
          Target="Editor"
          MaxWidth="420"
          HorizontalAlignment="Stretch"/>

        <motion:MotionSlot
          Grid.Column="1"
          Target="Preview"/>
      </Grid>
    </DataTemplate>
  </motion:MotionState.Template>
</motion:MotionState>
```

The `Editor` slot is now in a different layout tree and the `Preview` slot exists only in this state. During the motion,
the editor receives real intermediate bounds. Preview presence moves from zero to one.

## 6. Switch Between States

Set the controller state from code:

```csharp
EditorController.State =
    Equals(EditorController.State, "Quick")
        ? "Advanced"
        : "Quick";
```

Or bind it:

```xml
<motion:BasicMotionController
  State="{Binding EditorMode}"
  Duration="0:0:0.4"
  Easing="CubicEaseInOut"/>
```

State keys are compared as objects. If `EditorMode` is an enum or Boolean, use matching values for `MotionState.Key`
rather than relying on string conversion.

## 7. Add Target Property Motion

A slot can also describe selected properties of the real target. Some common `Control` properties have shortcut syntax:

```xml
<motion:MotionSlot
  Target="Preview"
  Opacity="0.85"
  ClipToBounds="True"/>
```

Other styled properties use setters. `x:SetterTargetType` gives the XAML compiler and IDE the target type:

```xml
<motion:MotionSlot
  x:SetterTargetType="Border"
  Target="Editor"
  MaxWidth="420">
  <Setter Property="CornerRadius" Value="8"/>
  <Setter Property="BorderThickness" Value="2"/>
  <Setter Property="BorderBrush" Value="#FF7B61FF"/>
</motion:MotionSlot>
```

Layout and target properties use the same pose progress. There is no second property-animation clock.

## 8. Customize a Directed Edge

Edges are directional. Add them as children of `BasicMotionController`:

```xml
<motion:BasicMotionController
  x:Name="EditorController"
  State="Quick"
  Duration="0:0:0.4"
  Easing="CubicEaseInOut">
  <motion:MotionEdge
    From="Quick"
    To="Advanced"
    Duration="0:0:0.7"
    Easing="CubicEaseOut"/>
  <motion:MotionEdge
    From="Advanced"
    To="Quick"
    Duration="0:0:0.22"
    Easing="SineEaseOut"/>
</motion:BasicMotionController>
```

Routes without a matching edge use the controller defaults. Defining more than one edge for the same `From` and `To`
pair is a configuration error.

## 9. Try Responsive and Interruptible Motion

Resize the window while the controller is between the two states. Both state templates are laid out again at the new
panel size, and the current frame is evaluated from those new endpoints.

Switch back before the first transition finishes. `BasicMotionController` uses its current pose as the source of the
next blend, so the control does not jump back to a previously settled state.

## Complete Example

```xml
<motion:MotionPanel x:Name="EditorMotion">
  <motion:MotionPanel.Controller>
    <motion:BasicMotionController
      x:Name="EditorController"
      State="Quick"
      Duration="0:0:0.4"
      Easing="CubicEaseInOut">
      <motion:MotionEdge
        From="Advanced"
        To="Quick"
        Duration="0:0:0.22"
        Easing="SineEaseOut"/>
    </motion:BasicMotionController>
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
          <Grid ColumnDefinitions="420,*" ColumnSpacing="24">
            <motion:MotionSlot
              x:SetterTargetType="Border"
              Target="Editor"
              MaxWidth="420">
              <Setter Property="CornerRadius" Value="8"/>
            </motion:MotionSlot>
            <motion:MotionSlot
              Grid.Column="1"
              Target="Preview"/>
          </Grid>
        </DataTemplate>
      </motion:MotionState.Template>
    </motion:MotionState>
  </motion:MotionPanel.States>

  <Border
    x:Name="Editor"
    Padding="24"
    Background="#FF203B56"
    CornerRadius="16">
    <TextBox
      AcceptsReturn="True"
      Text="Write a prompt..."/>
  </Border>

  <Border
    x:Name="Preview"
    Padding="24"
    Background="#FF2D2F36"
    CornerRadius="16">
    <TextBlock Text="Preview"/>
  </Border>
</motion:MotionPanel>
```

## What to Read Next

- Read [Core Concepts](core-concepts.md) for the mental model behind this example.
- Read [Target Properties](target-properties.md) before adding more shortcuts or setters.
- Read [Controllers and Poses](controllers-and-poses.md) to replace time with slider or scroll progress.

---

[← Documentation](README.md) · [Next: Core Concepts →](core-concepts.md)
