using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Projects live state templates onto stable direct children using a controller-provided pose.
/// </summary>
/// <remarks>
/// Every direct child must have a unique, non-empty <c>x:Name</c>. A
/// <see cref="MotionSlot"/> in each state template maps to that child through
/// <see cref="MotionSlot.Target"/>. State templates remain live and are laid out again when
/// the panel size or a binding changes; they are not bitmap snapshots.
/// </remarks>
/// <example>
/// The same <c>Card</c> instance moves between two responsive layouts:
/// <code><![CDATA[
/// <motion:MotionPanel>
///   <motion:MotionPanel.Controller>
///     <motion:BasicMotionController
///         State="{Binding CurrentState}"
///         Duration="0:0:0.3"
///         Easing="CubicEaseInOut"/>
///   </motion:MotionPanel.Controller>
///
///   <Border x:Name="Card" Padding="16"/>
///
///   <motion:MotionPanel.States>
///     <motion:MotionState Key="Centered">
///       <DataTemplate>
///         <Grid>
///           <motion:MotionSlot
///             Target="Card"
///             MaxWidth="600"
///             HorizontalAlignment="Center"/>
///         </Grid>
///       </DataTemplate>
///     </motion:MotionState>
///     <motion:MotionState Key="Leading">
///       <DataTemplate>
///         <Grid ColumnDefinitions="420,*">
///           <motion:MotionSlot Target="Card" MaxWidth="420"/>
///         </Grid>
///       </DataTemplate>
///     </motion:MotionState>
///   </motion:MotionPanel.States>
/// </motion:MotionPanel>
/// ]]></code>
/// </example>
public sealed class MotionPanel : Control
{
    /// <summary>
    /// Defines the <see cref="Controller"/> property.
    /// </summary>
    public static readonly StyledProperty<IMotionController?> ControllerProperty =
        AvaloniaProperty.Register<MotionPanel, IMotionController?>(nameof(Controller));

    private static readonly MotionLayoutSnapshot EmptyLayout =
        new(default, new Dictionary<string, MotionLayoutEntry>(StringComparer.Ordinal));

    private static readonly IReadOnlyDictionary<AvaloniaProperty, object?> EmptyPropertyValues =
        new Dictionary<AvaloniaProperty, object?>();

    static MotionPanel()
    {
        AffectsMeasure<MotionPanel>(ControllerProperty);
        ControllerProperty.Changed.AddClassHandler<MotionPanel>(static (panel, _) => panel.OnControllerChanged());
    }

    /// <summary>
    /// Gets the stable logical children arranged by the panel.
    /// </summary>
    [Content]
    public Controls Children { get; } = new();

    /// <summary>
    /// Gets the persistent layout states known to the panel.
    /// </summary>
    public AvaloniaList<MotionState> States { get; } = [];

    /// <summary>
    /// Gets or sets the controller that produces the current pose.
    /// </summary>
    public IMotionController? Controller
    {
        get => GetValue(ControllerProperty);
        set => SetValue(ControllerProperty, value);
    }

    /// <summary>
    /// Gets the layout-channel and target-property interpolation overrides.
    /// </summary>
    public AvaloniaList<MotionInterpolator> Interpolators { get; } = [];

    internal int TargetCount => _targets.Count;

    private readonly Dictionary<MotionState, MotionPanelHost> _scenes = [];
    private readonly Dictionary<string, TargetEntry> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<Control, MotionPanelItemContainer> _containers = [];
    private readonly Dictionary<Control, MotionPanelPropertyTarget> _propertyTargets = [];
    private readonly Dictionary<InterpolatorLookup, MotionInterpolator> _interpolatorCache = [];
    private readonly Dictionary<MotionState, Size> _measuredSizes = [];
    private readonly Dictionary<MotionState, MotionLayoutSnapshot> _arrangedLayouts = [];
    private readonly List<MotionState> _requiredStates = [];
    private readonly HashSet<MotionInterpolator> _attachedInterpolators = [];
    private IMotionController? _attachedController;
    private MotionLayoutSnapshot? _presentedLayout;
    private bool _targetMapDirty = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionPanel"/> class.
    /// </summary>
    public MotionPanel()
    {
        Children.CollectionChanged += OnChildrenChanged;
        States.CollectionChanged += OnStatesChanged;
        Interpolators.CollectionChanged += OnInterpolatorsChanged;
    }

    internal Size MeasureTarget(string? id, Size availableSize)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return default;
        }

        if (!_targets.TryGetValue(id, out var target))
        {
            throw new InvalidOperationException($"Motion state references target '{id}', but no direct child has that name.");
        }

        target.Child.Measure(availableSize);
        return target.Child.DesiredSize;
    }

    internal void NotifyStateEndpointChanged(MotionState state, MotionPanelHost host)
    {
        InvalidateMeasure();

        if (!_arrangedLayouts.ContainsKey(state))
        {
            return;
        }

        _arrangedLayouts[state] = host.CaptureLayout();
        ApplyPropertyValues(EvaluateLayout(_attachedController?.CurrentPose ?? MotionPose.Empty));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        RebuildTargetMap();
        var pose = _attachedController?.CurrentPose ?? MotionPose.Empty;
        MeasureRequiredScenes(pose, availableSize);
        MeasurePresentedContainers();
        return EvaluateDesiredSize(pose);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var pose = _attachedController?.CurrentPose ?? MotionPose.Empty;
        CaptureRequiredLayouts(pose, finalSize);

        var layout = EvaluateLayout(pose);
        ApplyLayout(layout);
        _presentedLayout = layout;

        if (_attachedController is MotionController controller)
        {
            controller.NotifyOwnerPresented();
        }

        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_attachedController is MotionController controller)
        {
            controller.NotifyOwnerAttachedToVisualTree();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_attachedController is MotionController controller)
        {
            controller.NotifyOwnerDetachedFromVisualTree();
        }

        foreach (var propertyTarget in _propertyTargets.Values)
        {
            propertyTarget.Apply(EmptyPropertyValues);
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnControllerChanged()
    {
        DetachController();
        _attachedController = Controller;

        if (_attachedController is null)
        {
            InvalidateMeasure();
            return;
        }

        _attachedController.CurrentPoseChanged += OnCurrentPoseChanged;
        if (_attachedController is MotionController controller)
        {
            controller.Attach(this);
            LogicalChildren.Add(controller);

            if (this.IsAttachedToVisualTree())
            {
                controller.NotifyOwnerAttachedToVisualTree();
            }
        }

        InvalidateMeasure();
    }

    private void DetachController()
    {
        if (_attachedController is null)
        {
            return;
        }

        _attachedController.CurrentPoseChanged -= OnCurrentPoseChanged;
        if (_attachedController is MotionController controller)
        {
            if (this.IsAttachedToVisualTree())
            {
                controller.NotifyOwnerDetachedFromVisualTree();
            }

            LogicalChildren.Remove(controller);
            controller.Detach(this);
        }

        _attachedController = null;
    }

    private void OnCurrentPoseChanged(object? sender, EventArgs e) => InvalidateMeasure();

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                AddChildren(e.NewStartingIndex, e.NewItems);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                RemoveChildren(e.OldItems);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not null)
                {
                    RemoveChildren(e.OldItems);
                }

                if (e.NewItems is not null)
                {
                    AddChildren(e.NewStartingIndex, e.NewItems);
                }

                break;
            case NotifyCollectionChangedAction.Move when e.OldItems is not null:
                LogicalChildren.MoveRange(e.OldStartingIndex, e.OldItems.Count, e.NewStartingIndex);
                VisualChildren.MoveRange(e.OldStartingIndex, e.OldItems.Count, e.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Reset:
                ResetChildren();
                break;
        }

        _targetMapDirty = true;
        InvalidateMeasure();
    }

    private void AddChildren(int index, IList items)
    {
        foreach (var child in items.OfType<Control>())
        {
            if (_containers.ContainsKey(child))
            {
                throw new InvalidOperationException($"{nameof(MotionPanel)} cannot contain the same child more than once.");
            }

            var container = new MotionPanelItemContainer(child);
            _containers.Add(child, container);
            _propertyTargets.Add(child, new MotionPanelPropertyTarget(child, container));
            child.PropertyChanged += OnChildPropertyChanged;
            LogicalChildren.Insert(index, child);
            VisualChildren.Insert(index, container);
            index++;
        }
    }

    private void RemoveChildren(IList items)
    {
        foreach (var child in items.OfType<Control>())
        {
            child.PropertyChanged -= OnChildPropertyChanged;

            if (_containers.Remove(child, out var container))
            {
                VisualChildren.Remove(container);
                container.DetachChild();
            }

            if (_propertyTargets.Remove(child, out var propertyTarget))
            {
                propertyTarget.Dispose();
            }

            LogicalChildren.Remove(child);
        }
    }

    private void ResetChildren()
    {
        foreach (var pair in _containers.ToArray())
        {
            pair.Key.PropertyChanged -= OnChildPropertyChanged;
            VisualChildren.Remove(pair.Value);
            pair.Value.DetachChild();
            LogicalChildren.Remove(pair.Key);
            _propertyTargets[pair.Key].Dispose();
        }

        _containers.Clear();
        _propertyTargets.Clear();
        AddChildren(0, Children);
    }

    private void OnChildPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != NameProperty)
        {
            return;
        }

        _targetMapDirty = true;
        InvalidateMeasure();
    }

    private void OnStatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeScenes();
        InvalidateMeasure();
    }

    private void OnInterpolatorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeInterpolators();
        _interpolatorCache.Clear();
        InvalidateMeasure();
    }

    private void OnInterpolatorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        _interpolatorCache.Clear();
        InvalidateMeasure();
    }

    private void OnInterpolatorEdgesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _interpolatorCache.Clear();
        InvalidateMeasure();
    }

    private void OnStatePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MotionState.TemplateProperty)
        {
            _scenes[(MotionState)sender!].Rebuild();
        }

        InvalidateMeasure();
    }

    private void SynchronizeScenes()
    {
        foreach (var pair in _scenes.Where(pair => !States.Contains(pair.Key)).ToArray())
        {
            pair.Key.PropertyChanged -= OnStatePropertyChanged;
            LogicalChildren.Remove(pair.Value);
            VisualChildren.Remove(pair.Value);
            pair.Value.Dispose();
            _scenes.Remove(pair.Key);
        }

        foreach (var state in States)
        {
            if (_scenes.ContainsKey(state))
            {
                continue;
            }

            state.PropertyChanged += OnStatePropertyChanged;
            var scene = new MotionPanelHost(this, state);
            _scenes.Add(state, scene);
            LogicalChildren.Add(scene);
            VisualChildren.Add(scene);
        }
    }

    private void SynchronizeInterpolators()
    {
        foreach (var interpolator in _attachedInterpolators.Where(item => !Interpolators.Contains(item)).ToArray())
        {
            interpolator.PropertyChanged -= OnInterpolatorPropertyChanged;
            interpolator.Edges.CollectionChanged -= OnInterpolatorEdgesChanged;
            _attachedInterpolators.Remove(interpolator);
        }

        foreach (var interpolator in Interpolators)
        {
            if (!_attachedInterpolators.Add(interpolator))
            {
                continue;
            }

            interpolator.PropertyChanged += OnInterpolatorPropertyChanged;
            interpolator.Edges.CollectionChanged += OnInterpolatorEdgesChanged;
        }
    }

    private void RebuildTargetMap()
    {
        if (!_targetMapDirty)
        {
            return;
        }

        _targets.Clear();

        foreach (var child in Children)
        {
            var id = child.Name;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException($"Every direct child of {nameof(MotionPanel)} must have a non-empty x:Name.");
            }

            if (!_targets.TryAdd(id, new TargetEntry(child, _containers[child], _propertyTargets[child])))
            {
                throw new InvalidOperationException($"{nameof(MotionPanel)} contains more than one direct child named '{id}'.");
            }
        }

        _targetMapDirty = false;
    }

    private void MeasureRequiredScenes(MotionPose pose, Size availableSize)
    {
        CollectRequiredStates(pose);
        _measuredSizes.Clear();

        foreach (var state in _requiredStates)
        {
            var scene = _scenes[state];
            scene.Measure(availableSize);
            _measuredSizes.Add(state, scene.DesiredSize);
        }
    }

    private void MeasurePresentedContainers()
    {
        foreach (var pair in _targets)
        {
            var container = pair.Value.Container;

            if (_presentedLayout?.Entries.TryGetValue(pair.Key, out var entry) == true)
            {
                container.Width = entry.Bounds.Width;
                container.Height = entry.Bounds.Height;
                container.Measure(entry.Bounds.Size);
            }
            else
            {
                container.Width = 0;
                container.Height = 0;
                container.Measure(default);
            }
        }
    }

    private void CaptureRequiredLayouts(MotionPose pose, Size finalSize)
    {
        CollectRequiredStates(pose);
        _arrangedLayouts.Clear();

        foreach (var state in _requiredStates)
        {
            var scene = _scenes[state];
            scene.Arrange(new Rect(finalSize));
            _arrangedLayouts.Add(state, scene.CaptureLayout());
        }
    }

    private void CollectRequiredStates(MotionPose pose)
    {
        _requiredStates.Clear();
        CollectRequiredStatesCore(pose);
    }

    private void CollectRequiredStatesCore(MotionPose pose)
    {
        switch (pose)
        {
            case StateMotionPose statePose:
                var state = FindState(statePose.Key);
                if (state is not null && !_requiredStates.Contains(state))
                {
                    _requiredStates.Add(state);
                }

                break;
            case BlendMotionPose blend:
                CollectRequiredStatesCore(blend.Source);
                CollectRequiredStatesCore(blend.Target);
                break;
        }
    }

    private MotionState? FindState(object? key)
    {
        MotionState? result = null;

        foreach (var state in States)
        {
            if (!Equals(state.Key, key))
            {
                continue;
            }

            if (result is not null)
            {
                throw new InvalidOperationException($"{nameof(MotionPanel)} contains multiple states with key '{key}'.");
            }

            result = state;
        }

        return result;
    }

    private Size EvaluateDesiredSize(MotionPose pose) =>
        pose switch
        {
            StateMotionPose statePose => GetMeasuredSize(statePose.Key),
            BlendMotionPose blend => InterpolateValue(
                null,
                MotionLayout.DesiredSizeProperty,
                EvaluateDesiredSize(blend.Source),
                EvaluateDesiredSize(blend.Target),
                blend,
                blend.Easing.Ease(blend.Progress)),
            _ => default,
        };

    private Size GetMeasuredSize(object? key)
    {
        var state = FindState(key);
        return state is not null && _measuredSizes.TryGetValue(state, out var size) ? size : default;
    }

    private MotionLayoutSnapshot EvaluateLayout(MotionPose pose) =>
        pose switch
        {
            StateMotionPose statePose => GetArrangedLayout(statePose.Key),
            BlendMotionPose blend => BlendLayouts(
                EvaluateLayout(blend.Source),
                EvaluateLayout(blend.Target),
                blend),
            _ => EmptyLayout,
        };

    private MotionLayoutSnapshot GetArrangedLayout(object? key)
    {
        var state = FindState(key);
        return state is not null && _arrangedLayouts.TryGetValue(state, out var layout) ? layout : EmptyLayout;
    }

    private void ApplyLayout(MotionLayoutSnapshot layout)
    {
        foreach (var pair in _targets)
        {
            var container = pair.Value.Container;
            if (!layout.Entries.TryGetValue(pair.Key, out var entry))
            {
                container.Opacity = 0;
                container.IsHitTestVisible = false;
                container.Position = default;
                container.Width = 0;
                container.Height = 0;
                container.Arrange(default);
                continue;
            }

            var presence = Math.Clamp(entry.Presence, 0, 1);
            container.Opacity = presence;
            container.IsHitTestVisible = presence > 0.0001;
            container.Position = entry.Bounds.Position;
            container.Width = entry.Bounds.Width;
            container.Height = entry.Bounds.Height;
            container.Arrange(new Rect(container.Position, entry.Bounds.Size));
        }

        ApplyPropertyValues(layout);
    }

    private void ApplyPropertyValues(MotionLayoutSnapshot layout)
    {
        foreach (var pair in _targets)
        {
            var values = layout.Entries.TryGetValue(pair.Key, out var entry) ? entry.Properties : EmptyPropertyValues;
            pair.Value.Properties.Apply(values);
        }
    }

    private MotionLayoutSnapshot BlendLayouts(MotionLayoutSnapshot source, MotionLayoutSnapshot target, BlendMotionPose blend)
    {
        var progress = blend.Easing.Ease(blend.Progress);
        var entries = new Dictionary<string, MotionLayoutEntry>(
            _targets.Count,
            StringComparer.Ordinal);

        foreach (var id in _targets.Keys)
        {
            var hasSource = source.Entries.TryGetValue(id, out var sourceEntry);
            var hasTarget = target.Entries.TryGetValue(id, out var targetEntry);

            if (!hasSource && !hasTarget)
            {
                continue;
            }

            if (!hasSource)
            {
                sourceEntry = targetEntry with { Presence = 0 };
            }

            if (!hasTarget)
            {
                targetEntry = sourceEntry with { Presence = 0 };
            }

            entries.Add(
                id,
                new MotionLayoutEntry(
                    new Rect(
                        InterpolateValue(
                            id,
                            MotionLayout.PositionProperty,
                            sourceEntry.Bounds.Position,
                            targetEntry.Bounds.Position,
                            blend,
                            progress),
                        InterpolateValue(
                            id,
                            MotionLayout.SizeProperty,
                            sourceEntry.Bounds.Size,
                            targetEntry.Bounds.Size,
                            blend,
                            progress)),
                    InterpolateValue(
                        id,
                        MotionLayout.PresenceProperty,
                        sourceEntry.Presence,
                        targetEntry.Presence,
                        blend,
                        progress),
                    BlendProperties(id, sourceEntry.Properties, targetEntry.Properties, blend, progress)));
        }

        return new MotionLayoutSnapshot(
            InterpolateValue(
                null,
                MotionLayout.DesiredSizeProperty,
                source.DesiredSize,
                target.DesiredSize,
                blend,
                progress),
            entries);
    }

    private IReadOnlyDictionary<AvaloniaProperty, object?> BlendProperties(
        string target,
        IReadOnlyDictionary<AvaloniaProperty, object?> source,
        IReadOnlyDictionary<AvaloniaProperty, object?> destination,
        BlendMotionPose blend,
        double inheritedProgress)
    {
        if (source.Count == 0 && destination.Count == 0)
        {
            return EmptyPropertyValues;
        }

        var result = new Dictionary<AvaloniaProperty, object?>(source.Count + destination.Count);
        foreach (var property in source.Keys.Concat(destination.Keys).Distinct())
        {
            var oldValue = source.TryGetValue(property, out var sourceValue) ? sourceValue : _targets[target].Properties.GetBaseValue(property);
            var newValue = destination.TryGetValue(property, out var destinationValue) ?
                destinationValue :
                _targets[target].Properties.GetBaseValue(property);
            result.Add(
                property,
                InterpolateValue(
                    target,
                    property,
                    oldValue,
                    newValue,
                    blend,
                    inheritedProgress));
        }

        return result;
    }

    private T InterpolateValue<T>(
        string? target,
        AvaloniaProperty property,
        T oldValue,
        T newValue,
        BlendMotionPose blend,
        double inheritedProgress)
    {
        var interpolator = FindInterpolator(target, property, blend.Edge);
        var progress = interpolator.Easing?.Ease(blend.Progress) ?? inheritedProgress;
        return (T)interpolator.InterpolateCore(property, progress, oldValue, newValue)!;
    }

    private MotionInterpolator FindInterpolator(string? target, AvaloniaProperty property, MotionEdge? edge)
    {
        var lookup = new InterpolatorLookup(target, property, edge);
        if (_interpolatorCache.TryGetValue(lookup, out var cached))
        {
            return cached;
        }

        MotionInterpolator? general = null;
        MotionInterpolator? edgeSpecific = null;

        foreach (var interpolator in Interpolators)
        {
            ValidateInterpolator(interpolator);

            if (!StringComparer.Ordinal.Equals(interpolator.Target, target) || interpolator.Property != property)
            {
                continue;
            }

            if (interpolator.Edges.Count > 0)
            {
                if (edge is null || !interpolator.Edges.Contains(edge))
                {
                    continue;
                }

                if (edgeSpecific is not null)
                {
                    throw CreateDuplicateInterpolatorException(target, property, edge);
                }

                edgeSpecific = interpolator;
                continue;
            }

            if (general is not null)
            {
                throw CreateDuplicateInterpolatorException(target, property, null);
            }

            general = interpolator;
        }

        var result = edgeSpecific ?? general ?? MotionAvaloniaInterpolator.Default;
        _interpolatorCache.Add(lookup, result);
        return result;
    }

    private static void ValidateInterpolator(MotionInterpolator interpolator)
    {
        if (interpolator.Property is not { } property)
        {
            throw new InvalidOperationException(
                $"Every {nameof(MotionInterpolator)} in {nameof(MotionPanel)}.{nameof(Interpolators)} must specify a Property.");
        }

        if (MotionLayout.IsPanelProperty(property))
        {
            if (interpolator.Target is not null)
            {
                throw new InvalidOperationException(
                    $"{nameof(MotionLayout)}.{property.Name} is panel-wide, so its interpolator must not specify a Target.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(interpolator.Target))
        {
            throw new InvalidOperationException($"An interpolator for property '{property}' must specify a non-empty Target.");
        }
    }

    private static InvalidOperationException CreateDuplicateInterpolatorException(
        string? target,
        AvaloniaProperty property,
        MotionEdge? edge) =>
        new(
            $"{nameof(MotionPanel)} contains more than one interpolator " +
            $"{(target is null ? "for the panel" : $"for target '{target}'")}, " +
            $"property '{property}'{(edge is null ? string.Empty : $" and edge '{edge.From}' to '{edge.To}'")}.");

    private readonly record struct TargetEntry(
        Control Child,
        MotionPanelItemContainer Container,
        MotionPanelPropertyTarget Properties
    );

    private readonly record struct InterpolatorLookup(
        string? Target,
        AvaloniaProperty Property,
        MotionEdge? Edge
    );
}