using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
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
/// <see cref="MotionSlot"/> in each state template maps to the child with the same
/// <see cref="StyledElement.Name"/>. State templates remain live and are laid out again
/// when the panel size or a binding changes; they are not bitmap snapshots.
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
///             x:Name="Card"
///             MaxWidth="600"
///             HorizontalAlignment="Center"/>
///         </Grid>
///       </DataTemplate>
///     </motion:MotionState>
///     <motion:MotionState Key="Leading">
///       <DataTemplate>
///         <Grid ColumnDefinitions="420,*">
///           <motion:MotionSlot x:Name="Card" MaxWidth="420"/>
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

    private static readonly MotionLayoutBuffer EmptyLayout = new();

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
    private readonly Dictionary<StyledElement, MotionPropertyTarget> _propertyTargets = [];
    private readonly Dictionary<MotionStateKey, MotionState> _stateIndex = [];
    private readonly Dictionary<InterpolatorLookup, MotionInterpolator> _interpolatorIndex = [];
    private readonly Dictionary<MotionState, Size> _measuredSizes = [];
    private readonly Dictionary<MotionState, MotionLayoutBuffer> _arrangedLayouts = [];
    private readonly List<MotionState> _requiredStates = [];
    private readonly List<MotionLayoutBuffer> _blendLayoutBuffers = [];
    private readonly List<StyledElement> _obsoletePropertyTargets = [];
    private readonly HashSet<MotionInterpolator> _attachedInterpolators = [];

    private IMotionController? _attachedController;
    private MotionLayoutBuffer? _presentedLayout;
    private int _nextBlendLayoutBuffer;
    private bool _targetMapDirty = true;
    private bool _interpolatorsDirty = true;
    private bool _interpolatorIndexDirty = true;
    private bool _scenesDirty = true;
    private bool _stateIndexDirty = true;
    private bool _sceneTargetsDirty = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionPanel"/> class.
    /// </summary>
    public MotionPanel()
    {
        Children.CollectionChanged += OnChildrenChanged;
        States.CollectionChanged += OnStatesChanged;
        Interpolators.CollectionChanged += OnInterpolatorsChanged;
    }

    internal Control ResolveTarget(string id)
    {
        RebuildTargetMap();

        if (!_targets.TryGetValue(id, out var target))
        {
            throw new InvalidOperationException($"Motion state references target '{id}', but no direct child has that name.");
        }

        return target.Child;
    }

    internal void NotifyStateEndpointChanged(MotionState state, MotionPanelHost host)
    {
        InvalidateMeasure();

        if (!_arrangedLayouts.ContainsKey(state))
        {
            return;
        }

        _arrangedLayouts[state] = host.CaptureLayout();
        ApplyPropertyValues(EvaluateLayoutRoot(_attachedController?.CurrentPose ?? MotionPose.Empty));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureTopology();
        var pose = _attachedController?.CurrentPose ?? MotionPose.Empty;
        MeasureRequiredScenes(pose, availableSize);
        MeasurePresentedContainers();
        return EvaluateDesiredSize(pose);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        EnsureTopology();
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var pose = _attachedController?.CurrentPose ?? MotionPose.Empty;
        CaptureRequiredLayouts(pose, finalSize);

        var layout = EvaluateLayoutRoot(pose);
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
        _sceneTargetsDirty = true;
        _presentedLayout = null;
        ClearBlendLayoutBuffers();
        foreach (var scene in _scenes.Values)
        {
            scene.InvalidateTargets();
        }

        if (IsInitialized)
        {
            EnsureTopology();
        }

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
            LogicalChildren.Insert(index, child);
            VisualChildren.Insert(index, container);
            index++;
        }
    }

    private void RemoveChildren(IList items)
    {
        foreach (var child in items.OfType<Control>())
        {
            if (_containers.Remove(child, out var container))
            {
                VisualChildren.Remove(container);
                container.DetachChild();
            }

            LogicalChildren.Remove(child);
        }

        ClearPropertyTargets();
    }

    private void ResetChildren()
    {
        foreach (var pair in _containers.ToArray())
        {
            VisualChildren.Remove(pair.Value);
            pair.Value.DetachChild();
            LogicalChildren.Remove(pair.Key);
        }

        _containers.Clear();
        ClearPropertyTargets();
        AddChildren(0, Children);
    }

    private void OnStatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _scenesDirty = true;
        _stateIndexDirty = true;
        _sceneTargetsDirty = true;
        ClearBlendLayoutBuffers();

        if (IsInitialized)
        {
            EnsureTopology();
        }

        InvalidateMeasure();
    }

    private void OnInterpolatorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _interpolatorsDirty = true;
        _interpolatorIndexDirty = true;

        if (IsInitialized)
        {
            EnsureTopology();
        }

        InvalidateMeasure();
    }

    private void OnInterpolatorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        _interpolatorIndexDirty = true;

        if (IsInitialized)
        {
            EnsureTopology();
        }

        InvalidateMeasure();
    }

    private void OnInterpolatorEdgesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _interpolatorIndexDirty = true;

        if (IsInitialized)
        {
            EnsureTopology();
        }

        InvalidateMeasure();
    }

    private void OnStatePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MotionState.KeyProperty)
        {
            _stateIndexDirty = true;
        }

        if (e.Property == MotionState.TemplateProperty)
        {
            _scenes[(MotionState)sender!].Rebuild();
            _sceneTargetsDirty = true;

            if (IsInitialized)
            {
                EnsureTopology();
            }
        }

        if (IsInitialized && _stateIndexDirty)
        {
            EnsureTopology();
        }

        InvalidateMeasure();
    }

    private void EnsureTopology()
    {
        RebuildTargetMap();
        EnsureInterpolators();
        EnsureScenes();
        EnsureSceneTargets();
    }

    private void EnsureInterpolators()
    {
        if (_interpolatorsDirty)
        {
            SynchronizeInterpolators();
            _interpolatorsDirty = false;
        }

        if (_interpolatorIndexDirty)
        {
            RebuildInterpolatorIndex();
            _interpolatorIndexDirty = false;
        }
    }

    private void EnsureScenes()
    {
        if (_scenesDirty)
        {
            SynchronizeScenes();
            _scenesDirty = false;
            _stateIndexDirty = true;
            _sceneTargetsDirty = true;
        }

        if (_stateIndexDirty)
        {
            RebuildStateIndex();
            _stateIndexDirty = false;
        }
    }

    private void EnsureSceneTargets()
    {
        if (!_sceneTargetsDirty)
        {
            return;
        }

        foreach (var scene in _scenes.Values)
        {
            scene.RebindTargets();
        }

        _sceneTargetsDirty = false;
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

    private void RebuildStateIndex()
    {
        _stateIndex.Clear();

        foreach (var state in States)
        {
            var key = new MotionStateKey(state.Key);
            if (!_stateIndex.TryAdd(key, state))
            {
                throw new InvalidOperationException($"{nameof(MotionPanel)} contains multiple states with key '{state.Key}'.");
            }
        }
    }

    private void RebuildInterpolatorIndex()
    {
        _interpolatorIndex.Clear();

        foreach (var interpolator in Interpolators)
        {
            ValidateInterpolator(interpolator);

            if (interpolator.Edges.Count == 0)
            {
                AddInterpolatorIndexEntry(interpolator, null);
                continue;
            }

            foreach (var edge in interpolator.Edges)
            {
                AddInterpolatorIndexEntry(interpolator, edge);
            }
        }
    }

    private void AddInterpolatorIndexEntry(MotionInterpolator interpolator, MotionEdge? edge)
    {
        var lookup = new InterpolatorLookup(interpolator.Target, interpolator.Property!, edge);
        if (_interpolatorIndex.TryAdd(lookup, interpolator) ||
            ReferenceEquals(_interpolatorIndex[lookup], interpolator))
        {
            return;
        }

        throw CreateDuplicateInterpolatorException(interpolator.Target, interpolator.Property!, edge);
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

            if (!_targets.TryAdd(id, new TargetEntry(child, _containers[child])))
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

    private MotionState? FindState(object? key) =>
        _stateIndex.GetValueOrDefault(new MotionStateKey(key));

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

    private MotionLayoutBuffer EvaluateLayoutRoot(MotionPose pose)
    {
        _nextBlendLayoutBuffer = 0;
        return EvaluateLayout(pose);
    }

    private MotionLayoutBuffer EvaluateLayout(MotionPose pose) =>
        pose switch
        {
            StateMotionPose statePose => GetArrangedLayout(statePose.Key),
            BlendMotionPose blend => BlendLayouts(
                EvaluateLayout(blend.Source),
                EvaluateLayout(blend.Target),
                blend),
            _ => EmptyLayout,
        };

    private MotionLayoutBuffer GetArrangedLayout(object? key)
    {
        var state = FindState(key);
        return state is not null && _arrangedLayouts.TryGetValue(state, out var layout) ? layout : EmptyLayout;
    }

    private MotionLayoutBuffer GetNextBlendLayoutBuffer()
    {
        if (_nextBlendLayoutBuffer == _blendLayoutBuffers.Count)
        {
            _blendLayoutBuffers.Add(new MotionLayoutBuffer(_targets.Count));
        }

        return _blendLayoutBuffers[_nextBlendLayoutBuffer++];
    }

    private void ClearBlendLayoutBuffers()
    {
        foreach (var buffer in _blendLayoutBuffers)
        {
            buffer.Clear();
        }

        _blendLayoutBuffers.Clear();
        _nextBlendLayoutBuffer = 0;
    }

    private void ApplyLayout(MotionLayoutBuffer layout)
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

    private void ApplyPropertyValues(MotionLayoutBuffer layout)
    {
        foreach (var pair in layout.PropertyValues)
        {
            GetOrCreatePropertyTarget(pair.Key).Apply(pair.Value);
        }

        _obsoletePropertyTargets.Clear();
        foreach (var pair in _propertyTargets)
        {
            if (!layout.PropertyValues.ContainsKey(pair.Key))
            {
                _obsoletePropertyTargets.Add(pair.Key);
            }
        }

        foreach (var target in _obsoletePropertyTargets)
        {
            if (_propertyTargets.Remove(target, out var propertyTarget))
            {
                propertyTarget.Dispose();
            }
        }
    }

    private MotionLayoutBuffer BlendLayouts(MotionLayoutBuffer source, MotionLayoutBuffer target, BlendMotionPose blend)
    {
        var progress = blend.Easing.Ease(blend.Progress);
        var result = GetNextBlendLayoutBuffer();
        result.BeginLayoutUpdate(
            InterpolateValue(
                null,
                MotionLayout.DesiredSizeProperty,
                source.DesiredSize,
                target.DesiredSize,
                blend,
                progress));
        result.BeginPropertyValuesUpdate();

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

            result.EntriesBuffer.Add(
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
                        progress)));
        }

        BlendPropertyValues(source.PropertyValues, target.PropertyValues, blend, progress, result);
        return result;
    }

    private void BlendPropertyValues(
        IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>> source,
        IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>> destination,
        BlendMotionPose blend,
        double inheritedProgress,
        MotionLayoutBuffer result)
    {
        foreach (var pair in source)
        {
            var destinationValues = destination.GetValueOrDefault(pair.Key, EmptyPropertyValues);
            BlendProperties(pair.Key, pair.Value, destinationValues, blend, inheritedProgress, result);
        }

        foreach (var pair in destination)
        {
            if (!source.ContainsKey(pair.Key))
            {
                BlendProperties(pair.Key, EmptyPropertyValues, pair.Value, blend, inheritedProgress, result);
            }
        }
    }

    private void BlendProperties(
        StyledElement target,
        IReadOnlyDictionary<AvaloniaProperty, object?> source,
        IReadOnlyDictionary<AvaloniaProperty, object?> destination,
        BlendMotionPose blend,
        double inheritedProgress,
        MotionLayoutBuffer result)
    {
        if (source.Count == 0 && destination.Count == 0)
        {
            return;
        }

        if (!result.TryAddPropertyTarget(target, out var values))
        {
            throw new InvalidOperationException(
                $"Motion blend produced duplicate property values for target '{target.Name ?? target.GetType().Name}'.");
        }

        var propertyTarget = GetOrCreatePropertyTarget(target);
        foreach (var pair in source)
        {
            var newValue = destination.TryGetValue(pair.Key, out var destinationValue) ?
                destinationValue :
                propertyTarget.GetBaseValue(pair.Key);
            values.Add(
                pair.Key,
                InterpolateValue(
                    target.Name,
                    pair.Key,
                    pair.Value,
                    newValue,
                    blend,
                    inheritedProgress));
        }

        foreach (var pair in destination)
        {
            if (source.ContainsKey(pair.Key))
            {
                continue;
            }

            values.Add(
                pair.Key,
                InterpolateValue(
                    target.Name,
                    pair.Key,
                    propertyTarget.GetBaseValue(pair.Key),
                    pair.Value,
                    blend,
                    inheritedProgress));
        }
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
        if (edge is not null &&
            _interpolatorIndex.TryGetValue(new InterpolatorLookup(target, property, edge), out var edgeSpecific))
        {
            return edgeSpecific;
        }

        if (_interpolatorIndex.TryGetValue(new InterpolatorLookup(target, property, null), out var general))
        {
            return general;
        }

        return MotionAvaloniaInterpolator.Default;
    }

    private MotionPropertyTarget GetOrCreatePropertyTarget(StyledElement target)
    {
        if (_propertyTargets.TryGetValue(target, out var result))
        {
            return result;
        }

        MotionPanelItemContainer? container = null;
        if (target is Control control)
        {
            _containers.TryGetValue(control, out container);
        }

        result = new MotionPropertyTarget(target, container);
        _propertyTargets.Add(target, result);
        return result;
    }

    private void ClearPropertyTargets()
    {
        foreach (var target in _propertyTargets.Values)
        {
            target.Dispose();
        }

        _propertyTargets.Clear();
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
        MotionPanelItemContainer Container
    );

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private readonly record struct InterpolatorLookup(
        string? Target,
        AvaloniaProperty Property,
        MotionEdge? Edge
    );

    private readonly record struct MotionStateKey(object? Value)
    {
        public bool Equals(MotionStateKey other) => Equals(Value, other.Value);

        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    }
}