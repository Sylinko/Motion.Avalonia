using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Arranges stable direct children using persistent, live layout state templates.
/// </summary>
public sealed class TransitionPanel : Control
{
    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> StateProperty =
        AvaloniaProperty.Register<TransitionPanel, object?>(nameof(State));

    /// <summary>
    /// Defines the <see cref="StateTransitionDuration"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> StateTransitionDurationProperty =
        AvaloniaProperty.Register<TransitionPanel, TimeSpan>(
            nameof(StateTransitionDuration),
            TimeSpan.FromMilliseconds(300),
            validate: static value => value >= TimeSpan.Zero);

    /// <summary>
    /// Defines the <see cref="StateTransitionEasing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> StateTransitionEasingProperty =
        AvaloniaProperty.Register<TransitionPanel, Easing?>(nameof(StateTransitionEasing));

    /// <summary>
    /// Defines the <see cref="LayoutTransitionDuration"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> LayoutTransitionDurationProperty =
        AvaloniaProperty.Register<TransitionPanel, TimeSpan>(
            nameof(LayoutTransitionDuration),
            TimeSpan.FromMilliseconds(250),
            validate: static value => value >= TimeSpan.Zero);

    /// <summary>
    /// Defines the <see cref="LayoutTransitionEasing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> LayoutTransitionEasingProperty =
        AvaloniaProperty.Register<TransitionPanel, Easing?>(nameof(LayoutTransitionEasing));

    static TransitionPanel()
    {
        AffectsMeasure<TransitionPanel>(StateProperty);
        StateProperty.Changed.AddClassHandler<TransitionPanel>(static (panel, _) => panel.OnRequestedStateChanged());
        StateTransitionEasingProperty.Changed.AddClassHandler<TransitionPanel>(static (panel, _) => panel.OnStateTransitionSettingsChanged());
        StateTransitionDurationProperty.Changed.AddClassHandler<TransitionPanel>(static (panel, _) => panel.OnStateTransitionSettingsChanged());
    }

    /// <summary>
    /// Gets the stable logical children arranged by the panel.
    /// </summary>
    [Content]
    public Controls Children { get; } = new();

    /// <summary>
    /// Gets the persistent layout states known to the panel.
    /// </summary>
    public AvaloniaList<TransitionPanelState> States { get; } = new();

    /// <summary>
    /// Gets or sets the key of the requested layout state.
    /// </summary>
    public object? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Gets or sets the duration used when <see cref="State"/> changes.
    /// </summary>
    public TimeSpan StateTransitionDuration
    {
        get => GetValue(StateTransitionDurationProperty);
        set => SetValue(StateTransitionDurationProperty, value);
    }

    /// <summary>
    /// Gets or sets the easing used when <see cref="State"/> changes.
    /// A null value disables state transition animation.
    /// </summary>
    public Easing? StateTransitionEasing
    {
        get => GetValue(StateTransitionEasingProperty);
        set => SetValue(StateTransitionEasingProperty, value);
    }

    /// <summary>
    /// Gets or sets the duration used when the active state layout changes without a state change.
    /// </summary>
    public TimeSpan LayoutTransitionDuration
    {
        get => GetValue(LayoutTransitionDurationProperty);
        set => SetValue(LayoutTransitionDurationProperty, value);
    }

    /// <summary>
    /// Gets or sets the easing used when the active state layout changes without a state change.
    /// A null value applies live layout changes immediately.
    /// </summary>
    public Easing? LayoutTransitionEasing
    {
        get => GetValue(LayoutTransitionEasingProperty);
        set => SetValue(LayoutTransitionEasingProperty, value);
    }

    internal int TargetCount => _targets.Count;

    private readonly Dictionary<TransitionPanelState, TransitionPanelHost> _scenes = new();
    private readonly Dictionary<string, TargetEntry> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<Control, TransitionPanelItemContainer> _containers = new();
    private readonly Dictionary<TransitionPanelState, double> _stateSourceWeights = new();
    private readonly Dictionary<TransitionPanelState, Size> _measuredSizes = new();
    private readonly Dictionary<TransitionPanelState, MotionLayout> _arrangedLayouts = new();
    private readonly List<TransitionPanelState> _requiredStates = new();
    private readonly List<KeyValuePair<TransitionPanelState, double>> _stateWeightBuffer = new();
    private TransitionPanelState? _selectedState;
    private TransitionPanelState? _settledState;
    private MotionLayout? _fixedStateSource;
    private MotionLayout? _layoutSource;
    private MotionLayout? _presentedLayout;
    private MotionLayout? _lastTargetLayout;
    private TransitionKind _transitionKind;
    private TopLevel? _topLevel;
    private TimeSpan _transitionElapsed;
    private TimeSpan? _lastFrameTime;
    private bool _hasPresentedLayout;
    private bool _stateChangePending = true;
    private bool _targetMapDirty = true;
    private bool _frameRequested;
    private bool _suppressLayoutTransitionOnce;
    private int _frameGeneration;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionPanel"/> class.
    /// </summary>
    public TransitionPanel()
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
            throw new InvalidOperationException(
                $"Layout morph state references target '{id}', but no direct child has that name.");
        }

        target.Child.Measure(availableSize);
        return target.Child.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        RebuildTargetMap();
        ApplyPendingStateChange();

        var layouts = MeasureRequiredScenes(availableSize);
        MeasurePresentedContainers();
        return GetDesiredSize(layouts);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        ApplyPendingStateChange();

        var layouts = CaptureRequiredLayouts(finalSize);
        var targetLayout = GetStateLayout(_selectedState, layouts) ?? new MotionLayout(default, new Dictionary<string, MotionLayoutEntry>());
        var presentedLayout = ResolvePresentedLayout(targetLayout, layouts);

        ApplyLayout(presentedLayout);
        _presentedLayout = presentedLayout;
        _lastTargetLayout = targetLayout;
        _hasPresentedLayout = true;

        return finalSize;
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                AddChildren(e.NewStartingIndex, e.NewItems!);
                break;
            case NotifyCollectionChangedAction.Remove:
                RemoveChildren(e.OldStartingIndex, e.OldItems!);
                break;
            case NotifyCollectionChangedAction.Replace:
                RemoveChildren(e.OldStartingIndex, e.OldItems!);
                AddChildren(e.NewStartingIndex, e.NewItems!);
                break;
            case NotifyCollectionChangedAction.Move:
                LogicalChildren.MoveRange(
                    e.OldStartingIndex,
                    e.OldItems!.Count,
                    e.NewStartingIndex);
                VisualChildren.MoveRange(
                    e.OldStartingIndex,
                    e.OldItems.Count,
                    e.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Reset:
                ResetChildren();
                break;
        }

        _targetMapDirty = true;
        _stateChangePending = true;
        InvalidateMeasure();
    }

    private void AddChildren(int index, IList items)
    {
        foreach (var child in items.OfType<Control>())
        {
            if (_containers.ContainsKey(child))
            {
                throw new InvalidOperationException(
                    $"{nameof(TransitionPanel)} cannot contain the same child more than once.");
            }

            var container = new TransitionPanelItemContainer(child);
            _containers.Add(child, container);
            child.PropertyChanged += OnChildPropertyChanged;
            LogicalChildren.Insert(index, child);
            VisualChildren.Insert(index, container);
            index++;
        }
    }

    private void RemoveChildren(int index, IList items)
    {
        foreach (var child in items.OfType<Control>())
        {
            child.PropertyChanged -= OnChildPropertyChanged;

            if (_containers.Remove(child, out var container))
            {
                VisualChildren.Remove(container);
                container.DetachChild();
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
        }

        _containers.Clear();
        AddChildren(0, Children);
    }

    private void OnChildPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NameProperty)
        {
            _targetMapDirty = true;
            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        RequestNextFrame();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel = null;
        // Preserve accumulated progress, but discard the root-specific timestamp so time
        // spent outside a visual tree is not counted when the animation resumes.
        _lastFrameTime = null;
        _frameGeneration++;
        _frameRequested = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnStatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeScenes();

        if (_selectedState is not null && !_scenes.ContainsKey(_selectedState))
        {
            _selectedState = null;
            _settledState = null;
            _suppressLayoutTransitionOnce = true;
            ClearTransition();
        }
        else if (_transitionKind == TransitionKind.State && _stateSourceWeights.Keys.Any(state => !_scenes.ContainsKey(state)))
        {
            _settledState = _selectedState;
            _suppressLayoutTransitionOnce = true;
            ClearTransition();
        }

        _stateChangePending = true;
        InvalidateMeasure();
    }

    private void OnStatePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TransitionPanelState.TemplateProperty)
        {
            _scenes[(TransitionPanelState)sender!].Rebuild();
        }

        _stateChangePending = true;
        InvalidateMeasure();
    }

    private void OnRequestedStateChanged()
    {
        _stateChangePending = true;
        InvalidateMeasure();
    }

    private void OnStateTransitionSettingsChanged()
    {
        if (_transitionKind != TransitionKind.State || StateTransitionEasing is not null && StateTransitionDuration > TimeSpan.Zero)
        {
            return;
        }

        _settledState = _selectedState;
        _suppressLayoutTransitionOnce = true;
        ClearTransition();
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
            var scene = new TransitionPanelHost(this, state);
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
                throw new InvalidOperationException(
                    $"Every direct child of {nameof(TransitionPanel)} must have a non-empty x:Name.");
            }

            if (!_targets.TryAdd(id, new TargetEntry(child, _containers[child])))
            {
                throw new InvalidOperationException(
                    $"{nameof(TransitionPanel)} contains more than one direct child named '{id}'.");
            }
        }

        _targetMapDirty = false;
    }

    private void ApplyPendingStateChange()
    {
        if (!_stateChangePending)
        {
            return;
        }

        _stateChangePending = false;
        var requested = FindRequestedState();
        if (ReferenceEquals(requested, _selectedState))
        {
            return;
        }

        if (!_hasPresentedLayout ||
            requested is null ||
            _selectedState is null ||
            StateTransitionEasing is null ||
            StateTransitionDuration <= TimeSpan.Zero)
        {
            _selectedState = requested;
            _settledState = requested;
            _suppressLayoutTransitionOnce = true;
            ClearTransition();
            return;
        }

        PrepareStateSource();
        _selectedState = requested;
        _transitionKind = TransitionKind.State;
        RestartTransitionClock();
        RequestNextFrame();
    }

    private TransitionPanelState? FindRequestedState()
    {
        TransitionPanelState? match = null;
        foreach (var candidate in States)
        {
            if (!Equals(candidate.Key, State))
            {
                continue;
            }

            if (match is not null)
            {
                throw new InvalidOperationException(
                    $"{nameof(TransitionPanel)} contains multiple states with key '{State}'.");
            }

            match = candidate;
        }

        return match;
    }

    private void PrepareStateSource()
    {
        if (_transitionKind == TransitionKind.State)
        {
            if (_fixedStateSource is not null)
            {
                _fixedStateSource = _presentedLayout;
                _stateSourceWeights.Clear();
                return;
            }

            var progress = GetTransitionProgress(StateTransitionDuration);
            var eased = StateTransitionEasing!.Ease(progress);
            _stateWeightBuffer.Clear();
            foreach (var pair in _stateSourceWeights)
            {
                _stateWeightBuffer.Add(pair);
            }

            _stateSourceWeights.Clear();

            foreach (var pair in _stateWeightBuffer)
            {
                _stateSourceWeights[pair.Key] = pair.Value * (1 - eased);
            }

            if (_selectedState is not null)
            {
                _stateSourceWeights[_selectedState] =
                    _stateSourceWeights.GetValueOrDefault(_selectedState) + eased;
            }

            return;
        }

        _fixedStateSource = null;
        _stateSourceWeights.Clear();

        if (_transitionKind == TransitionKind.Layout && _presentedLayout is not null)
        {
            _fixedStateSource = _presentedLayout;
            return;
        }

        if (_settledState is not null)
        {
            _stateSourceWeights[_settledState] = 1;
        }
        else if (_selectedState is not null)
        {
            _stateSourceWeights[_selectedState] = 1;
        }
    }

    private Dictionary<TransitionPanelState, Size> MeasureRequiredScenes(Size availableSize)
    {
        CollectRequiredStates();
        _measuredSizes.Clear();

        foreach (var state in _requiredStates)
        {
            var scene = _scenes[state];
            scene.Measure(availableSize);
            _measuredSizes.Add(state, scene.DesiredSize);
        }

        return _measuredSizes;
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

    private Dictionary<TransitionPanelState, MotionLayout> CaptureRequiredLayouts(Size finalSize)
    {
        CollectRequiredStates();
        _arrangedLayouts.Clear();

        foreach (var state in _requiredStates)
        {
            var scene = _scenes[state];
            scene.Arrange(new Rect(finalSize));
            _arrangedLayouts.Add(state, scene.CaptureLayout());
        }

        return _arrangedLayouts;
    }

    private void CollectRequiredStates()
    {
        _requiredStates.Clear();

        if (_selectedState is not null)
        {
            _requiredStates.Add(_selectedState);
        }

        if (_transitionKind == TransitionKind.State && _fixedStateSource is null)
        {
            foreach (var state in _stateSourceWeights.Keys)
            {
                if (!_requiredStates.Contains(state))
                {
                    _requiredStates.Add(state);
                }
            }
        }
    }

    private Size GetDesiredSize(IReadOnlyDictionary<TransitionPanelState, Size> sizes)
    {
        var target = _selectedState is not null && sizes.TryGetValue(_selectedState, out var selectedSize)
            ? selectedSize
            : default;
        if (_transitionKind != TransitionKind.State)
        {
            return target;
        }

        var source = _fixedStateSource?.DesiredSize ?? MixDesiredSizes(sizes, _stateSourceWeights);
        var progress = StateTransitionEasing!.Ease(GetTransitionProgress(StateTransitionDuration));
        return Lerp(source, target, progress);
    }

    private MotionLayout ResolvePresentedLayout(MotionLayout target, IReadOnlyDictionary<TransitionPanelState, MotionLayout> layouts)
    {
        if (_transitionKind == TransitionKind.State)
        {
            var source = _fixedStateSource ?? MixStateLayouts(layouts, _stateSourceWeights);
            var progress = StateTransitionEasing!.Ease(GetTransitionProgress(StateTransitionDuration));
            return BlendLayouts(source, target, progress);
        }

        if (_suppressLayoutTransitionOnce)
        {
            _suppressLayoutTransitionOnce = false;
            _transitionKind = TransitionKind.None;
            _settledState = _selectedState;
            return target;
        }

        if (!_hasPresentedLayout || LayoutTransitionEasing is null || LayoutTransitionDuration <= TimeSpan.Zero)
        {
            _transitionKind = TransitionKind.None;
            _settledState = _selectedState;
            return target;
        }

        if (_lastTargetLayout is not null && !LayoutsEqual(_lastTargetLayout, target))
        {
            _layoutSource = _presentedLayout;
            _transitionKind = TransitionKind.Layout;
            RestartTransitionClock();
            RequestNextFrame();
        }

        if (_transitionKind != TransitionKind.Layout || _layoutSource is null)
        {
            return target;
        }

        var layoutProgress = LayoutTransitionEasing.Ease(GetTransitionProgress(LayoutTransitionDuration));
        return BlendLayouts(_layoutSource, target, layoutProgress);
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

    private void RequestNextFrame()
    {
        if (_frameRequested || _transitionKind == TransitionKind.None || _topLevel is null)
        {
            return;
        }

        _frameRequested = true;
        var generation = _frameGeneration;
        _topLevel.RequestAnimationFrame(timestamp => OnAnimationFrame(timestamp, generation));
    }

    private void OnAnimationFrame(TimeSpan timestamp, int generation)
    {
        if (generation != _frameGeneration)
        {
            return;
        }

        _frameRequested = false;
        if (_lastFrameTime is { } lastFrameTime && timestamp > lastFrameTime)
        {
            _transitionElapsed += timestamp - lastFrameTime;
        }

        _lastFrameTime = timestamp;
        if (IsTransitionComplete())
        {
            _suppressLayoutTransitionOnce = _transitionKind == TransitionKind.State;
            _settledState = _selectedState;
            ClearTransition();
        }

        InvalidateMeasure();
        RequestNextFrame();
    }

    private bool IsTransitionComplete()
    {
        if (_transitionKind == TransitionKind.None)
        {
            return false;
        }

        var duration = _transitionKind == TransitionKind.State ? StateTransitionDuration : LayoutTransitionDuration;
        return duration <= TimeSpan.Zero || _transitionElapsed >= duration;
    }

    private double GetTransitionProgress(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 0;
        }

        return Math.Clamp(_transitionElapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
    }

    private void ClearTransition()
    {
        _transitionKind = TransitionKind.None;
        RestartTransitionClock();
        _fixedStateSource = null;
        _layoutSource = null;
        _stateSourceWeights.Clear();
    }

    private void RestartTransitionClock()
    {
        _transitionElapsed = TimeSpan.Zero;
        _lastFrameTime = null;
    }

    private static MotionLayout? GetStateLayout(TransitionPanelState? state, IReadOnlyDictionary<TransitionPanelState, MotionLayout> layouts) =>
        state is not null && layouts.TryGetValue(state, out var result) ? result : null;

    private static Size MixDesiredSizes(
        IReadOnlyDictionary<TransitionPanelState, Size> sizes,
        IReadOnlyDictionary<TransitionPanelState, double> weights)
    {
        var width = 0d;
        var height = 0d;

        foreach (var pair in weights)
        {
            if (sizes.TryGetValue(pair.Key, out var size))
            {
                width += size.Width * pair.Value;
                height += size.Height * pair.Value;
            }
        }

        return new Size(width, height);
    }

    private MotionLayout MixStateLayouts(
        IReadOnlyDictionary<TransitionPanelState, MotionLayout> layouts,
        IReadOnlyDictionary<TransitionPanelState, double> weights)
    {
        var desiredWidth = 0d;
        var desiredHeight = 0d;
        var entries = new Dictionary<string, MotionLayoutEntry>(_targets.Count, StringComparer.Ordinal);

        foreach (var pair in weights)
        {
            if (layouts.TryGetValue(pair.Key, out var layout))
            {
                desiredWidth += layout.DesiredSize.Width * pair.Value;
                desiredHeight += layout.DesiredSize.Height * pair.Value;
            }
        }

        foreach (var id in _targets.Keys)
        {
            var rectWeight = 0d;
            var x = 0d;
            var y = 0d;
            var width = 0d;
            var height = 0d;
            var opacity = 0d;

            foreach (var pair in weights)
            {
                if (!layouts.TryGetValue(pair.Key, out var layout) ||
                    !layout.Entries.TryGetValue(id, out var entry))
                {
                    continue;
                }

                rectWeight += pair.Value;
                x += entry.Bounds.X * pair.Value;
                y += entry.Bounds.Y * pair.Value;
                width += entry.Bounds.Width * pair.Value;
                height += entry.Bounds.Height * pair.Value;
                opacity += entry.Opacity * pair.Value;
            }

            if (rectWeight > 0)
            {
                entries.Add(
                    id,
                    new MotionLayoutEntry(
                        new Rect(
                            x / rectWeight,
                            y / rectWeight,
                            width / rectWeight,
                            height / rectWeight),
                        opacity));
            }
        }

        return new MotionLayout(new Size(desiredWidth, desiredHeight), entries);
    }

    private MotionLayout BlendLayouts(MotionLayout from, MotionLayout to, double progress)
    {
        var entries = new Dictionary<string, MotionLayoutEntry>(_targets.Count, StringComparer.Ordinal);

        foreach (var id in _targets.Keys)
        {
            var hasFrom = from.Entries.TryGetValue(id, out var fromEntry);
            var hasTo = to.Entries.TryGetValue(id, out var toEntry);

            if (!hasFrom)
            {
                fromEntry = toEntry with { Opacity = 0 };
            }

            if (!hasTo)
            {
                toEntry = fromEntry with { Opacity = 0 };
            }

            entries.Add(
                id,
                new MotionLayoutEntry(
                    Lerp(fromEntry.Bounds, toEntry.Bounds, progress),
                    Lerp(fromEntry.Opacity, toEntry.Opacity, progress)));
        }

        return new MotionLayout(Lerp(from.DesiredSize, to.DesiredSize, progress), entries);
    }

    private static bool LayoutsEqual(MotionLayout left, MotionLayout right)
    {
        if (!Close(left.DesiredSize.Width, right.DesiredSize.Width) ||
            !Close(left.DesiredSize.Height, right.DesiredSize.Height) ||
            left.Entries.Count != right.Entries.Count)
        {
            return false;
        }

        foreach (var pair in left.Entries)
        {
            if (!right.Entries.TryGetValue(pair.Key, out var other) ||
                !Close(pair.Value.Bounds.X, other.Bounds.X) ||
                !Close(pair.Value.Bounds.Y, other.Bounds.Y) ||
                !Close(pair.Value.Bounds.Width, other.Bounds.Width) ||
                !Close(pair.Value.Bounds.Height, other.Bounds.Height) ||
                !Close(pair.Value.Opacity, other.Opacity))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Close(double left, double right) => Math.Abs(left - right) < 0.01;

    private static double Lerp(double from, double to, double progress) =>
        from + (to - from) * progress;

    private static Size Lerp(Size from, Size to, double progress) =>
        new(Lerp(from.Width, to.Width, progress), Lerp(from.Height, to.Height, progress));

    private static Rect Lerp(Rect from, Rect to, double progress) => new(
        Lerp(from.X, to.X, progress),
        Lerp(from.Y, to.Y, progress),
        Lerp(from.Width, to.Width, progress),
        Lerp(from.Height, to.Height, progress));

    private enum TransitionKind
    {
        None,
        State,
        Layout,
    }

    private readonly record struct TargetEntry(Control Child, TransitionPanelItemContainer Container);
}