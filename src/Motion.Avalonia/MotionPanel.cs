using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Projects live state templates onto stable direct children using a controller-provided pose.
/// </summary>
public sealed class MotionPanel : Control
{
    /// <summary>
    /// Defines the <see cref="Controller"/> property.
    /// </summary>
    public static readonly StyledProperty<IMotionController?> ControllerProperty =
        AvaloniaProperty.Register<MotionPanel, IMotionController?>(nameof(Controller));

    private static readonly MotionLayout EmptyLayout =
        new(default, new Dictionary<string, MotionLayoutEntry>(StringComparer.Ordinal));

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

    internal int TargetCount => _targets.Count;

    private readonly Dictionary<MotionState, MotionPanelHost> _scenes = [];
    private readonly Dictionary<string, TargetEntry> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<Control, MotionPanelItemContainer> _containers = [];
    private readonly Dictionary<Control, MotionPanelPropertyTarget> _propertyTargets = [];
    private readonly Dictionary<MotionState, Size> _measuredSizes = [];
    private readonly Dictionary<MotionState, MotionLayout> _arrangedLayouts = [];
    private readonly List<MotionState> _requiredStates = [];
    private IMotionController? _attachedController;
    private MotionLayout? _presentedLayout;
    private TopLevel? _topLevel;
    private bool _targetMapDirty = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionPanel"/> class.
    /// </summary>
    public MotionPanel()
    {
        Children.CollectionChanged += OnChildrenChanged;
        States.CollectionChanged += OnStatesChanged;
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

    internal bool RequestControllerAnimationFrame(
        MotionController controller,
        Action<TimeSpan> callback)
    {
        if (!ReferenceEquals(controller, _attachedController) || _topLevel is null)
        {
            return false;
        }

        _topLevel.RequestAnimationFrame(timestamp =>
        {
            if (ReferenceEquals(controller, _attachedController) && _topLevel is not null)
            {
                callback(timestamp);
            }
        });
        return true;
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
        UpdatePropertyTargets(pose);

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
        _topLevel = TopLevel.GetTopLevel(this);

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
            propertyTarget.CompleteImmediately();
        }

        _topLevel = null;
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

            if (_topLevel is not null)
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
            if (_topLevel is not null)
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
            _propertyTargets.Add(child, new MotionPanelPropertyTarget(child));
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
            BlendMotionPose blend => Lerp(
                EvaluateDesiredSize(blend.Source),
                EvaluateDesiredSize(blend.Target),
                blend.Easing.Ease(blend.Progress)),
            _ => default,
        };

    private Size GetMeasuredSize(object? key)
    {
        var state = FindState(key);
        return state is not null && _measuredSizes.TryGetValue(state, out var size) ? size : default;
    }

    private MotionLayout EvaluateLayout(MotionPose pose) =>
        pose switch
        {
            StateMotionPose statePose => GetArrangedLayout(statePose.Key),
            BlendMotionPose blend => BlendLayouts(
                EvaluateLayout(blend.Source),
                EvaluateLayout(blend.Target),
                blend.Easing.Ease(blend.Progress)),
            _ => EmptyLayout,
        };

    private MotionLayout GetArrangedLayout(object? key)
    {
        var state = FindState(key);
        return state is not null && _arrangedLayouts.TryGetValue(state, out var layout) ? layout : EmptyLayout;
    }

    private MotionLayout? GetPropertyTargetLayout(MotionPose pose) =>
        pose switch
        {
            StateMotionPose statePose => GetArrangedLayout(statePose.Key),
            BlendMotionPose blend => GetPropertyTargetLayout(blend.Target),
            _ => null,
        };

    private void UpdatePropertyTargets(MotionPose pose)
    {
        var targetLayout = GetPropertyTargetLayout(pose);
        var blend = pose as BlendMotionPose;
        var duration = blend?.PropertyTransitionDuration ?? TimeSpan.Zero;
        var easing = blend?.PropertyTransitionDuration is not null ? blend.Easing : null;

        foreach (var pair in _targets)
        {
            var slot = targetLayout?.Entries.TryGetValue(pair.Key, out var entry) == true ? entry.Slot : null;
            pair.Value.Properties.Update(slot, duration, easing);
        }
    }

    private void ApplyLayout(MotionLayout layout)
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

            var opacity = Math.Clamp(entry.Opacity, 0, 1);
            container.Opacity = opacity;
            container.IsHitTestVisible = opacity > 0.0001;
            container.Position = entry.Bounds.Position;
            container.Width = entry.Bounds.Width;
            container.Height = entry.Bounds.Height;
            container.Arrange(new Rect(container.Position, entry.Bounds.Size));
        }
    }

    private MotionLayout BlendLayouts(MotionLayout source, MotionLayout target, double progress)
    {
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
                sourceEntry = targetEntry with { Opacity = 0 };
            }

            if (!hasTarget)
            {
                targetEntry = sourceEntry with { Opacity = 0 };
            }

            entries.Add(
                id,
                new MotionLayoutEntry(
                    Lerp(sourceEntry.Bounds, targetEntry.Bounds, progress),
                    Lerp(sourceEntry.Opacity, targetEntry.Opacity, progress),
                    targetEntry.Slot ?? sourceEntry.Slot));
        }

        return new MotionLayout(Lerp(source.DesiredSize, target.DesiredSize, progress), entries);
    }

    private static double Lerp(double source, double target, double progress) =>
        source + ((target - source) * progress);

    private static Size Lerp(Size source, Size target, double progress) =>
        new(
            Lerp(source.Width, target.Width, progress),
            Lerp(source.Height, target.Height, progress));

    private static Rect Lerp(Rect source, Rect target, double progress) =>
        new(
            Lerp(source.X, target.X, progress),
            Lerp(source.Y, target.Y, progress),
            Lerp(source.Width, target.Width, progress),
            Lerp(source.Height, target.Height, progress));

    private readonly record struct TargetEntry(
        Control Child,
        MotionPanelItemContainer Container,
        MotionPanelPropertyTarget Properties
    );
}