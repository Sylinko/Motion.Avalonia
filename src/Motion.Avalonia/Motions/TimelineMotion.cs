using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Collections;
using Avalonia.Metadata;
using Motion.Avalonia.Utilities;

namespace Motion.Avalonia;

/// <summary>
/// Evaluates sparse keyframes at absolute times and optionally advances them with a player.
/// </summary>
public sealed class TimelineMotion : PlayerDrivenMotion, IKeyFrameMotion
{
    /// <summary>
    /// Defines the <see cref="Time"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> TimeProperty =
        AvaloniaProperty.Register<TimelineMotion, TimeSpan>(
            nameof(Time),
            validate: static value => value >= TimeSpan.Zero);

    /// <summary>
    /// Defines the <see cref="Loop"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> LoopProperty =
        AvaloniaProperty.Register<TimelineMotion, bool>(nameof(Loop));

    /// <summary>
    /// Defines the <see cref="Duration"/> property.
    /// </summary>
    public static readonly DirectProperty<TimelineMotion, TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterDirect<TimelineMotion, TimeSpan>(
            nameof(Duration),
            motion => motion.Duration);

    static TimelineMotion()
    {
        AffectsMotion<TimelineMotion>(EasingProperty);
    }

    /// <summary>
    /// Gets or sets the absolute evaluation time.
    /// </summary>
    public TimeSpan Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether player-driven playback wraps at <see cref="Duration"/>.
    /// </summary>
    public bool Loop
    {
        get => GetValue(LoopProperty);
        set => SetValue(LoopProperty, value);
    }

    /// <summary>
    /// Gets the duration derived from the latest keyframe.
    /// </summary>
    public TimeSpan Duration
    {
        get;
        private set => SetAndRaise(DurationProperty, ref field, value);
    }

    /// <summary>
    /// Gets the sparse absolute-time keyframes.
    /// </summary>
    [Content]
    public AvaloniaList<TimelineMotionKeyFrame> KeyFrames => _keyFrames;

    /// <inheritdoc/>
    IEnumerable<MotionKeyFrame> IKeyFrameMotion.KeyFrames => _keyFrames.OrderBy(keyFrame => keyFrame.KeyTime);

    private readonly LogicalChildrenList<TimelineMotionKeyFrame> _keyFrames;
    private int _initializationDepth;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineMotion"/> class.
    /// </summary>
    public TimelineMotion()
    {
        _keyFrames = new LogicalChildrenList<TimelineMotionKeyFrame>(this);
        _keyFrames.CollectionChanged += HandleKeyFramesChanged;
        Player.PropertyChanged += HandlePlayerPropertyChanged;
        Player.Tick += HandlePlayerTick;
    }

    /// <inheritdoc />
    public override void BeginInit()
    {
        base.BeginInit();
        _initializationDepth++;
    }

    /// <inheritdoc />
    public override void EndInit()
    {
        if (_initializationDepth == 0)
        {
            base.EndInit();
            return;
        }

        if (_initializationDepth == 1)
        {
            InvalidateMotion();
            UpdateDuration();
        }

        _initializationDepth--;
        base.EndInit();
    }

    /// <inheritdoc />
    protected override MotionRuntime CreateRuntime(MotionCompilationContext context)
    {
        UpdateDuration();
        return new KeyFrameMotionRuntime<TimelineMotion>(context, this);
    }

    private void HandlePlayerTick(MotionPlayer sender, TimeSpan time, TimeSpan deltaTime)
    {
        if (Duration <= TimeSpan.Zero)
        {
            Player.Stop();
            return;
        }

        var nextTime = Time + deltaTime;
        if (nextTime < Duration)
        {
            if (deltaTime > TimeSpan.Zero)
            {
                SetCurrentValue(TimeProperty, nextTime);
            }

            return;
        }

        if (Loop)
        {
            SetCurrentValue(TimeProperty, TimeSpan.FromTicks(nextTime.Ticks % Duration.Ticks));
            return;
        }

        SetCurrentValue(TimeProperty, Duration);
        Player.Stop();
    }

    Cue IKeyFrameMotion.GetCue(MotionKeyFrame keyFrame)
    {
        if (keyFrame is not TimelineMotionKeyFrame timelineKeyFrame)
        {
            throw new ArgumentException($"{nameof(TimelineMotion)} can only resolve {nameof(TimelineMotionKeyFrame)} cues.", nameof(keyFrame));
        }

        var cue = Duration > TimeSpan.Zero ? timelineKeyFrame.KeyTime.TotalSeconds / Duration.TotalSeconds : 0;
        return new Cue(Math.Clamp(cue, 0, 1));
    }

    IDisposable IObservable<double>.Subscribe(IObserver<double> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return this.GetObservable(TimeProperty, GetProgress).Subscribe(observer);
    }

    private void HandlePlayerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MotionPlayer.StateProperty && Player.State == MotionPlayerState.Playing && Duration > TimeSpan.Zero && Time >= Duration)
        {
            SetCurrentValue(TimeProperty, TimeSpan.Zero);
        }
    }

    private double GetProgress(TimeSpan time)
    {
        if (Duration <= TimeSpan.Zero)
        {
            return 0;
        }

        return Math.Clamp(time.TotalSeconds / Duration.TotalSeconds, 0, 1);
    }

    private void HandleKeyFramesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_initializationDepth > 0)
        {
            return;
        }

        InvalidateMotion();
        UpdateDuration();
    }

    private void UpdateDuration()
    {
        Duration = _keyFrames.Max(keyFrame => keyFrame.KeyTime);
    }
}