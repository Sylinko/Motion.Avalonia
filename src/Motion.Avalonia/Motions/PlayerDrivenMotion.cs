using Avalonia;
using Avalonia.LogicalTree;

namespace Motion.Avalonia;

/// <summary>
/// Provides a stable <see cref="MotionPlayer"/> for motions driven by frame-aligned time.
/// </summary>
/// <remarks>
/// The player follows this motion's rooted logical-tree lifetime. Its playback state and elapsed
/// time survive temporary detachment, while frame scheduling resumes against the next logical
/// root. When the logical tree has a visual ancestor, the nearest one is used as the clock
/// attachment so a visual-tree detach pauses RAF even when the logical parent remains attached.
/// </remarks>
public abstract class PlayerDrivenMotion : Motion
{
    /// <summary>
    /// Gets the stable player owned by this motion.
    /// </summary>
    public MotionPlayer Player { get; } = new();

    private IDisposable? _playerAttachment;

    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        var clockVisual = this.FindLogicalAncestorOfType<Visual>() ?? e.Root as Visual;
        if (clockVisual is not null)
        {
            _playerAttachment = Player.Attach(clockVisual);
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        var attachment = _playerAttachment;
        _playerAttachment = null;
        try
        {
            attachment?.Dispose();
        }
        finally
        {
            base.OnDetachedFromLogicalTree(e);
        }
    }
}