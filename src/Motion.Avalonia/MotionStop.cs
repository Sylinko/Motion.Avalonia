using Avalonia;
using Avalonia.Animation.Easings;

namespace Motion.Avalonia;

/// <summary>
/// Describes one state stop in a <see cref="SequenceMotionController"/>.
/// </summary>
/// <remarks>
/// <see cref="Easing"/> shapes the interval arriving at this stop, mirroring the way a keyframe
/// owns its incoming timing curve. Stops are ordered by <see cref="Offset"/> while preserving
/// declaration order for equal offsets.
/// </remarks>
/// <example>
/// The <c>Expanded</c> state is reached at 60% of the controller's normalized range:
/// <code><![CDATA[
/// <motion:MotionStop
///   State="Expanded"
///   Offset="0.6"
///   Easing="CubicEaseOut"/>
/// ]]></code>
/// </example>
public sealed class MotionStop : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> StateProperty =
        AvaloniaProperty.Register<MotionStop, object?>(nameof(State));

    /// <summary>
    /// Defines the <see cref="Offset"/> property.
    /// </summary>
    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<MotionStop, double>(
            nameof(Offset),
            validate: static value => double.IsFinite(value) && value is >= 0 and <= 1);

    /// <summary>
    /// Defines the <see cref="Easing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> EasingProperty =
        AvaloniaProperty.Register<MotionStop, Easing?>(nameof(Easing));

    /// <summary>
    /// Gets or sets the state presented at this stop.
    /// </summary>
    public object? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Gets or sets the normalized position of this stop in the sequence.
    /// </summary>
    public double Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the easing used by the interval that arrives at this stop.
    /// A null value uses linear easing.
    /// </summary>
    public Easing? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }
}