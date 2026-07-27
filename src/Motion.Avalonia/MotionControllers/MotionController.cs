using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Provides Avalonia integration and animation-frame scheduling for motion controllers.
/// </summary>
public abstract class MotionController : StyledElement, IMotionController
{
    /// <summary>
    /// Defines the <see cref="CurrentPose"/> property.
    /// </summary>
    public static readonly DirectProperty<MotionController, MotionPose> CurrentPoseProperty =
        AvaloniaProperty.RegisterDirect<MotionController, MotionPose>(nameof(CurrentPose), controller => controller.CurrentPose);

    /// <inheritdoc />
    public MotionPose CurrentPose
    {
        get;
        private set => SetAndRaise(CurrentPoseProperty, ref field, value);
    } = MotionPose.Empty;

    /// <inheritdoc />
    public event EventHandler? CurrentPoseChanged;

    private MotionPanel? _owner;

    /// <summary>
    /// Gets whether this controller is currently assigned to a panel.
    /// </summary>
    protected bool IsAttached => _owner is not null;

    /// <summary>
    /// Publishes a new pose to the owning panel.
    /// </summary>
    /// <param name="pose">The new current pose.</param>
    protected void SetCurrentPose(MotionPose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);

        if (ReferenceEquals(CurrentPose, pose))
        {
            return;
        }

        CurrentPose = pose;
        CurrentPoseChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests a callback on the next animation frame of the owning panel.
    /// </summary>
    /// <param name="callback">The animation-frame callback.</param>
    /// <returns>True when a frame was scheduled; otherwise false.</returns>
    protected bool RequestAnimationFrame(Action<TimeSpan> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return _owner?.RequestControllerAnimationFrame(this, callback) == true;
    }

    /// <summary>
    /// Called after the controller is assigned to a panel.
    /// </summary>
    protected virtual void OnAttached() { }

    /// <summary>
    /// Called before the controller is removed from its panel.
    /// </summary>
    protected virtual void OnDetached() { }

    /// <summary>
    /// Called when the owning panel is attached to a visual tree.
    /// </summary>
    protected virtual void OnOwnerAttachedToVisualTree() { }

    /// <summary>
    /// Called when the owning panel is detached from a visual tree.
    /// </summary>
    protected virtual void OnOwnerDetachedFromVisualTree() { }

    /// <summary>
    /// Called after the owning panel first presents the current pose.
    /// </summary>
    protected virtual void OnOwnerPresented() { }

    internal void Attach(MotionPanel owner)
    {
        if (_owner is not null)
        {
            throw new InvalidOperationException($"{GetType().Name} is already attached to a {nameof(MotionPanel)}.");
        }

        _owner = owner;
        OnAttached();
    }

    internal void Detach(MotionPanel owner)
    {
        if (!ReferenceEquals(_owner, owner))
        {
            return;
        }

        OnDetached();
        _owner = null;
    }

    internal void NotifyOwnerAttachedToVisualTree() => OnOwnerAttachedToVisualTree();

    internal void NotifyOwnerDetachedFromVisualTree() => OnOwnerDetachedFromVisualTree();

    internal void NotifyOwnerPresented() => OnOwnerPresented();
}