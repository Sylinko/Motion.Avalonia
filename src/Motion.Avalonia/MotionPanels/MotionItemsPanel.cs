using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;

namespace Motion.Avalonia;

/// <summary>
/// Projects live motion templates onto the stable children of an owning <see cref="MotionPanel"/>.
/// </summary>
/// <remarks>
/// Every direct child must have a unique non-empty effective Motion identity. <see cref="Motion.NameProperty"/>
/// takes precedence over <c>x:Name</c>; slots in states or keyframes map to those children by the
/// same identity. Template trees remain live and responsive; the panel never captures bitmaps or
/// substitutes transform-only scaling for real layout.
///
/// The panel is used as an <see cref="ItemsControl.ItemsPanel"/>. Every target is rendered
/// through one <see cref="MotionItemContainer"/> envelope: the panel creates it for already
/// materialized direct children, while an items control can provide the same container as its item
/// container. Position, size, and presence remain attached to the materialized target child;
/// the envelope supplies the visual boundary, composed presence opacity, and paint order.
/// </remarks>
public sealed class MotionItemsPanel : Panel
{
    internal int TargetCount => _targets.Count;

    internal MotionPanel? Owner => TemplatedParent as MotionPanel;

    private Motion? CurrentMotion => Owner?.Motion;

    private readonly Dictionary<string, TargetEntry> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<Control, MotionItemContainer> _containers = [];
    private readonly List<IMotionLayoutSource> _requiredLayoutSources = [];
    private readonly HashSet<IMotionLayoutSource> _requiredLayoutSourceSet = [];
    private MotionRuntime? _motionRuntime;
    private bool _targetMapDirty = true;
    private bool _isMotionDirty = true;
    private bool _ensuringMotionRuntime;

    static MotionItemsPanel()
    {
        Motion.DesiredSizeProperty.Changed.AddClassHandler<MotionItemsPanel>(static (panel, _) => panel.InvalidateHostMeasure());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionItemsPanel"/> class.
    /// </summary>
    public MotionItemsPanel() { }

    internal Control ResolveTarget(string name)
    {
        RebuildTargetMap();
        if (!_targets.TryGetValue(name, out var target))
        {
            throw new InvalidOperationException($"Motion layout source references target '{name}', but no direct child has that Motion identity.");
        }

        return target.Child;
    }

    internal IDisposable AttachLayoutRoot(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        LogicalChildren.Add(root);

        try
        {
            VisualChildren.Add(root);
            return new LayoutRootAttachment(this, root);
        }
        catch
        {
            LogicalChildren.Remove(root);
            throw;
        }
    }

    private void DetachLayoutRoot(Control root)
    {
        VisualChildren.Remove(root);
        LogicalChildren.Remove(root);
    }

    internal void InvalidateMotion(Motion motion)
    {
        if (!ReferenceEquals(CurrentMotion, motion))
        {
            return;
        }

        InvalidateMotion();
    }

    internal void InvalidateMotion()
    {
        _isMotionDirty = true;
        InvalidateHostMeasure();
    }

    /// <summary>
    /// Invalidates identity-dependent target and layout mappings after an attached
    /// <see cref="Motion.NameProperty"/> changes somewhere in this logical tree.
    /// </summary>
    /// <remarks>
    /// A direct child identity invalidates the target dictionary. Descendant identities still
    /// invalidate the runtime because a replacement layout may resolve a different property
    /// target. The replacement is deferred until the next measure pass.
    /// </remarks>
    internal void InvalidateMotionIdentity(StyledElement source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is Control child && IsTargetIdentitySource(child))
        {
            _targetMapDirty = true;
        }

        _isMotionDirty = true;
        InvalidateHostMeasure();
    }

    private bool IsTargetIdentitySource(Control source)
    {
        if (Children.Contains(source))
        {
            return true;
        }

        return source.VisualParent is MotionItemContainer container && Children.Contains(container);
    }

    internal AvaloniaObject ResolveTrackDestination(MotionChannel channel)
    {
        if (channel.Property == Motion.DesiredSizeProperty)
        {
            return this;
        }

        if (channel.LayoutTarget is { } layoutTarget)
        {
            return channel.Property == ZIndexProperty ?
                ResolveTargetEntry(layoutTarget).Container :
                ResolveTargetEntry(layoutTarget).Child;
        }

        var propertyTarget = channel.PropertyTarget ?? throw new InvalidOperationException($"MotionChannel '{channel.Property}' has no destination.");
        if (channel.Property == ZIndexProperty && propertyTarget is Control control && _containers.TryGetValue(control, out var container))
        {
            return container;
        }

        return propertyTarget;
    }

    internal void EnsureRequiredLayoutSources(MotionCompilationContext context)
    {
        if (_motionRuntime is not { } motionRuntime || !ReferenceEquals(motionRuntime.CompilationContext, context))
        {
            return;
        }

        CollectRequiredLayoutSources();
        if (!context.AreLayoutSourcesReady(_requiredLayoutSources))
        {
            InvalidateHostMeasure();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InvalidateHostMeasure();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMotionReady();
        CollectRequiredLayoutSources();
        MeasureRequiredLayoutSources(availableSize);
        _motionRuntime?.PublishMeasureTracks();

        foreach (var target in _targets.Values)
        {
            var size = target.Child.GetValue(Motion.SizeProperty);
            target.Container.Measure(size);
        }

        return GetValue(Motion.DesiredSizeProperty);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureMotionReady();
        CaptureRequiredLayouts(finalSize);
        _motionRuntime?.Publish();

        foreach (var target in _targets.Values)
        {
            var container = target.Container;
            var size = target.Child.GetValue(Motion.SizeProperty);
            container.Measure(size);
            container.Arrange(new Rect(target.Child.GetValue(Motion.PositionProperty), size));
        }

        _motionRuntime?.Presented();
        return finalSize;
    }

    private void WrapNewChildren(IList items)
    {
        foreach (var child in items.OfType<Control>())
        {
            if (child is MotionItemContainer || _containers.ContainsKey(child))
            {
                continue;
            }

            var visualIndex = VisualChildren.IndexOf(child);
            if (visualIndex < 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(MotionItemsPanel)} could not locate a newly added child in its visual children.");
            }

            var container = new MotionItemContainer();
            VisualChildren.RemoveAt(visualIndex);
            container.AttachDirectChild(child);
            _containers.Add(child, container);
            VisualChildren.Insert(visualIndex, container);
        }
    }

    private void UnwrapRemovedChildren(IList items)
    {
        foreach (var child in items.OfType<Control>())
        {
            if (!_containers.Remove(child, out var container))
            {
                continue;
            }

            VisualChildren.Remove(container);
            container.DetachDirectChild();
        }
    }

    /// <inheritdoc />
    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                WrapNewChildren(e.NewItems);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                UnwrapRemovedChildren(e.OldItems);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not null)
                {
                    UnwrapRemovedChildren(e.OldItems);
                }

                if (e.NewItems is not null)
                {
                    WrapNewChildren(e.NewItems);
                }

                break;
        }

        _targetMapDirty = true;
        _isMotionDirty = true;
        InvalidateHostMeasure();
    }

    private void InvalidateHostMeasure()
    {
        InvalidateMeasure();
        Owner?.Presenter?.InvalidateMeasure();
        Owner?.InvalidateMeasure();
    }

    private void EnsureMotionReady()
    {
        if (_ensuringMotionRuntime)
        {
            return;
        }

        _ensuringMotionRuntime = true;
        try
        {
            RebuildTargetMap();
            if (!_isMotionDirty)
            {
                return;
            }

            if (CurrentMotion is not { } motion)
            {
                DisposeMotionRuntime();
                _isMotionDirty = false;
                return;
            }

            const int maximumCompilationAttempts = 2;
            for (var attempt = 0; attempt < maximumCompilationAttempts; attempt++)
            {
                _isMotionDirty = false;
                var context = new MotionCompilationContext(this, motion);
                MotionRuntime? candidate = null;
                try
                {
                    candidate = motion.CompileCore(context);
                    candidate.Prepare();
                }
                catch
                {
                    candidate?.Dispose();
                    context.Dispose();
                    _isMotionDirty = true;
                    throw;
                }

                if (!ReferenceEquals(motion, CurrentMotion) || _isMotionDirty)
                {
                    candidate.Dispose();
                    if (ReferenceEquals(motion, CurrentMotion) && attempt + 1 < maximumCompilationAttempts)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"{motion.GetType().Name} repeatedly invalidated itself while compiling.");
                }

                var previous = _motionRuntime;
                _motionRuntime = candidate;
                try
                {
                    candidate.Commit();
                }
                catch
                {
                    _motionRuntime = previous;
                    _isMotionDirty = true;
                    candidate.Dispose();
                    throw;
                }

                previous?.Dispose();
                candidate.CompleteCommit();
                return;
            }
        }
        finally
        {
            _ensuringMotionRuntime = false;
        }
    }

    private void DisposeMotionRuntime()
    {
        _motionRuntime?.Dispose();
        _motionRuntime = null;
    }

    private void RebuildTargetMap()
    {
        if (!_targetMapDirty)
        {
            return;
        }

        var existingContainers = new Dictionary<Control, MotionItemContainer>(_containers);
        _targets.Clear();
        _containers.Clear();
        foreach (var child in Children)
        {
            MotionItemContainer container;
            Control target;

            if (child is MotionItemContainer itemContainer)
            {
                container = itemContainer;
                target = container.GetOrCreateMotionChild() ?? throw new InvalidOperationException(
                    $"{nameof(MotionItemsPanel)} item container has no materialized content control.");
            }
            else
            {
                target = child;
                container = existingContainers.TryGetValue(child, out var existing) ?
                    existing :
                    throw new InvalidOperationException(
                        $"{nameof(MotionItemsPanel)} child '{Motion.GetEffectiveName(child) ?? child.GetType().Name}' has no motion container.");
            }

            var name = Motion.GetEffectiveName(target);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    $"Every direct child of {nameof(MotionItemsPanel)} must have a non-empty {nameof(Motion)} identity " +
                    $"({nameof(Motion.NameProperty)} or x:Name).");
            }

            if (!_targets.TryAdd(name, new TargetEntry(target, container)))
            {
                throw new InvalidOperationException($"{nameof(MotionItemsPanel)} contains more than one direct child with Motion identity '{name}'.");
            }

            _containers.Add(target, container);
        }

        _targetMapDirty = false;
    }

    private TargetEntry ResolveTargetEntry(string id)
    {
        RebuildTargetMap();
        if (!_targets.TryGetValue(id, out var target))
        {
            throw new InvalidOperationException(
                $"Motion layout source references target '{id}', but no direct child has that Motion identity.");
        }

        return target;
    }

    private void CollectRequiredLayoutSources()
    {
        _requiredLayoutSources.Clear();
        _requiredLayoutSourceSet.Clear();
        _motionRuntime?.CollectRequiredLayoutSources(AddRequiredLayoutSource);
    }

    private void AddRequiredLayoutSource(IMotionLayoutSource source)
    {
        if (_requiredLayoutSourceSet.Add(source))
        {
            _requiredLayoutSources.Add(source);
        }
    }

    private void MeasureRequiredLayoutSources(Size availableSize)
    {
        _motionRuntime?.CompilationContext.MeasureLayoutSources(_requiredLayoutSources, availableSize);
    }

    private void CaptureRequiredLayouts(Size finalSize)
    {
        _motionRuntime?.CompilationContext.CaptureLayoutSources(_requiredLayoutSources, finalSize);
    }

    private readonly record struct TargetEntry(
        Control Child,
        MotionItemContainer Container
    );

    private sealed class LayoutRootAttachment(MotionItemsPanel owner, Control root) : IDisposable
    {
        private MotionItemsPanel? _owner = owner;

        public void Dispose()
        {
            var currentOwner = _owner;
            if (currentOwner is null)
            {
                return;
            }

            _owner = null;
            currentOwner.DetachLayoutRoot(root);
        }
    }
}