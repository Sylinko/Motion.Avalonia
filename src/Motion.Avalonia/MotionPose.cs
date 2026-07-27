using Avalonia.Animation.Easings;

namespace Motion.Avalonia;

/// <summary>
/// Describes a live state composition produced by an <see cref="IMotionController"/>.
/// </summary>
public abstract class MotionPose
{
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
    /// <param name="easing">The default easing for the blend.</param>
    public static MotionPose Blend(MotionPose source, MotionPose target, double progress, Easing? easing = null) =>
        CreateBlend(source, target, progress, easing ?? new LinearEasing(), null);

    internal static MotionPose CreateBlend(MotionPose source, MotionPose target, double progress, Easing easing, MotionEdge? edge)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(easing);

        if (!double.IsFinite(progress))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be finite.");
        }

        return new BlendMotionPose(source, target, Math.Clamp(progress, 0, 1), easing, edge);
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