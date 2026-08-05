using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Contains one evaluated layout source's panel layout and real-element property endpoints.
/// </summary>
/// <remarks>
/// Instances own reusable dictionaries. Callers may retain a frame until the owning host captures
/// it again, but must not treat its read-only views as immutable collections.
/// </remarks>
public sealed class MotionLayoutFrame
{
    /// <summary>
    /// Gets the desired size measured for the represented layout source.
    /// </summary>
    public Size DesiredSize { get; private set; }

    /// <summary>
    /// Gets the captured layout channels keyed by effective Motion identity.
    /// </summary>
    public IReadOnlyDictionary<string, MotionLayoutEntry> Entries => _entries;

    /// <summary>
    /// Gets captured property endpoints keyed by their resolved target elements.
    /// </summary>
    public IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, MotionEndpointValue>> PropertyValues => _propertyValues;

    internal Dictionary<string, MotionLayoutEntry> EntriesBuffer => _entries;

    private readonly Dictionary<string, MotionLayoutEntry> _entries;
    private readonly Dictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, MotionEndpointValue>> _propertyValues = [];
    private readonly Dictionary<StyledElement, Dictionary<AvaloniaProperty, MotionEndpointValue>> _propertyValueBuffers = [];
    private readonly List<Dictionary<AvaloniaProperty, MotionEndpointValue>> _activePropertyValueBuffers = [];

    internal MotionLayoutFrame(int targetCapacity = 0)
    {
        _entries = new Dictionary<string, MotionLayoutEntry>(targetCapacity, StringComparer.Ordinal);
    }

    internal void BeginLayoutUpdate(Size desiredSize)
    {
        DesiredSize = desiredSize;
        _entries.Clear();
    }

    internal void BeginPropertyValuesUpdate()
    {
        foreach (var values in _activePropertyValueBuffers)
        {
            values.Clear();
        }

        _activePropertyValueBuffers.Clear();
        _propertyValues.Clear();
    }

    internal bool TryAddPropertyTarget(StyledElement target, out Dictionary<AvaloniaProperty, MotionEndpointValue> values)
    {
        if (_propertyValues.ContainsKey(target))
        {
            values = null!;
            return false;
        }

        if (!_propertyValueBuffers.TryGetValue(target, out values!))
        {
            values = [];
            _propertyValueBuffers.Add(target, values);
        }

        values.Clear();
        _propertyValues.Add(target, values);
        _activePropertyValueBuffers.Add(values);
        return true;
    }

    internal Dictionary<AvaloniaProperty, MotionEndpointValue> GetOrAddPropertyTarget(StyledElement target)
    {
        if (_propertyValues.TryGetValue(target, out var existing))
        {
            return (Dictionary<AvaloniaProperty, MotionEndpointValue>)existing;
        }

        TryAddPropertyTarget(target, out var values);
        return values;
    }

    internal void ResetPropertyValues()
    {
        BeginPropertyValuesUpdate();
        _propertyValueBuffers.Clear();
        _activePropertyValueBuffers.Clear();
    }

    internal void Clear()
    {
        DesiredSize = default;
        _entries.Clear();
        ResetPropertyValues();
    }
}

/// <summary>
/// Contains the layout channels associated with one named direct panel child.
/// </summary>
public readonly record struct MotionLayoutEntry(
    Rect Bounds,
    double Presence,
    MotionInterpolationMetadata PositionMetadata = default,
    MotionInterpolationMetadata SizeMetadata = default,
    MotionInterpolationMetadata PresenceMetadata = default
);