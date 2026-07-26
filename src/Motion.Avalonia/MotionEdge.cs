using Avalonia;
using Avalonia.Animation.Easings;

namespace Motion.Avalonia;

/// <summary>
/// Overrides the timing used for one directed state change.
/// </summary>
public sealed class MotionEdge : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="From"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> FromProperty =
        AvaloniaProperty.Register<MotionEdge, object?>(nameof(From));

    /// <summary>
    /// Defines the <see cref="To"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> ToProperty =
        AvaloniaProperty.Register<MotionEdge, object?>(nameof(To));

    /// <summary>
    /// Defines the <see cref="Duration"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<MotionEdge, TimeSpan?>(
            nameof(Duration),
            validate: static value => value is null || value >= TimeSpan.Zero);

    /// <summary>
    /// Defines the <see cref="Easing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> EasingProperty =
        AvaloniaProperty.Register<MotionEdge, Easing?>(nameof(Easing));

    /// <summary>
    /// Gets or sets the source state key.
    /// </summary>
    public object? From
    {
        get => GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    /// <summary>
    /// Gets or sets the destination state key.
    /// </summary>
    public object? To
    {
        get => GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    /// <summary>
    /// Gets or sets the duration override. A null value inherits the controller duration.
    /// </summary>
    public TimeSpan? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets the easing override. A null value inherits the controller easing.
    /// </summary>
    public Easing? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }
}