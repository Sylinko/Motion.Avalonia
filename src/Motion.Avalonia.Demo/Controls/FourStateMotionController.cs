using Avalonia;
using Avalonia.Animation.Easings;

namespace Motion.Avalonia.Demo.Controls;

public sealed class FourStateMotionController : MotionController
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<FourStateMotionController, double>(
            nameof(Progress),
            validate: static value => double.IsFinite(value),
            coerce: static (_, value) => Math.Clamp(value, 0, 3));

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<FourStateMotionController, TimeSpan>(
            nameof(Duration),
            TimeSpan.FromMilliseconds(450),
            validate: static value => value >= TimeSpan.Zero);

    public static readonly StyledProperty<Easing> EasingProperty =
        AvaloniaProperty.Register<FourStateMotionController, Easing>(
            nameof(Easing),
            new CubicEaseInOut());

    private static readonly string[] StateKeys = ["Orbit", "Columns", "Focus", "Stack"];
    private static readonly Easing PoseEasing = new LinearEasing();

    static FourStateMotionController()
    {
        ProgressProperty.Changed.AddClassHandler<FourStateMotionController>(
            static (controller, _) => controller.OnProgressChanged());
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public Easing Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }

    private double _animationSource;
    private double _animationTarget;
    private TimeSpan _elapsed;
    private TimeSpan? _lastFrameTime;
    private bool _isAnimating;
    private bool _frameRequested;
    private bool _isWritingAnimatedProgress;
    private int _frameGeneration;

    public FourStateMotionController()
    {
        PublishPose();
    }

    public void AnimateTo(int stateIndex)
    {
        var target = Math.Clamp(stateIndex, 0, StateKeys.Length - 1);
        if (Math.Abs(Progress - target) < 0.0001)
        {
            return;
        }

        StopAnimation();
        if (Duration <= TimeSpan.Zero)
        {
            SetProgress(target);
            return;
        }

        _animationSource = Progress;
        _animationTarget = target;
        _elapsed = TimeSpan.Zero;
        _lastFrameTime = null;
        _isAnimating = true;
        RequestNextFrame();
    }

    protected override void OnOwnerAttachedToVisualTree() => RequestNextFrame();

    protected override void OnOwnerDetachedFromVisualTree()
    {
        if (_isAnimating)
        {
            var target = _animationTarget;
            StopAnimation();
            SetProgress(target);
        }
    }

    private void OnProgressChanged()
    {
        if (!_isWritingAnimatedProgress)
        {
            StopAnimation();
        }

        PublishPose();
    }

    private void PublishPose()
    {
        var progress = Math.Clamp(Progress, 0, StateKeys.Length - 1);
        var sourceIndex = (int)Math.Floor(progress);
        var targetIndex = (int)Math.Ceiling(progress);

        if (sourceIndex == targetIndex)
        {
            SetCurrentPose(MotionPose.State(StateKeys[sourceIndex]));
            return;
        }

        SetCurrentPose(
            MotionPose.Blend(
                MotionPose.State(StateKeys[sourceIndex]),
                MotionPose.State(StateKeys[targetIndex]),
                progress - sourceIndex,
                PoseEasing));
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
        if (generation != _frameGeneration || !_isAnimating)
        {
            return;
        }

        _frameRequested = false;
        if (_lastFrameTime is { } lastFrameTime && timestamp > lastFrameTime)
        {
            _elapsed += timestamp - lastFrameTime;
        }

        _lastFrameTime = timestamp;
        var rawProgress = Math.Clamp(_elapsed.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
        var progress = Easing.Ease(rawProgress);
        SetProgress(_animationSource + ((_animationTarget - _animationSource) * progress));

        if (rawProgress >= 1)
        {
            StopAnimation();
            return;
        }

        RequestNextFrame();
    }

    private void SetProgress(double progress)
    {
        _isWritingAnimatedProgress = true;
        try
        {
            SetCurrentValue(ProgressProperty, progress);
        }
        finally
        {
            _isWritingAnimatedProgress = false;
        }
    }

    private void StopAnimation()
    {
        if (_frameRequested)
        {
            _frameGeneration++;
        }

        _frameRequested = false;
        _isAnimating = false;
        _elapsed = TimeSpan.Zero;
        _lastFrameTime = null;
    }
}
