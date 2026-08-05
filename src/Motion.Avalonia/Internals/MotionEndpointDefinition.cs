using Avalonia;
using Avalonia.Styling;

namespace Motion.Avalonia;

/// <summary>
/// Describes one original target property value selected by a motion style.
/// </summary>
/// <remarks>
/// Instances are stored as values of safe shadow properties. A binding remains a definition
/// until the winning style is known, then it is evaluated against the real target.
/// </remarks>
internal sealed record MotionEndpointDefinition(
    AvaloniaProperty Property,
    object? Value,
    MotionInterpolationMetadata Metadata = default
) : ISetterValue
{
    public void Initialize(SetterBase setter)
    {
        if (Value is ISetterValue value)
        {
            value.Initialize(setter);
        }
    }
}