using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Transitions to a requested state using default timing and optional directed edge overrides.
/// </summary>
public sealed class BasicMotionController : MotionController
{
    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> StateProperty =
        AvaloniaProperty.Register<BasicMotionController, object?>(nameof(State));

    /// <summary>
    /// Defines the <see cref="Duration"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<BasicMotionController, TimeSpan>(
            nameof(Duration),
            TimeSpan.FromMilliseconds(300),
            validate: static value => value >= TimeSpan.Zero);

    /// <summary>
    /// Defines the <see cref="Easing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> EasingProperty =
        AvaloniaProperty.Register<BasicMotionController, Easing?>(nameof(Easing), new LinearEasing());

    static BasicMotionController()
    {
        StateProperty.Changed.AddClassHandler<BasicMotionController>(static (s, e) => s.OnStateChanged(e.OldValue, e.NewValue));
        DurationProperty.Changed.AddClassHandler<BasicMotionController>(static (s, _) => s.OnTimingChanged());
        EasingProperty.Changed.AddClassHandler<BasicMotionController>(static (s, _) => s.OnTimingChanged());
    }

    /// <summary>
    /// Gets or sets the requested state key.
    /// </summary>
    public object? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Gets or sets the default transition duration.
    /// </summary>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets the default transition easing. A null value disables animation.
    /// </summary>
    public Easing? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }

    /// <summary>
    /// Gets the directed state-change overrides.
    /// </summary>
    [Content]
    public AvaloniaList<MotionEdge> Edges { get; } = [];

    private MotionPose _source = MotionPose.Empty;
    private MotionPose _target = MotionPose.Empty;
    private Easing? _activeEasing;
    private TimeSpan _activeDuration;
    private TimeSpan _elapsed;
    private TimeSpan? _lastFrameTime;
    private bool _isAnimating;
    private bool _frameRequested;
    private bool _hasPresented;
    private int _frameGeneration;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMotionController"/> class.
    /// </summary>
    public BasicMotionController()
    {
        SetCurrentPose(MotionPose.State(State));
    }

    /// <inheritdoc />
    protected override void OnAttached() => _hasPresented = false;

    /// <inheritdoc />
    protected override void OnDetached() => _hasPresented = false;

    /// <inheritdoc />
    protected override void OnOwnerAttachedToVisualTree() => RequestNextFrame();

    /// <inheritdoc />
    protected override void OnOwnerPresented() => _hasPresented = true;

    /// <inheritdoc />
    protected override void OnOwnerDetachedFromVisualTree()
    {
        _frameGeneration++;
        _frameRequested = false;
        _lastFrameTime = null;

        if (_isAnimating)
        {
            CompleteTransition();
        }
    }

    private void OnStateChanged(object? oldState, object? newState)
    {
        if (!IsAttached || !_hasPresented)
        {
            StopTransition();
            SetCurrentPose(MotionPose.State(newState));
            return;
        }

        var edge = FindEdge(oldState, newState);
        var duration = edge?.Duration ?? Duration;
        var easing = edge?.Easing ?? Easing;

        if (duration <= TimeSpan.Zero || easing is null)
        {
            StopTransition();
            SetCurrentPose(MotionPose.State(newState));
            return;
        }

        _source = CurrentPose;
        _target = MotionPose.State(newState);
        _activeDuration = duration;
        _activeEasing = easing;
        _elapsed = TimeSpan.Zero;
        _lastFrameTime = null;
        _isAnimating = true;
        SetCurrentPose(MotionPose.CreateBlend(_source, _target, 0, easing, duration));
        RequestNextFrame();
    }

    private MotionEdge? FindEdge(object? from, object? to)
    {
        MotionEdge? result = null;
        foreach (var edge in Edges)
        {
            if (!Equals(edge.From, from) || !Equals(edge.To, to))
            {
                continue;
            }

            if (result is not null)
            {
                throw new InvalidOperationException($"{nameof(BasicMotionController)} contains more than one edge from '{from}' to '{to}'.");
            }

            result = edge;
        }

        return result;
    }

    private void OnTimingChanged()
    {
        if (!_isAnimating)
        {
            return;
        }

        if (Duration <= TimeSpan.Zero || Easing is null)
        {
            CompleteTransition();
        }
    }

    private void RequestNextFrame()
    {
        if (!_isAnimating || _frameRequested)
        {
            return;
        }

        var generation = _frameGeneration;
        _frameRequested = RequestAnimationFrame(timestamp => OnAnimationFrame(timestamp, generation));
    }

    private void OnAnimationFrame(TimeSpan timestamp, int generation)
    {
        if (generation != _frameGeneration || !_isAnimating || _activeEasing is null)
        {
            return;
        }

        _frameRequested = false;
        if (_lastFrameTime is { } lastFrameTime && timestamp > lastFrameTime)
        {
            _elapsed += timestamp - lastFrameTime;
        }

        _lastFrameTime = timestamp;
        var progress = _activeDuration <= TimeSpan.Zero ? 1 : Math.Clamp(_elapsed.TotalMilliseconds / _activeDuration.TotalMilliseconds, 0, 1);

        if (progress >= 1)
        {
            CompleteTransition();
            return;
        }

        SetCurrentPose(MotionPose.CreateBlend(_source, _target, progress, _activeEasing, _activeDuration));
        RequestNextFrame();
    }

    private void CompleteTransition()
    {
        var target = _target;
        StopTransition();
        SetCurrentPose(target);
    }

    private void StopTransition()
    {
        if (_frameRequested)
        {
            _frameGeneration++;
        }

        _frameRequested = false;
        _isAnimating = false;
        _elapsed = TimeSpan.Zero;
        _lastFrameTime = null;
        _source = MotionPose.Empty;
        _target = MotionPose.Empty;
        _activeEasing = null;
        _activeDuration = TimeSpan.Zero;
    }
}