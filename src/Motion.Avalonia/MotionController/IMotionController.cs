namespace Motion.Avalonia;

/// <summary>
/// Produces the current live pose for a <see cref="MotionPanel"/>.
/// </summary>
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