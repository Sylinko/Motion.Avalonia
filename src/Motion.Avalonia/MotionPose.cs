using Avalonia.Animation.Easings;

namespace Motion.Avalonia;

/// <summary>
/// Describes a live state composition produced by an <see cref="IMotionController"/>.
/// </summary>
/// <remarks>
/// Poses refer to state keys rather than frozen coordinates. Blends may be nested, which lets
/// an interrupted transition continue from the exact pose currently on screen.
/// </remarks>
/// <example>
/// <code>
/// var pose = MotionPose.CreateBlend(
///     MotionPose.State("Compact"),
///     MotionPose.State("Expanded"),
///     progress,
///     new CubicEaseInOut(),
///     edge);
/// </code>
/// </example>
public abstract class MotionPose
{
    private static readonly Easing DefaultEasing = new LinearEasing();

    /// <summary>
    /// Gets the pose that presents no state.
    /// </summary>
    public static MotionPose Empty { get; } = new EmptyMotionPose();

    /// <summary>
    /// Creates a pose that presents one state.
    /// </summary>
    /// <param name="key">The state key.</param>
    public static MotionPose State(object? key) => new StateMotionPose(key);

    /// <summary>
    /// Creates a live blend between two poses.
    /// </summary>
    /// <param name="source">The source pose.</param>
    /// <param name="target">The target pose.</param>
    /// <param name="progress">The raw normalized progress.</param>
    /// <param name="easing">The default easing for the blend. A null value uses linear easing.</param>
    /// <param name="edge">The optional directed edge represented by the blend.</param>
    /// <returns>A pose whose progress is clamped to the inclusive range from zero to one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="target"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="progress"/> is not finite.</exception>
    public static MotionPose CreateBlend(
        MotionPose source,
        MotionPose target,
        double progress,
        Easing? easing = null,
        MotionEdge? edge = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (!double.IsFinite(progress))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be finite.");
        }

        return new BlendMotionPose(
            source,
            target,
            Math.Clamp(progress, 0, 1),
            easing ?? DefaultEasing,
            edge);
    }
}

internal sealed class EmptyMotionPose : MotionPose;

internal sealed class StateMotionPose(object? key) : MotionPose
{
    public object? Key { get; } = key;
}

internal sealed class BlendMotionPose(MotionPose source, MotionPose target, double progress, Easing easing, MotionEdge? edge) : MotionPose
{
    public MotionPose Source { get; } = source;

    public MotionPose Target { get; } = target;

    public double Progress { get; } = progress;

    public Easing Easing { get; } = easing;

    public MotionEdge? Edge { get; } = edge;
}