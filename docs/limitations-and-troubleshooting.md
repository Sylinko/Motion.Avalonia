# Limitations and Troubleshooting

[Documentation index](README.md)

## Current Scope

- Targets must be stable direct children of one `MotionPanel`.
- Cross-window, cross-page, and cross-ancestor shared-element transitions are not implemented.
- Motion performs real layout; it does not provide bitmap snapshot transitions.
- Direct Avalonia properties cannot receive animation-priority bindings.
- External custom `Motion` implementations are not yet a supported extension surface; use the built-in motions and
  custom interpolators.
- All mutation and playback APIs are UI-thread-affine.

## “No direct child has that name”

Every referenced `MotionSlot.Name` must match a direct target:

```xml
<motion:MotionSlot x:Name="Editor"/>
...
<Border x:Name="Editor"/>
```

Check spelling, nesting, and whether the target is accidentally inside another control.

## Empty or Duplicate Names

Every direct target and every slot in one layout source must have a unique non-empty name. Duplicate state keys are also
rejected.

## A State Target Disappears

That is complete-state semantics: a target omitted from a `MotionState` has zero presence. Add the slot when the target
should remain in layout.

## A Keyframe Omission Does Nothing

That is sparse-track semantics. A property omitted from a keyframe has no key there. Author the property explicitly if
the channel should reach a value at that Cue or KeyTime.

## A Property Changes Discretely

The default interpolator could not find a compatible Avalonia animator for the declared property. Supply a strongly
typed `MotionInterpolator<T>` through `MotionInterpolation`.

Boolean, cursor, focus, and similar semantic values are naturally discrete unless a custom policy says otherwise.

## Setter Property Cannot Be Resolved

Specify `x:SetterTargetType`:

```xml
<motion:MotionSlot
  x:Name="Card"
  x:SetterTargetType="Border">
  <Setter Property="CornerRadius" Value="16"/>
</motion:MotionSlot>
```

## A Direct Property Throws

Motion supports styled and attached properties. Move the effect to a styled property, an attached property, or a
purpose-built target control API.

## TargetStyles Reject a Selector

TargetStyles currently reject:

- property-value selectors such as `[IsChecked=True]`;
- `/template/` traversal;
- non-native `IStyle` entries;
- non-native setter types;
- Avalonia transitions and animations.

Use type, name, class, child, and descendant selectors over the target logical subtree.

## A Binding Is Rejected

`OneWayToSource` has no endpoint value and is unsupported. A custom binding type must expose an Avalonia observable
binding expression. Use a standard reflection, compiled, template, or multi-binding when possible.

## Underlying Values Are Lost

They should not be. Motion binds at animation priority and tracks base-value changes. Verify that application code is
not calling `ClearValue` or replacing the target itself. Removing `MotionPanel.Motion` should reveal the latest
underlying value.

## Runtime Shortcut Set to Its Default Is Not Detected

Avalonia does not raise a public effective-value `PropertyChanged` event when an unset property is explicitly assigned
the same value as its default. AXAML initialization is unaffected because Apply scans `IsSet`. Runtime authoring that
depends on the `unset → explicitly default` distinction requires a future value-store-level observation mechanism.

## Motion Does Not Advance

- `SequenceMotion` never advances itself; update or bind `Progress`.
- `TimelineMotion` advances only while `Player.State` is `Playing`.
- `MotionPlayer` must be attached to a rooted visual. `PlayerDrivenMotion` handles this automatically.
- A detached visual intentionally suspends animation-frame requests.

## Full Solution Does Not Build

Mobile host projects require the corresponding .NET workloads. To validate the reusable library and desktop demo
without iOS or Android workloads:

```shell
dotnet build src/Motion.Avalonia/Motion.Avalonia.csproj
dotnet build src/Motion.Avalonia.Demo.Desktop/Motion.Avalonia.Demo.Desktop.csproj
dotnet test tests/Motion.Avalonia.Tests/Motion.Avalonia.Tests.csproj
```
