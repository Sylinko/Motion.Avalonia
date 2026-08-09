using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Hosts one live motion layout source and captures its layout and property endpoints into one
/// reusable frame.
/// </summary>
internal sealed class MotionPanelHost : Decorator, IMotionLayout
{
    public event EventHandler? EndpointChanged;

    public event EventHandler? EndpointStructureChanged;

    private readonly MotionItemsPanel _owner;
    private readonly string _description;
    private readonly List<MotionSlot> _slots = [];
    private readonly Dictionary<MotionSlot, SlotEntry> _slotEntries = [];
    private readonly Dictionary<MotionSlot, int> _slotZIndices = [];
    private readonly HashSet<string> _slotNames = new(StringComparer.Ordinal);
    private readonly List<Visual> _paintOrderVisuals = [];
    private readonly List<MotionSlot> _orderedSlots = [];
    private readonly MotionLayoutFrame _layoutFrame;
    private bool _targetsDirty = true;
    private bool _slotOrderDirty = true;
    private bool _propertyValuesDirty = true;
    private IDisposable? _attachment;
    private bool _disposed;
    private long _layoutFrameVersion = 1;
    private long _capturedLayoutFrameVersion;

    private MotionPanelHost(MotionCompilationContext context, IDataTemplate? template, string description)
    {
        _owner = context.Panel;
        _description = description;
        _layoutFrame = new MotionLayoutFrame(context.TargetCount);
        Opacity = 0;
        IsHitTestVisible = false;
        Focusable = false;
        Build(template, context.DataContext);
    }

    internal static IMotionLayout Create(MotionCompilationContext context, IDataTemplate? template, string description)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var result = new MotionPanelHost(context, template, description);
        try
        {
            result._attachment = context.AttachLayoutRoot(result);
            result.RebindTargets();
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private void Build(IDataTemplate? template, object? dataContext)
    {
        UnsubscribeSlots();
        Child = template?.Build(dataContext);
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

            var name = Motion.GetEffectiveName(slot);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    $"Every {nameof(MotionSlot)} in {_description} must specify a non-empty {nameof(Motion)} identity " +
                    $"({nameof(Motion.NameProperty)} or x:Name).");
            }

            if (!_slotNames.Add(name))
            {
                throw new InvalidOperationException(
                    $"{_description} contains more than one {nameof(MotionSlot)} with Motion identity '{name}'.");
            }

            var target = _owner.ResolveTarget(name);
            slot.BindTarget(target);
            var entry = new SlotEntry(name, slot, target, CreateLayoutAncestorPath(slot));
            _slotEntries.Add(slot, entry);
        }

        _slotOrderDirty = true;
        _propertyValuesDirty = true;
        InvalidateLayoutFrame();
        _layoutFrame.ResetPropertyValues();
        _targetsDirty = false;
    }

    public Size MeasureTarget(MotionSlot slot, Size availableSize)
    {
        if (!_slotEntries.TryGetValue(slot, out var entry))
        {
            throw new InvalidOperationException(
                $"{nameof(MotionSlot)} '{Motion.GetEffectiveName(slot) ?? slot.Name}' is not registered with its layout host.");
        }

        entry.Target.Measure(availableSize);
        return entry.Target.DesiredSize;
    }

    /// <summary>
    /// Gets the current reusable layout frame, capturing the host only when its local frame is dirty.
    /// </summary>
    /// <remarks>
    /// The returned frame remains live and is reused by this host. Marking the frame dirty does not
    /// publish an event; the next reader performs the capture on the UI thread. This keeps frame
    /// freshness pull-based and prevents every track from recapturing the same layout.
    /// </remarks>
    public MotionLayoutFrame CaptureLayout()
    {
        var captureVersion = _layoutFrameVersion;
        if (_capturedLayoutFrameVersion == captureVersion)
        {
            return _layoutFrame;
        }

        EnsureSlotOrder();
        if (_propertyValuesDirty)
        {
            RebuildPropertyValues();
        }

        _layoutFrame.BeginLayoutUpdate(DesiredSize);
        foreach (var slot in _orderedSlots)
        {
            var entry = _slotEntries[slot];
            _layoutFrame.AddLayoutEntry(
                entry.Name,
                new MotionLayoutEntry(
                    GetLayoutBounds(entry),
                    Motion.GetAuthoredPresence(slot),
                    Motion.GetSlotMetadata(slot, Motion.PositionProperty),
                    Motion.GetSlotMetadata(slot, Motion.SizeProperty),
                    Motion.GetSlotMetadata(slot, Motion.PresenceProperty)));
        }

        _capturedLayoutFrameVersion = captureVersion;
        return _layoutFrame;
    }

    void IMotionLayout.Arrange(Rect finalRect)
    {
        InvalidateLayoutFrame();
        Arrange(finalRect);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnsubscribeSlots();
        _attachment?.Dispose();
        _attachment = null;
    }

    /// <summary>
    /// Invalidates the reusable frame without capturing it immediately.
    /// </summary>
    private void InvalidateLayoutFrame() => _layoutFrameVersion++;

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
        _layoutFrame.BeginPropertyValuesUpdate();
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
            if (!_layoutFrame.TryAddPropertyTarget(pair.Key, out var values))
            {
                throw new InvalidOperationException(
                    $"{_description} defines motion properties for target '{pair.Key.Name ?? pair.Key.GetType().Name}' more than once.");
            }

            foreach (var property in pair.Value)
            {
                values.Add(property.Key, property.Value);
            }

            if (ReferenceEquals(pair.Key, entry.Target))
            {
                var metadata = values.TryGetValue(ZIndexProperty, out var endpoint) ?
                    endpoint.Metadata :
                    Motion.GetSlotMetadata(entry.Slot, ZIndexProperty);
                values[ZIndexProperty] = new MotionEndpointValue(relativeZIndex, metadata);
            }
        }

        if (!_layoutFrame.PropertyValues.ContainsKey(entry.Target))
        {
            _layoutFrame.TryAddPropertyTarget(entry.Target, out var values);
            values.Add(
                ZIndexProperty,
                new MotionEndpointValue(
                    relativeZIndex,
                    Motion.GetSlotMetadata(entry.Slot, ZIndexProperty)));
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
                $"{nameof(MotionSlot)} '{Motion.GetEffectiveName(slot) ?? slot.Name}' is not a descendant of its layout host.");
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
        slot.EndpointStructureChanged += OnSlotEndpointStructureChanged;
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
            slot.EndpointStructureChanged -= OnSlotEndpointStructureChanged;
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
        _layoutFrame.Clear();
        _targetsDirty = true;
        _slotOrderDirty = true;
        _propertyValuesDirty = true;
        InvalidateLayoutFrame();
    }

    private void OnSlotEndpointChanged(object? sender, EventArgs e)
    {
        _propertyValuesDirty = true;
        InvalidateLayoutFrame();

        if (sender is MotionSlot slot &&
            _slotEntries.TryGetValue(slot, out var entry) &&
            (!_slotZIndices.TryGetValue(slot, out var previousZIndex) || previousZIndex != slot.GetStateZIndex(entry.Target)))
        {
            _slotOrderDirty = true;
        }

        EndpointChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSlotEndpointStructureChanged(object? sender, EventArgs e)
    {
        _propertyValuesDirty = true;
        _slotOrderDirty = true;
        InvalidateLayoutFrame();
        EndpointStructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPaintOrderVisualPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ZIndexProperty || sender is MotionSlot)
        {
            return;
        }

        _slotOrderDirty = true;
        _propertyValuesDirty = true;
        InvalidateLayoutFrame();
        EndpointChanged?.Invoke(this, EventArgs.Empty);
    }

    private readonly record struct SlotEntry(
        string Name,
        MotionSlot Slot,
        Control Target,
        Visual[] LayoutAncestors
    );

    private readonly record struct OrderedVisual(Visual Visual, int Index);
}