using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Hosts one live state template and captures its layout and property endpoint snapshot.
/// </summary>
internal sealed class MotionPanelHost : Decorator, IDisposable
{
    public MotionState State { get; }

    private readonly MotionPanel _owner;
    private readonly List<MotionSlot> _slots = [];

    public MotionPanelHost(MotionPanel owner, MotionState state)
    {
        _owner = owner;
        State = state;
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

    public Size MeasureTarget(string? target, Size availableSize) =>
        _owner.MeasureTarget(target, availableSize);

    public MotionLayoutSnapshot CaptureLayout()
    {
        var orderedSlots = new List<MotionSlot>(_owner.TargetCount);
        if (Child is not null)
        {
            CollectSlotsInPaintOrder(Child, orderedSlots);
        }

        var entries = new Dictionary<string, MotionLayoutEntry>(_owner.TargetCount, StringComparer.Ordinal);
        var propertyValues = new Dictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>>();
        for (var i = 0; i < orderedSlots.Count; i++)
        {
            AddSlot(orderedSlots[i], i, entries, propertyValues);
        }

        return new MotionLayoutSnapshot(DesiredSize, entries, propertyValues);
    }

    public void Dispose() => UnsubscribeSlots();

    private void AddSlot(
        MotionSlot slot,
        int relativeZIndex,
        Dictionary<string, MotionLayoutEntry> entries,
        Dictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>> propertyValues)
    {
        var target = slot.Target;
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException($"Every {nameof(MotionSlot)} in state '{State.Key}' must specify a non-empty target.");
        }

        if (!entries.TryAdd(target, new MotionLayoutEntry(GetLayoutBounds(slot), 1)))
        {
            throw new InvalidOperationException($"State '{State.Key}' contains more than one slot for transition target '{target}'.");
        }

        var targetControl = _owner.ResolveTarget(target);
        foreach (var pair in slot.CaptureEndpointValues(targetControl))
        {
            var values = new Dictionary<AvaloniaProperty, object?>(pair.Value);
            if (ReferenceEquals(pair.Key, targetControl))
            {
                values[ZIndexProperty] = relativeZIndex;
            }

            if (!propertyValues.TryAdd(pair.Key, values))
            {
                throw new InvalidOperationException(
                    $"State '{State.Key}' defines motion properties for target '{pair.Key.Name ?? pair.Key.GetType().Name}' more than once.");
            }
        }

        if (!propertyValues.ContainsKey(targetControl))
        {
            propertyValues.Add(
                targetControl,
                new Dictionary<AvaloniaProperty, object?>
                {
                    [ZIndexProperty] = relativeZIndex,
                });
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
        if (visual is not MotionSlot slot || string.IsNullOrWhiteSpace(slot.Target))
        {
            return visual.ZIndex;
        }

        return slot.GetStateZIndex(_owner.ResolveTarget(slot.Target));
    }

    private Rect GetLayoutBounds(MotionSlot slot)
    {
        var position = slot.Bounds.Position;
        var current = slot.GetVisualParent();

        while (current is not null && current != this)
        {
            position += (Vector)current.Bounds.Position;
            current = current.GetVisualParent();
        }

        if (current != this)
        {
            throw new InvalidOperationException($"Motion slot '{slot.Target}' is not a descendant of its state scene.");
        }

        return new Rect(position, slot.Bounds.Size);
    }

    private void SubscribeSlots()
    {
        if (Child is MotionSlot rootSlot)
        {
            SubscribeSlot(rootSlot);
        }

        if (Child is null)
        {
            return;
        }

        foreach (var slot in Child.GetVisualDescendants().OfType<MotionSlot>())
        {
            SubscribeSlot(slot);
        }
    }

    private void SubscribeSlot(MotionSlot slot)
    {
        _slots.Add(slot);
        slot.EndpointChanged += OnSlotEndpointChanged;
    }

    private void UnsubscribeSlots()
    {
        foreach (var slot in _slots)
        {
            slot.EndpointChanged -= OnSlotEndpointChanged;
            slot.ReleaseEndpointScope();
        }

        _slots.Clear();
    }

    private void OnSlotEndpointChanged(object? sender, EventArgs e) =>
        _owner.NotifyStateEndpointChanged(State, this);

    private readonly record struct OrderedVisual(Visual Visual, int Index);
}

/// <summary>
/// Contains one evaluated state's panel layout and real-element property endpoints.
/// </summary>
internal sealed record MotionLayoutSnapshot(
    Size DesiredSize,
    IReadOnlyDictionary<string, MotionLayoutEntry> Entries,
    IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>> PropertyValues
);

/// <summary>
/// Contains the layout channels associated with one named direct panel child.
/// </summary>
internal readonly record struct MotionLayoutEntry(
    Rect Bounds,
    double Presence
);