using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Hosts one live state template and captures its layout and property endpoints into a reusable buffer.
/// </summary>
internal sealed class MotionPanelHost : Decorator, IDisposable
{
    public MotionState State { get; }

    private readonly MotionPanel _owner;
    private readonly List<MotionSlot> _slots = [];
    private readonly Dictionary<MotionSlot, SlotEntry> _slotEntries = [];
    private readonly Dictionary<MotionSlot, int> _slotZIndices = [];
    private readonly HashSet<string> _slotNames = new(StringComparer.Ordinal);
    private readonly List<Visual> _paintOrderVisuals = [];
    private readonly List<MotionSlot> _orderedSlots = [];
    private readonly MotionLayoutBuffer _layoutBuffer;
    private bool _targetsDirty = true;
    private bool _slotOrderDirty = true;
    private bool _propertyValuesDirty = true;

    public MotionPanelHost(MotionPanel owner, MotionState state)
    {
        _owner = owner;
        State = state;
        _layoutBuffer = new MotionLayoutBuffer(owner.TargetCount);
        Opacity = 0;
        IsHitTestVisible = false;
        Focusable = false;
        Rebuild();
    }

    public void Rebuild()
    {
        UnsubscribeSlots();
        Child = State.Template?.Build(_owner.DataContext);
        SubscribeSlots();
        InvalidateMeasure();
    }

    public void RebindTargets()
    {
        if (!_targetsDirty)
        {
            return;
        }

        _slotEntries.Clear();
        _slotZIndices.Clear();
        _slotNames.Clear();

        foreach (var slot in _slots)
        {
            slot.ReleaseEndpointScope();

            var name = slot.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Every {nameof(MotionSlot)} in state '{State.Key}' must specify a non-empty x:Name.");
            }

            if (!_slotNames.Add(name))
            {
                throw new InvalidOperationException($"State '{State.Key}' contains more than one {nameof(MotionSlot)} named '{name}'.");
            }

            var target = _owner.ResolveTarget(name);
            slot.BindTarget(target);
            var entry = new SlotEntry(name, slot, target, CreateLayoutAncestorPath(slot));
            _slotEntries.Add(slot, entry);
        }

        _slotOrderDirty = true;
        _propertyValuesDirty = true;
        _layoutBuffer.ResetPropertyValues();
        _targetsDirty = false;
    }

    public void InvalidateTargets()
    {
        foreach (var slot in _slots)
        {
            slot.ReleaseEndpointScope();
        }

        _slotEntries.Clear();
        _slotZIndices.Clear();
        _slotNames.Clear();
        _slotOrderDirty = true;
        _propertyValuesDirty = true;
        _targetsDirty = true;
        _layoutBuffer.ResetPropertyValues();
    }

    public Size MeasureTarget(MotionSlot slot, Size availableSize)
    {
        if (!_slotEntries.TryGetValue(slot, out var entry))
        {
            throw new InvalidOperationException($"{nameof(MotionSlot)} '{slot.Name}' is not registered with its state host.");
        }

        entry.Target.Measure(availableSize);
        return entry.Target.DesiredSize;
    }

    public MotionLayoutBuffer CaptureLayout()
    {
        EnsureSlotOrder();
        if (_propertyValuesDirty)
        {
            RebuildPropertyValues();
        }

        _layoutBuffer.BeginLayoutUpdate(DesiredSize);
        foreach (var slot in _orderedSlots)
        {
            var entry = _slotEntries[slot];
            _layoutBuffer.EntriesBuffer.Add(entry.Name, new MotionLayoutEntry(GetLayoutBounds(entry), 1));
        }

        return _layoutBuffer;
    }

    public void Dispose() => UnsubscribeSlots();

    private void EnsureSlotOrder()
    {
        if (!_slotOrderDirty)
        {
            return;
        }

        _orderedSlots.Clear();
        _slotZIndices.Clear();
        if (Child is not null)
        {
            CollectSlotsInPaintOrder(Child, _orderedSlots);
        }

        _slotOrderDirty = false;
    }

    private void RebuildPropertyValues()
    {
        _layoutBuffer.BeginPropertyValuesUpdate();
        for (var i = 0; i < _orderedSlots.Count; i++)
        {
            AddSlotPropertyValues(_slotEntries[_orderedSlots[i]], i);
        }

        _propertyValuesDirty = false;
    }

    private void AddSlotPropertyValues(SlotEntry entry, int relativeZIndex)
    {
        foreach (var pair in entry.Slot.CaptureEndpointValues(entry.Target))
        {
            if (!_layoutBuffer.TryAddPropertyTarget(pair.Key, out var values))
            {
                throw new InvalidOperationException(
                    $"State '{State.Key}' defines motion properties for target '{pair.Key.Name ?? pair.Key.GetType().Name}' more than once.");
            }

            foreach (var property in pair.Value)
            {
                values.Add(property.Key, property.Value);
            }

            if (ReferenceEquals(pair.Key, entry.Target))
            {
                values[ZIndexProperty] = relativeZIndex;
            }
        }

        if (!_layoutBuffer.PropertyValues.ContainsKey(entry.Target))
        {
            _layoutBuffer.TryAddPropertyTarget(entry.Target, out var values);
            values.Add(ZIndexProperty, relativeZIndex);
        }
    }

    private void CollectSlotsInPaintOrder(Visual visual, List<MotionSlot> result)
    {
        if (visual is MotionSlot slot)
        {
            result.Add(slot);
            return;
        }

        foreach (var child in visual
                     .GetVisualChildren()
                     .Select((item, index) => new OrderedVisual(item, index))
                     .OrderBy(item => GetStateZIndex(item.Visual))
                     .ThenBy(item => item.Index))
        {
            CollectSlotsInPaintOrder(child.Visual, result);
        }
    }

    private int GetStateZIndex(Visual visual)
    {
        if (visual is not MotionSlot slot || !_slotEntries.TryGetValue(slot, out var entry))
        {
            return visual.ZIndex;
        }

        var zIndex = slot.GetStateZIndex(entry.Target);
        _slotZIndices[slot] = zIndex;
        return zIndex;
    }

    private Rect GetLayoutBounds(SlotEntry entry)
    {
        var position = entry.Slot.Bounds.Position;
        foreach (var ancestor in entry.LayoutAncestors)
        {
            position += (Vector)ancestor.Bounds.Position;
        }

        return new Rect(position, entry.Slot.Bounds.Size);
    }

    private Visual[] CreateLayoutAncestorPath(MotionSlot slot)
    {
        var result = new List<Visual>();
        var current = slot.GetVisualParent();
        while (current is not null && current != this)
        {
            result.Add(current);
            current = current.GetVisualParent();
        }

        if (current != this)
        {
            throw new InvalidOperationException(
                $"{nameof(MotionSlot)} '{slot.Name}' is not a descendant of its state host.");
        }

        return [.. result];
    }

    private void SubscribeSlots()
    {
        if (Child is MotionSlot rootSlot)
        {
            SubscribeSlot(rootSlot);
        }

        if (Child is not null)
        {
            SubscribePaintOrderVisual(Child);
            foreach (var visual in Child.GetVisualDescendants())
            {
                SubscribePaintOrderVisual(visual);
                if (visual is MotionSlot slot)
                {
                    SubscribeSlot(slot);
                }
            }
        }
    }

    private void SubscribeSlot(MotionSlot slot)
    {
        slot.AttachHost(this);
        _slots.Add(slot);
        slot.EndpointChanged += OnSlotEndpointChanged;
    }

    private void SubscribePaintOrderVisual(Visual visual)
    {
        _paintOrderVisuals.Add(visual);
        visual.PropertyChanged += OnPaintOrderVisualPropertyChanged;
    }

    private void UnsubscribeSlots()
    {
        foreach (var slot in _slots)
        {
            slot.EndpointChanged -= OnSlotEndpointChanged;
            slot.DetachHost(this);
        }

        foreach (var visual in _paintOrderVisuals)
        {
            visual.PropertyChanged -= OnPaintOrderVisualPropertyChanged;
        }

        _slots.Clear();
        _paintOrderVisuals.Clear();
        _slotEntries.Clear();
        _slotZIndices.Clear();
        _slotNames.Clear();
        _orderedSlots.Clear();
        _layoutBuffer.Clear();
        _targetsDirty = true;
        _slotOrderDirty = true;
        _propertyValuesDirty = true;
    }

    private void OnSlotEndpointChanged(object? sender, EventArgs e)
    {
        _propertyValuesDirty = true;

        if (sender is MotionSlot slot &&
            _slotEntries.TryGetValue(slot, out var entry) &&
            (!_slotZIndices.TryGetValue(slot, out var previousZIndex) || previousZIndex != slot.GetStateZIndex(entry.Target)))
        {
            _slotOrderDirty = true;
        }

        _owner.NotifyStateEndpointChanged(State, this);
    }

    private void OnPaintOrderVisualPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ZIndexProperty || sender is MotionSlot)
        {
            return;
        }

        _slotOrderDirty = true;
        _propertyValuesDirty = true;
        _owner.NotifyStateEndpointChanged(State, this);
    }

    private readonly record struct SlotEntry(
        string Name,
        MotionSlot Slot,
        Control Target,
        Visual[] LayoutAncestors
    );

    private readonly record struct OrderedVisual(Visual Visual, int Index);
}

/// <summary>
/// Contains one evaluated state's panel layout and real-element property endpoints.
/// </summary>
/// <remarks>
/// Instances own reusable dictionaries. Callers may retain a buffer until the owning host or
/// panel evaluates it again, but must not treat its read-only views as immutable collections.
/// </remarks>
internal sealed class MotionLayoutBuffer(int targetCapacity = 0)
{
    public Size DesiredSize { get; private set; }

    public IReadOnlyDictionary<string, MotionLayoutEntry> Entries => _entries;

    public IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>> PropertyValues =>
        _propertyValues;

    internal Dictionary<string, MotionLayoutEntry> EntriesBuffer => _entries;

    private readonly Dictionary<string, MotionLayoutEntry> _entries = new(targetCapacity, StringComparer.Ordinal);
    private readonly Dictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>> _propertyValues = [];
    private readonly Dictionary<StyledElement, Dictionary<AvaloniaProperty, object?>> _propertyValueBuffers = [];
    private readonly List<Dictionary<AvaloniaProperty, object?>> _activePropertyValueBuffers = [];

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

    internal bool TryAddPropertyTarget(StyledElement target, out Dictionary<AvaloniaProperty, object?> values)
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
internal readonly record struct MotionLayoutEntry(
    Rect Bounds,
    double Presence
);