using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Maps original Avalonia properties to lazily registered, type-safe shadow properties.
/// </summary>
/// <remarks>
/// A shadow property is shared by all style scopes. This keeps property registration bounded by
/// the number of animated property kinds rather than the number of states or controls.
/// </remarks>
internal static class MotionShadowPropertyRegistry
{
    private static readonly Dictionary<AvaloniaProperty, StyledProperty<MotionEndpointDefinition?>> Properties = [];
    private static readonly Dictionary<AvaloniaProperty, AvaloniaProperty> Sources = [];
    private static int _nextId;

    public static StyledProperty<MotionEndpointDefinition?> GetOrCreate(AvaloniaProperty source)
    {
        if (Properties.TryGetValue(source, out var result))
        {
            return result;
        }

#pragma warning disable AVP1001
        // Endpoint properties are intentionally registered lazily so custom Avalonia properties
        // remain first-class motion channels without reflection or a closed property catalog.
        result = AvaloniaProperty.RegisterAttached<MotionStyleProxy, MotionEndpointDefinition?>(
            $"Endpoint{_nextId++}_{source.Name}",
            typeof(MotionShadowPropertyRegistry));
#pragma warning restore AVP1001

        Properties.Add(source, result);
        Sources.Add(result, source);
        return result;
    }

    public static bool TryGetSource(AvaloniaProperty property, out AvaloniaProperty source) =>
        Sources.TryGetValue(property, out source!);
}