namespace Motion.Avalonia;

/// <summary>
/// Provides a stable <see cref="MotionPlayer"/> for controllers whose pose is driven by time-based
/// playback.
/// </summary>
/// <remarks>
/// The player is attached to the owning <see cref="MotionPanel"/> for the complete controller-panel
/// association. Temporary visual-tree detachment only suspends frame scheduling. The attachment
/// lease is released when the controller is removed or replaced.
/// Override <see cref="OnPlayerTick"/> to translate clock time into a pose or another
/// controller-specific value.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public sealed class FadingController : PlayerDrivenMotionController
/// {
///     private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(300);
///
///     public void Start()
///     {
///         Player.Reset();
///         Player.Play();
///     }
///
///     protected override void OnPlayerTick(TimeSpan time, TimeSpan deltaTime)
///     {
///         var progress = Math.Clamp(time / Duration, 0, 1);
///         SetCurrentPose(MotionPose.CreateBlend(
///             MotionPose.State("Hidden"),
///             MotionPose.State("Visible"),
///             progress));
///     }
/// }
/// ]]></code>
/// </example>
public abstract class PlayerDrivenMotionController : MotionController
{
    /// <summary>
    /// Gets the stable player owned by this controller.
    /// </summary>
    public MotionPlayer Player { get; } = new();

    private IDisposable? _playerAttachment;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerDrivenMotionController"/> class.
    /// </summary>
    protected PlayerDrivenMotionController()
    {
        Player.Tick += HandlePlayerTick;
    }

    /// <summary>
    /// Called whenever <see cref="Player"/> publishes a frame-aligned clock tick.
    /// </summary>
    /// <param name="time">The absolute elapsed player time.</param>
    /// <param name="deltaTime">The time elapsed since the previous player tick.</param>
    protected virtual void OnPlayerTick(TimeSpan time, TimeSpan deltaTime) { }

    internal override void AttachCore(MotionPanel owner)
    {
        base.AttachCore(owner);
        _playerAttachment = Player.Attach(owner);
    }

    internal override void DetachCore(MotionPanel owner)
    {
        var attachment = _playerAttachment;
        _playerAttachment = null;
        try
        {
            attachment?.Dispose();
        }
        finally
        {
            base.DetachCore(owner);
        }
    }

    private void HandlePlayerTick(MotionPlayer sender, TimeSpan time, TimeSpan deltaTime) =>
        OnPlayerTick(time, deltaTime);
}