using Avalonia;
using Avalonia.Animation.Easings;

namespace Motion.Avalonia;

/// <summary>
/// Describes one state keyframe in a <see cref="TimelineMotionController"/>.
/// </summary>
/// <remarks>
/// <see cref="Easing"/> belongs to the interval that arrives at this keyframe, not the interval
/// that leaves it. For example, given keyframes A, B, and C, the easing declared on B shapes the
/// A-to-B interval, while the easing declared on C shapes the B-to-C interval. Keyframes are
/// ordered by <see cref="KeyTime"/> while preserving declaration order for equal times.
/// </remarks>
/// <example>
/// The <c>Discussion</c> state is reached two seconds into the timeline:
/// <code><![CDATA[
/// <motion:MotionKeyFrame
///   State="Discussion"
///   KeyTime="0:0:2"
///   Easing="CubicEaseOut"/>
/// ]]></code>
/// </example>
public sealed class MotionKeyFrame : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> StateProperty =
        AvaloniaProperty.Register<MotionKeyFrame, object?>(nameof(State));

    /// <summary>
    /// Defines the <see cref="KeyTime"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> KeyTimeProperty =
        AvaloniaProperty.Register<MotionKeyFrame, TimeSpan>(
            nameof(KeyTime),
            validate: static value => value >= TimeSpan.Zero);

    /// <summary>
    /// Defines the <see cref="Easing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> EasingProperty =
        AvaloniaProperty.Register<MotionKeyFrame, Easing?>(nameof(Easing));

    /// <summary>
    /// Gets or sets the state presented at this keyframe.
    /// </summary>
    public object? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Gets or sets the absolute time of this keyframe.
    /// </summary>
    public TimeSpan KeyTime
    {
        get => GetValue(KeyTimeProperty);
        set => SetValue(KeyTimeProperty, value);
    }

    /// <summary>
    /// Gets or sets the easing used to approach this keyframe from the preceding keyframe.
    /// </summary>
    /// <remarks>
    /// Suppose the preceding keyframe is at time <c>t0</c>, this keyframe is at <c>t1</c>, and
    /// the current timeline time is <c>t</c>. The controller first calculates local progress as
    /// <c>(t - t0) / (t1 - t0)</c>, then applies this easing before interpolating the two states.
    ///
    /// This destination-owned convention means changing this value affects how motion arrives at
    /// <see cref="State"/>, without changing how motion leaves it toward the following keyframe.
    /// The first keyframe normally has no incoming interval, so its easing has no effect. An
    /// interval whose endpoints have the same <see cref="KeyTime"/> is an instantaneous cut and
    /// does not evaluate easing. A null value uses linear easing.
    /// </remarks>
    public Easing? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }
}