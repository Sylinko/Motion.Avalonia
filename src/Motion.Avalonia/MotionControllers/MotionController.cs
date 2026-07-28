using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Provides Avalonia integration for motion controllers.
/// </summary>
/// <remarks>
/// A controller instance can be attached to only one <see cref="MotionPanel"/> at a time.
/// Use <see cref="SetCurrentPose"/> to publish state.
/// Derive from <see cref="PlayerDrivenMotionController"/> when the controller needs a stable
/// frame-aligned <see cref="MotionPlayer"/>.
/// </remarks>
/// <example>
/// A progress-driven controller can publish a blend without owning a clock:
/// <code><![CDATA[
/// public sealed class ProgressController : MotionController
/// {
///     public double Progress
///     {
///         get;
///         set
///         {
///             field = Math.Clamp(value, 0, 1);
///             SetCurrentPose(MotionPose.CreateBlend(
///                 MotionPose.State("Start"),
///                 MotionPose.State("End"),
///                 field));
///         }
///     }
/// }
/// ]]></code>
/// </example>
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
    /// <exception cref="ArgumentNullException"><paramref name="pose"/> is null.</exception>
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
        try
        {
            AttachCore(owner);
            OnAttached();
        }
        catch
        {
            try
            {
                DetachCore(owner);
            }
            finally
            {
                _owner = null;
            }

            throw;
        }
    }

    internal void Detach(MotionPanel owner)
    {
        if (!ReferenceEquals(_owner, owner))
        {
            return;
        }

        try
        {
            OnDetached();
        }
        finally
        {
            try
            {
                DetachCore(owner);
            }
            finally
            {
                _owner = null;
            }
        }
    }

    internal void NotifyOwnerAttachedToVisualTree() => OnOwnerAttachedToVisualTree();

    internal void NotifyOwnerDetachedFromVisualTree() => OnOwnerDetachedFromVisualTree();

    internal void NotifyOwnerPresented() => OnOwnerPresented();

    internal virtual void AttachCore(MotionPanel owner) { }

    internal virtual void DetachCore(MotionPanel owner) { }
}
