using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Contains one evaluated layout source's panel layout and real-element property endpoints.
/// </summary>
/// <remarks>
/// Instances own reusable dictionaries. Callers may retain a frame until the owning layout captures
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

    private readonly Dictionary<string, MotionLayoutEntry> _entries;
    private readonly Dictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, MotionEndpointValue>> _propertyValues = [];
    private readonly Dictionary<StyledElement, Dictionary<AvaloniaProperty, MotionEndpointValue>> _propertyValueBuffers = [];
    private readonly List<Dictionary<AvaloniaProperty, MotionEndpointValue>> _activePropertyValueBuffers = [];

    /// <summary>
    /// Initializes a reusable layout frame.
    /// </summary>
    /// <param name="targetCapacity">The expected number of named layout targets.</param>
    public MotionLayoutFrame(int targetCapacity = 0)
    {
        _entries = new Dictionary<string, MotionLayoutEntry>(targetCapacity, StringComparer.Ordinal);
    }

    /// <summary>
    /// Starts a layout update while retaining the frame's reusable storage.
    /// </summary>
    /// <param name="desiredSize">The desired size produced by the current measure pass.</param>
    public void BeginLayoutUpdate(Size desiredSize)
    {
        DesiredSize = desiredSize;
        _entries.Clear();
    }

    /// <summary>
    /// Adds one named layout endpoint to the current layout update.
    /// </summary>
    /// <param name="name">The effective Motion identity of the target.</param>
    /// <param name="entry">The evaluated layout endpoint.</param>
    public void AddLayoutEntry(string name, MotionLayoutEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_entries.TryAdd(name, entry))
        {
            throw new InvalidOperationException($"The motion layout frame already contains target '{name}'.");
        }
    }

    /// <summary>
    /// Starts a target-property update while retaining the frame's reusable storage.
    /// </summary>
    public void BeginPropertyValuesUpdate()
    {
        foreach (var values in _activePropertyValueBuffers)
        {
            values.Clear();
        }

        _activePropertyValueBuffers.Clear();
        _propertyValues.Clear();
    }

    /// <summary>
    /// Adds one target-property endpoint to the current property update.
    /// </summary>
    /// <param name="target">The real target element.</param>
    /// <param name="property">The target property represented by the endpoint.</param>
    /// <param name="value">The endpoint value and interpolation metadata.</param>
    public void AddPropertyValue(
        StyledElement target,
        AvaloniaProperty property,
        MotionEndpointValue value)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);

        var values = GetOrAddPropertyTarget(target);
        if (!values.TryAdd(property, value))
        {
            throw new InvalidOperationException(
                $"The motion layout frame already contains property '{property}' for target " +
                $"'{target.Name ?? target.GetType().Name}'.");
        }
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

    /// <summary>
    /// Clears every layout and property endpoint retained by this frame.
    /// </summary>
    public void Clear()
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