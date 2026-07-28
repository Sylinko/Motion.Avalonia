using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Maps absolute time across an ordered timeline of state keyframes.
/// </summary>
/// <remarks>
/// Set <see cref="Time"/> directly for scrubbing or use the controller-owned
/// <see cref="PlayerDrivenMotionController.Player"/> for frame-aligned playback. <see cref="Duration"/> is
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
/// Start authored playback with <c>Timeline.Player.Play()</c>. The player's frame deltas advance
/// <see cref="Time"/> until the timeline reaches <see cref="Duration"/>.
/// </example>
public sealed class TimelineMotionController : PlayerDrivenMotionController
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineMotionController"/> class.
    /// </summary>
    public TimelineMotionController()
    {
        KeyFrames.CollectionChanged += OnKeyFramesChanged;
        PublishPose();
    }

    private void OnTimeChanged() => PublishPose();

    /// <inheritdoc />
    protected override void OnPlayerTick(TimeSpan time, TimeSpan deltaTime)
    {
        if (Duration <= TimeSpan.Zero || Time >= Duration)
        {
            Player.Stop();
            return;
        }

        var remaining = Duration - Time;
        if (deltaTime >= remaining)
        {
            SetCurrentValue(TimeProperty, Duration);
            Player.Stop();
            return;
        }

        if (deltaTime > TimeSpan.Zero)
        {
            SetCurrentValue(TimeProperty, Time + deltaTime);
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
        Player.Stop();
        PublishPose();
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