using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Interpolates points along one cubic Bézier curve.
/// </summary>
/// <remarks>
/// The source and destination values are the first and last Bézier points. The two configured
/// vectors locate the inner control points relative to those live endpoints:
/// <c>P1 = source + SourceControlOffset</c> and
/// <c>P2 = destination + TargetControlOffset</c>.
/// Register this interpolator for <see cref="MotionLayout.PositionProperty"/> to move a layout
/// target along a curved path while its size continues through the normal size interpolator.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// <motion:CubicBezierMotionInterpolator
///     Target="Satellite"
///     Property="(motion:MotionLayout.Position)"
///     SourceControlOffset="120,-80"
///     TargetControlOffset="-96,64"/>
/// ]]></code>
/// </example>
public sealed class CubicBezierMotionInterpolator : MotionInterpolator<Point>
{
    /// <summary>
    /// Defines the <see cref="SourceControlOffset"/> property.
    /// </summary>
    public static readonly StyledProperty<Vector> SourceControlOffsetProperty =
        AvaloniaProperty.Register<CubicBezierMotionInterpolator, Vector>(
            nameof(SourceControlOffset),
            validate: static value => IsFinite(value));

    /// <summary>
    /// Defines the <see cref="TargetControlOffset"/> property.
    /// </summary>
    public static readonly StyledProperty<Vector> TargetControlOffsetProperty =
        AvaloniaProperty.Register<CubicBezierMotionInterpolator, Vector>(
            nameof(TargetControlOffset),
            validate: static value => IsFinite(value));

    /// <summary>
    /// Gets or sets the offset from the source point to the first control point.
    /// </summary>
    public Vector SourceControlOffset
    {
        get => GetValue(SourceControlOffsetProperty);
        set => SetValue(SourceControlOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the offset from the destination point to the second control point.
    /// </summary>
    public Vector TargetControlOffset
    {
        get => GetValue(TargetControlOffsetProperty);
        set => SetValue(TargetControlOffsetProperty, value);
    }

    /// <inheritdoc />
    public override Point Interpolate(double progress, Point oldValue, Point newValue)
    {
        var inverse = 1 - progress;
        var sourceControl = oldValue + SourceControlOffset;
        var targetControl = newValue + TargetControlOffset;
        var sourceWeight = inverse * inverse * inverse;
        var sourceControlWeight = 3 * inverse * inverse * progress;
        var targetControlWeight = 3 * inverse * progress * progress;
        var targetWeight = progress * progress * progress;

        return new Point(
            (sourceWeight * oldValue.X) +
            (sourceControlWeight * sourceControl.X) +
            (targetControlWeight * targetControl.X) +
            (targetWeight * newValue.X),
            (sourceWeight * oldValue.Y) +
            (sourceControlWeight * sourceControl.Y) +
            (targetControlWeight * targetControl.Y) +
            (targetWeight * newValue.Y));
    }

    private static bool IsFinite(Vector value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y);
}
