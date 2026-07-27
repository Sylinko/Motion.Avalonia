namespace Motion.Avalonia;

/// <summary>
/// Produces the current live pose for a <see cref="MotionPanel"/>.
/// </summary>
/// <remarks>
/// This contract intentionally contains no state, duration, easing, or playback API. A controller
/// may be driven by time, scrolling, a gesture, or a solver. Derive from
/// <see cref="MotionController"/> for the usual Avalonia lifecycle and frame scheduling support.
/// </remarks>
/// <example>
/// A minimal implementation publishes a state and raises the event when it changes:
/// <code><![CDATA[
/// public sealed class SelectionController : IMotionController
/// {
///     public MotionPose CurrentPose { get; private set; } = MotionPose.Empty;
///     public event EventHandler? CurrentPoseChanged;
///
///     public void Select(object? state)
///     {
///         CurrentPose = MotionPose.State(state);
///         CurrentPoseChanged?.Invoke(this, EventArgs.Empty);
///     }
/// }
/// ]]></code>
/// </example>
public interface IMotionController
{
    /// <summary>
    /// Gets the pose currently presented by the controller.
    /// </summary>
    MotionPose CurrentPose { get; }

    /// <summary>
    /// Occurs when <see cref="CurrentPose"/> changes.
    /// </summary>
    event EventHandler? CurrentPoseChanged;
}