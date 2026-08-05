using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Motion.Avalonia;

/// <summary>
/// Represents a frame-aligned motion clock tick.
/// </summary>
/// <param name="sender">The player that published the tick.</param>
/// <param name="time">The absolute elapsed clock time.</param>
/// <param name="deltaTime">The time elapsed since the previous tick.</param>
public delegate void MotionTickEventHandler(MotionPlayer sender, TimeSpan time, TimeSpan deltaTime);

/// <summary>
/// Provides a frame-aligned clock for time-driven motion.
/// </summary>
/// <remarks>
/// Attach the player to a <see cref="Visual"/> while it should use that visual's
/// <see cref="TopLevel"/> animation clock. The returned lease owns the event subscriptions and
/// must be disposed before the player can attach to another visual. A temporary visual-tree
/// detachment suspends frame requests without ending the lease, and detached time is excluded
/// when the visual is attached again.
///
/// The player deliberately has no duration or normalized progress. Motions interpret
/// <see cref="Tick"/> values according to their own timing model.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// var player = new MotionPlayer();
/// using var attachment = player.Attach(control);
///
/// player.Tick += (_, time, deltaTime) =>
///     Debug.WriteLine($"{time}: +{deltaTime}");
///
/// player.Play();
/// player.Pause(); // Keeps Time.
/// player.Play();  // Continues from Time.
/// player.Stop();  // Stops and resets Time to zero.
/// ]]></code>
/// </example>
public sealed class MotionPlayer : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="Time"/> property.
    /// </summary>
    public static readonly DirectProperty<MotionPlayer, TimeSpan> TimeProperty =
        AvaloniaProperty.RegisterDirect<MotionPlayer, TimeSpan>(
            nameof(Time),
            player => player.Time);

    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly DirectProperty<MotionPlayer, MotionPlayerState> StateProperty =
        AvaloniaProperty.RegisterDirect<MotionPlayer, MotionPlayerState>(
            nameof(State),
            player => player.State);

    /// <summary>
    /// Gets the absolute elapsed clock time.
    /// </summary>
    public TimeSpan Time
    {
        get;
        private set => SetAndRaise(TimeProperty, ref field, value);
    }

    /// <summary>
    /// Gets the current clock state.
    /// </summary>
    public MotionPlayerState State
    {
        get;
        private set => SetAndRaise(StateProperty, ref field, value);
    }

    /// <summary>
    /// Occurs on each animation frame while the clock is playing.
    /// </summary>
    /// <remarks>
    /// The first tick after playing, resetting, attaching, or reattaching has a deltaTime of zero because it establishes the animation-clock baseline.
    /// </remarks>
    public event MotionTickEventHandler? Tick;

    private Visual? _visual;
    private TopLevel? _topLevel;
    private TimeSpan? _lastFrameTime;
    private bool _frameRequested;
    private int _attachmentGeneration;
    private int _frameGeneration;

    /// <summary>
    /// Attaches this player to the animation clock and visual-tree lifetime of
    /// <paramref name="visual"/>.
    /// </summary>
    /// <param name="visual">The visual that supplies the current <see cref="TopLevel"/>.</param>
    /// <returns>
    /// A lease that ends the attachment. Dispose it on the UI thread before attaching this player
    /// to another visual.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="visual"/> is null.</exception>
    /// <exception cref="InvalidOperationException">This player already has an active attachment.</exception>
    public IDisposable Attach(Visual visual)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(visual);

        if (_visual is not null)
        {
            throw new InvalidOperationException(
                $"{nameof(MotionPlayer)} is already attached. Dispose the current attachment before attaching it again.");
        }

        var generation = ++_attachmentGeneration;
        _visual = visual;
        visual.AttachedToVisualTree += OnVisualAttachedToVisualTree;
        visual.DetachedFromVisualTree += OnVisualDetachedFromVisualTree;
        _topLevel = TopLevel.GetTopLevel(visual);

        if (_topLevel is not null)
        {
            _lastFrameTime = null;
            RequestNextFrame();
        }

        return new Attachment(this, generation);
    }

    /// <summary>
    /// Starts or resumes the clock from its current <see cref="Time"/>.
    /// </summary>
    public void Play()
    {
        VerifyAccess();

        if (State == MotionPlayerState.Playing)
        {
            return;
        }

        InvalidateRequestedFrame();
        State = MotionPlayerState.Playing;
        RequestNextFrame();
    }

    /// <summary>
    /// Pauses the clock without changing <see cref="Time"/>.
    /// </summary>
    public void Pause()
    {
        VerifyAccess();

        if (State != MotionPlayerState.Playing)
        {
            return;
        }

        InvalidateRequestedFrame();
        State = MotionPlayerState.Paused;
    }

    /// <summary>
    /// Resets <see cref="Time"/> to zero without changing <see cref="State"/>.
    /// </summary>
    /// <remarks>
    /// Resetting a playing clock establishes a new animation-frame baseline before time advances
    /// again.
    /// </remarks>
    public void Reset()
    {
        VerifyAccess();

        var isPlaying = State == MotionPlayerState.Playing;
        InvalidateRequestedFrame();
        Time = TimeSpan.Zero;

        if (isPlaying)
        {
            RequestNextFrame();
        }
    }

    /// <summary>
    /// Stops the clock and resets <see cref="Time"/> to zero.
    /// </summary>
    /// <remarks>
    /// This is the convenient stop-state equivalent of pausing and then resetting.
    /// </remarks>
    public void Stop()
    {
        VerifyAccess();

        InvalidateRequestedFrame();
        State = MotionPlayerState.Stopped;
        Time = TimeSpan.Zero;
    }

    private void OnVisualAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (!ReferenceEquals(sender, _visual))
        {
            return;
        }

        _topLevel = TopLevel.GetTopLevel(_visual);
        _lastFrameTime = null;
        RequestNextFrame();
    }

    private void OnVisualDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (!ReferenceEquals(sender, _visual))
        {
            return;
        }

        _topLevel = null;
        InvalidateRequestedFrame();
    }

    private void ReleaseAttachment(int generation)
    {
        VerifyAccess();

        if (generation != _attachmentGeneration || _visual is not { } visual)
        {
            return;
        }

        visual.AttachedToVisualTree -= OnVisualAttachedToVisualTree;
        visual.DetachedFromVisualTree -= OnVisualDetachedFromVisualTree;
        _visual = null;
        _topLevel = null;
        InvalidateRequestedFrame();
    }

    private void RequestNextFrame()
    {
        if (State != MotionPlayerState.Playing || _frameRequested || _topLevel is not { } topLevel)
        {
            return;
        }

        var generation = _frameGeneration;
        _frameRequested = true;
        try
        {
            topLevel.RequestAnimationFrame(timestamp => OnAnimationFrame(timestamp, generation));
        }
        catch
        {
            _frameRequested = false;
            throw;
        }
    }

    private void OnAnimationFrame(TimeSpan timestamp, int generation)
    {
        if (generation != _frameGeneration || State != MotionPlayerState.Playing)
        {
            return;
        }

        _frameRequested = false;
        var deltaTime = TimeSpan.Zero;
        if (_lastFrameTime is not { } lastFrameTime)
        {
            _lastFrameTime = timestamp;
        }
        else if (timestamp > lastFrameTime)
        {
            deltaTime = timestamp - lastFrameTime;
            _lastFrameTime = timestamp;
            Time += deltaTime;
        }

        Tick?.Invoke(this, Time, deltaTime);

        if (generation == _frameGeneration && State == MotionPlayerState.Playing)
        {
            RequestNextFrame();
        }
    }

    private void InvalidateRequestedFrame()
    {
        _frameGeneration++;
        _frameRequested = false;
        _lastFrameTime = null;
    }

    private sealed class Attachment(MotionPlayer player, int generation) : IDisposable
    {
        private MotionPlayer? _player = player;

        public void Dispose()
        {
            Dispatcher.UIThread.VerifyAccess();

            if (_player is not { } current)
            {
                return;
            }

            current.ReleaseAttachment(generation);
            _player = null;
        }
    }
}

/// <summary>
/// Identifies the current state of a <see cref="MotionPlayer"/>.
/// </summary>
public enum MotionPlayerState
{
    /// <summary>
    /// The clock is not advancing and its time is zero.
    /// </summary>
    Stopped,

    /// <summary>
    /// The clock is advancing on animation frames.
    /// </summary>
    Playing,

    /// <summary>
    /// The clock retains its time until played again, reset, or stopped.
    /// </summary>
    Paused,
}