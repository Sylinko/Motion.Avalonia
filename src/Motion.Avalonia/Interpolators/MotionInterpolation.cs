using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Data;

namespace Motion.Avalonia;

/// <summary>
/// Selects timing and value interpolation for one motion property.
/// </summary>
/// <remarks>
/// Global entries match by <see cref="Target"/> and <see cref="Property"/>. Entries attached to
/// a <see cref="MotionSlot"/> already have an implicit target and normally specify only the
/// property. A missing easing falls through to the next scope and ultimately resolves to linear.
/// </remarks>
public sealed class MotionInterpolation : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="Target"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TargetProperty =
        AvaloniaProperty.Register<MotionInterpolation, string?>(nameof(Target));

    /// <summary>
    /// Defines the <see cref="Property"/> property.
    /// </summary>
    public static readonly StyledProperty<AvaloniaProperty?> PropertyProperty =
        AvaloniaProperty.Register<MotionInterpolation, AvaloniaProperty?>(nameof(Property));

    /// <summary>
    /// Defines the <see cref="Easing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> EasingProperty =
        AvaloniaProperty.Register<MotionInterpolation, Easing?>(nameof(Easing));

    /// <summary>
    /// Defines the <see cref="Interpolator"/> property.
    /// </summary>
    public static readonly StyledProperty<MotionInterpolator?> InterpolatorProperty =
        AvaloniaProperty.Register<MotionInterpolation, MotionInterpolator?>(nameof(Interpolator));

    /// <summary>
    /// Gets or sets the target name. Omit it for a slot-local or panel-wide entry.
    /// </summary>
    public string? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Gets or sets the target property or built-in layout channel.
    /// </summary>
    public AvaloniaProperty? Property
    {
        get => GetValue(PropertyProperty);
        set => SetValue(PropertyProperty, value);
    }

    /// <summary>
    /// Gets or sets the temporal progress mapping.
    /// </summary>
    public Easing? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }

    /// <summary>
    /// Gets or sets the value-space interpolation algorithm.
    /// </summary>
    public MotionInterpolator? Interpolator
    {
        get => GetValue(InterpolatorProperty);
        set => SetValue(InterpolatorProperty, value);
    }

    internal MotionInterpolationMetadata GetEndpointMetadata()
    {
        var easing = IsSet(EasingProperty) ? new Optional<Easing?>(Easing) : default;
        var interpolator = IsSet(InterpolatorProperty) ? new Optional<MotionInterpolator?>(Interpolator) : default;
        return new MotionInterpolationMetadata(easing, interpolator);
    }
}