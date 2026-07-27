using Avalonia;
using Avalonia.Threading;

namespace Motion.Avalonia;

/// <summary>
/// Provides Avalonia integration and animation-frame scheduling for motion controllers.
/// </summary>
/// <remarks>
/// A controller instance can be attached to only one <see cref="MotionPanel"/> at a time.
/// Use <see cref="SetCurrentPose"/> to publish state and
/// <see cref="RequestAnimationFrame"/> for a panel-lifetime-aware animation loop.
/// Controllers that subscribe to <see cref="PlayerProgressChanged"/> receive a controller-owned
/// <see cref="MotionPlayer"/> through <see cref="Player"/>.
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

    /// <summary>
    /// Defines the <see cref="Player"/> property.
    /// </summary>
    public static readonly DirectProperty<MotionController, MotionPlayer?> PlayerProperty =
        AvaloniaProperty.RegisterDirect<MotionController, MotionPlayer?>(
            nameof(Player),
            controller => controller.Player);

    /// <inheritdoc />
    public MotionPose CurrentPose
    {
        get;
        private set => SetAndRaise(CurrentPoseProperty, ref field, value);
    } = MotionPose.Empty;

    /// <summary>
    /// Gets the controller-owned player, or null when this controller does not consume playback progress.
    /// </summary>
    public MotionPlayer? Player
    {
        get;
        private set => SetAndRaise(PlayerProperty, ref field, value);
    }

    /// <inheritdoc />
    public event EventHandler? CurrentPoseChanged;

    /// <summary>
    /// Occurs when the controller-owned player publishes progress. The first subscription creates
    /// <see cref="Player"/> and removing the last subscription releases it.
    /// </summary>
    protected event EventHandler? PlayerProgressChanged
    {
        add
        {
            Dispatcher.UIThread.VerifyAccess();
            if (value is null)
            {
                return;
            }

            var wasEmpty = _playerProgressChanged is null;
            _playerProgressChanged += value;
            if (wasEmpty)
            {
                Player = new MotionPlayer(this);
            }
        }
        remove
        {
            Dispatcher.UIThread.VerifyAccess();
            if (value is null)
            {
                return;
            }

            var hadSubscribers = _playerProgressChanged is not null;
            _playerProgressChanged -= value;
            if (hadSubscribers && _playerProgressChanged is null)
            {
                Player?.Dispose();
                Player = null;
            }
        }
    }

    private MotionPanel? _owner;
    private EventHandler? _playerProgressChanged;

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
    /// Requests a callback on the next animation frame of the owning panel.
    /// </summary>
    /// <param name="callback">The animation-frame callback.</param>
    /// <returns>True when a frame was scheduled; otherwise false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    protected bool RequestAnimationFrame(Action<TimeSpan> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return _owner?.RequestControllerAnimationFrame(this, callback) == true;
    }

    internal bool RequestPlayerAnimationFrame(
        MotionPlayer player,
        Action<TimeSpan> callback) =>
        ReferenceEquals(Player, player) && RequestAnimationFrame(callback);

    internal void NotifyPlayerProgressChanged(MotionPlayer player)
    {
        if (ReferenceEquals(Player, player))
        {
            _playerProgressChanged?.Invoke(player, EventArgs.Empty);
        }
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
        Player?.NotifyOwnerDetachedFromVisualTree();
        _owner = null;
    }

    internal void NotifyOwnerAttachedToVisualTree()
    {
        Player?.NotifyOwnerAttachedToVisualTree();
        OnOwnerAttachedToVisualTree();
    }

    internal void NotifyOwnerDetachedFromVisualTree()
    {
        OnOwnerDetachedFromVisualTree();
        Player?.NotifyOwnerDetachedFromVisualTree();
    }

    internal void NotifyOwnerPresented() => OnOwnerPresented();
}
