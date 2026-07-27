using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Metadata;
using Motion.Avalonia.Extensions;

namespace Motion.Avalonia;

/// <summary>
/// Maps absolute time across an ordered timeline of state keyframes.
/// </summary>
/// <remarks>
/// Set <see cref="Time"/> directly for scrubbing or use the controller-owned
/// <see cref="MotionController.Player"/> for frame-aligned playback. <see cref="Duration"/> is
/// derived from the latest keyframe. At an exact key time the controller publishes a state pose;
/// between keyframes it publishes a blend using the destination keyframe's easing. Equal key
/// times create an instantaneous cut, with the last declared keyframe winning at and after that
/// time.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// <motion:TimelineMotionController x:Name="Timeline" Time="{Binding Position}">
///   <motion:MotionKeyFrame State="Intro" KeyTime="0:0:0"/>
///   <motion:MotionKeyFrame State="Details" KeyTime="0:0:0.8" Easing="CubicEaseOut"/>
///   <motion:MotionKeyFrame State="Complete" KeyTime="0:0:2"/>
/// </motion:TimelineMotionController>
/// ]]></code>
/// Start authored playback with <c>Timeline.Player?.Play(Timeline.Duration)</c>.
/// </example>
public sealed class TimelineMotionController : MotionController
{
    /// <summary>
    /// Defines the <see cref="Time"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> TimeProperty =
        AvaloniaProperty.Register<TimelineMotionController, TimeSpan>(
            nameof(Time),
            validate: static value => value >= TimeSpan.Zero);

    /// <summary>
    /// Defines the <see cref="Duration"/> property.
    /// </summary>
    public static readonly DirectProperty<TimelineMotionController, TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterDirect<TimelineMotionController, TimeSpan>(
            nameof(Duration),
            controller => controller.Duration);

    static TimelineMotionController()
    {
        TimeProperty.Changed.AddClassHandler<TimelineMotionController>(static (controller, _) => controller.OnTimeChanged());
    }

    /// <summary>
    /// Gets or sets the absolute position evaluated by the timeline.
    /// Values after <see cref="Duration"/> hold the final keyframe.
    /// </summary>
    public TimeSpan Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <summary>
    /// Gets the timeline duration derived from the latest keyframe.
    /// </summary>
    public TimeSpan Duration
    {
        get;
        private set => SetAndRaise(DurationProperty, ref field, value);
    }

    /// <summary>
    /// Gets the state keyframes that define the timeline.
    /// </summary>
    [Content]
    public AvaloniaList<MotionKeyFrame> KeyFrames { get; } = [];

    private readonly HashSet<MotionKeyFrame> _attachedKeyFrames = [];
    private readonly List<MotionKeyFrame> _orderedKeyFrames = [];
    private bool _updatingPlayerFromTime;
    private bool _updatingTimeFromPlayer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineMotionController"/> class.
    /// </summary>
    public TimelineMotionController()
    {
        PlayerProgressChanged += OnPlayerProgressChanged;
        KeyFrames.CollectionChanged += OnKeyFramesChanged;
        PublishPose();
    }

    /// <inheritdoc />
    protected override void OnAttached() => SynchronizePlayerToTime();

    private void OnTimeChanged()
    {
        if (!_updatingTimeFromPlayer)
        {
            SynchronizePlayerToTime();
        }

        PublishPose();
    }

    private void OnPlayerProgressChanged(object? sender, EventArgs e)
    {
        if (_updatingPlayerFromTime || Player is not { } player)
        {
            return;
        }

        _updatingTimeFromPlayer = true;
        try
        {
            SetCurrentValue(TimeProperty, GetTime(player.Progress));
        }
        finally
        {
            _updatingTimeFromPlayer = false;
        }
    }

    private void OnKeyFramesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeKeyFrames();
        RebuildOrderedKeyFrames();
        OnTimelineStructureChanged();
    }

    private void SynchronizeKeyFrames()
    {
        foreach (var keyFrame in _attachedKeyFrames.Where(keyFrame => !KeyFrames.Contains(keyFrame)).ToArray())
        {
            keyFrame.PropertyChanged -= OnKeyFramePropertyChanged;
            _attachedKeyFrames.Remove(keyFrame);
        }

        foreach (var keyFrame in KeyFrames)
        {
            if (!_attachedKeyFrames.Add(keyFrame))
            {
                continue;
            }

            keyFrame.PropertyChanged += OnKeyFramePropertyChanged;
        }
    }

    private void OnKeyFramePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MotionKeyFrame.KeyTimeProperty)
        {
            RebuildOrderedKeyFrames();
            OnTimelineStructureChanged();
            return;
        }

        PublishPose();
    }

    private void RebuildOrderedKeyFrames()
    {
        _orderedKeyFrames.Clear();
        foreach (var keyFrame in KeyFrames)
        {
            MotionSegmentEvaluator.InsertOrdered(
                _orderedKeyFrames,
                keyFrame,
                static item => item.KeyTime.Ticks);
        }

        Duration = _orderedKeyFrames.Count == 0 ? TimeSpan.Zero : _orderedKeyFrames[^1].KeyTime;
    }

    private void OnTimelineStructureChanged()
    {
        Player?.Stop();
        if (IsAttached)
        {
            SynchronizePlayerToTime();
        }

        PublishPose();
    }

    private void SynchronizePlayerToTime()
    {
        if (Player is not { } player)
        {
            return;
        }

        var progress = GetProgress(Time);
        if (!player.Progress.IsCloseTo(progress, 1e-10))
        {
            _updatingPlayerFromTime = true;
            try
            {
                player.Seek(progress);
            }
            finally
            {
                _updatingPlayerFromTime = false;
            }
        }
    }

    private double GetProgress(TimeSpan time)
    {
        if (Duration <= TimeSpan.Zero || time <= TimeSpan.Zero)
        {
            return 0;
        }

        if (time >= Duration)
        {
            return 1;
        }

        return time.Ticks / (double)Duration.Ticks;
    }

    private TimeSpan GetTime(double progress)
    {
        if (Duration <= TimeSpan.Zero || progress <= 0)
        {
            return TimeSpan.Zero;
        }

        if (progress >= 1)
        {
            return Duration;
        }

        return TimeSpan.FromTicks((long)(Duration.Ticks * progress));
    }

    private void PublishPose() =>
        SetCurrentPose(
            MotionSegmentEvaluator.Evaluate(
                _orderedKeyFrames,
                Time.Ticks,
                static item => item.KeyTime.Ticks,
                static item => item.State,
                static item => item.Easing));
}