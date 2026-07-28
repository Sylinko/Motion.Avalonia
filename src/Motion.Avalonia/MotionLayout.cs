using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Identifies the built-in layout channels evaluated by <see cref="MotionPanel"/>.
/// </summary>
/// <remarks>
/// These attached properties are strongly typed interpolation keys rather than values that
/// applications set on a control. Use them as <see cref="MotionInterpolator.Property"/> values
/// to customize panel desired size, target position, target size, or target presence with the
/// same resolver used by ordinary target properties.
/// </remarks>
public static class MotionLayout
{
    /// <summary>
    /// Identifies the panel-wide <see cref="Size"/> channel used during measure.
    /// </summary>
    public static readonly AttachedProperty<Size> DesiredSizeProperty =
        AvaloniaProperty.RegisterAttached<MotionPanel, Size>(
            "DesiredSize",
            typeof(MotionLayout));

    /// <summary>
    /// Identifies the per-target <see cref="Point"/> channel used during arrange.
    /// </summary>
    public static readonly AttachedProperty<Point> PositionProperty =
        AvaloniaProperty.RegisterAttached<MotionPanelItemContainer, Point>(
            "Position",
            typeof(MotionLayout));

    /// <summary>
    /// Identifies the per-target <see cref="Size"/> channel used during measure and arrange.
    /// </summary>
    public static readonly AttachedProperty<Size> SizeProperty =
        AvaloniaProperty.RegisterAttached<MotionPanelItemContainer, Size>(
            "Size",
            typeof(MotionLayout));

    /// <summary>
    /// Identifies the per-target presence channel used when a target enters or leaves a state.
    /// </summary>
    public static readonly AttachedProperty<double> PresenceProperty =
        AvaloniaProperty.RegisterAttached<MotionPanelItemContainer, double>(
            "Presence",
            typeof(MotionLayout));

    internal static bool IsLayoutProperty(AvaloniaProperty property) =>
        property == DesiredSizeProperty ||
        property == PositionProperty ||
        property == SizeProperty ||
        property == PresenceProperty;

    internal static bool IsPanelProperty(AvaloniaProperty property) =>
        property == DesiredSizeProperty;
}
