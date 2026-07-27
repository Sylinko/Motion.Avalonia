using Avalonia;
using Avalonia.Threading;

namespace Motion.Avalonia;

/// <summary>
/// Controls time-based playback for a <see cref="MotionController"/>.
/// </summary>
/// <remarks>
/// A player is created and owned by a controller when that controller subscribes to playback
/// progress. Applications do not construct players directly; use
/// <see cref="MotionController.Player"/> to access the current player. Playback progress is
/// linear. Controllers, edges, stops, and interpolators remain responsible for easing.
/// </remarks>
/// <example>
/// A sequence can be played over two seconds without adding playback methods to the controller:
/// <code>
/// controller.Player?.Play(TimeSpan.FromSeconds(2));
/// controller.Player?.Pause();
/// controller.Player?.Resume();
/// controller.Player?.Seek(0.5);
/// </code>
/// </example>
public sealed class MotionPlayer : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="Duration"/> property.
    /// </summary>
    public static readonly DirectProperty<MotionPlayer, TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterDirect<MotionPlayer, TimeSpan>(
            nameof(Duration),
            player => player.Duration);

    /// <summary>
    /// Defines the <see cref="Position"/> property.
    /// </summary>
    public static readonly DirectProperty<MotionPlayer, TimeSpan> PositionProperty =
        AvaloniaProperty.RegisterDirect<MotionPlayer, TimeSpan>(
            nameof(Position),
            player => player.Position);

    /// <summary>
    /// Defines the <see cref="Progress"/> property.
    /// </summary>
    public static readonly DirectProperty<MotionPlayer, double> ProgressProperty =
        AvaloniaProperty.RegisterDirect<MotionPlayer, double>(
            nameof(Progress),
            player => player.Progress);

    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly DirectProperty<MotionPlayer, MotionPlayerState> StateProperty =
        AvaloniaProperty.RegisterDirect<MotionPlayer, MotionPlayerState>(
            nameof(State),
            player => player.State);

    /// <summary>
    /// Gets the duration of the current or most recent playback.
    /// </summary>
    public TimeSpan Duration
    {
        get;
        private set => SetAndRaise(DurationProperty, ref field, value);
    }

    /// <summary>
    /// Gets the current playback position.
    /// </summary>
    public TimeSpan Position
    {
        get;
        private set => SetAndRaise(PositionProperty, ref field, value);
    }

    /// <summary>
    /// Gets the normalized playback progress.
    /// </summary>
    public double Progress
    {
        get;
        private set => SetAndRaise(ProgressProperty, ref field, value);
    }

    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    public MotionPlayerState State
    {
        get;
        private set => SetAndRaise(StateProperty, ref field, value);
    }

    private MotionController? _owner;
    private TimeSpan? _lastFrameTime;
    private bool _frameRequested;
    private bool _isDisposed;
    private int _frameGeneration;

    internal MotionPlayer(MotionController owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Starts playback from the beginning using the supplied duration.
    /// </summary>
    /// <param name="duration">The time required to move from progress zero to one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is not positive.</exception>
    /// <exception cref="ObjectDisposedException">The owning controller no longer uses this player.</exception>
    public void Play(TimeSpan duration)
    {
        VerifyAccessAndNotDisposed();

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Playback duration must be positive.");
        }

        InvalidateRequestedFrame();
        Duration = duration;
        Position = TimeSpan.Zero;
        State = MotionPlayerState.Playing;
        PublishProgress(0);
        RequestNextFrame();
    }

    /// <summary>
    /// Pauses active playback without changing its position.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The owning controller no longer uses this player.</exception>
    public void Pause()
    {
        VerifyAccessAndNotDisposed();

        if (State != MotionPlayerState.Playing)
        {
            return;
        }

        InvalidateRequestedFrame();
        State = MotionPlayerState.Paused;
    }

    /// <summary>
    /// Resumes paused playback from its current position.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The owning controller no longer uses this player.</exception>
    public void Resume()
    {
        VerifyAccessAndNotDisposed();

        if (State != MotionPlayerState.Paused)
        {
            return;
        }

        State = MotionPlayerState.Playing;
        _lastFrameTime = null;
        RequestNextFrame();
    }

    /// <summary>
    /// Stops playback at its current position.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The owning controller no longer uses this player.</exception>
    public void Stop()
    {
        VerifyAccessAndNotDisposed();

        if (State == MotionPlayerState.Stopped)
        {
            return;
        }

        InvalidateRequestedFrame();
        State = MotionPlayerState.Stopped;
    }

    /// <summary>
    /// Seeks to a normalized position. Seeking active playback to its end completes it.
    /// </summary>
    /// <param name="progress">The finite progress value, clamped to the range from zero to one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="progress"/> is not finite.</exception>
    /// <exception cref="ObjectDisposedException">The owning controller no longer uses this player.</exception>
    public void Seek(double progress)
    {
        VerifyAccessAndNotDisposed();

        if (!double.IsFinite(progress))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), progress, "Playback progress must be finite.");
        }

        progress = Math.Clamp(progress, 0, 1);
        Position = GetPosition(progress);
        _lastFrameTime = null;

        if (progress >= 1 && State == MotionPlayerState.Playing)
        {
            InvalidateRequestedFrame();
            State = MotionPlayerState.Stopped;
        }

        PublishProgress(progress);
    }

    internal void NotifyOwnerAttachedToVisualTree()
    {
        Dispatcher.UIThread.VerifyAccess();
        RequestNextFrame();
    }

    internal void NotifyOwnerDetachedFromVisualTree()
    {
        Dispatcher.UIThread.VerifyAccess();
        InvalidateRequestedFrame();
    }

    internal void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();

        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _owner = null;
        InvalidateRequestedFrame();
        State = MotionPlayerState.Stopped;
    }

    private void VerifyAccessAndNotDisposed()
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void RequestNextFrame()
    {
        if (_isDisposed || State != MotionPlayerState.Playing || _frameRequested || _owner is null)
        {
            return;
        }

        var generation = _frameGeneration;
        _frameRequested = _owner.RequestPlayerAnimationFrame(this, timestamp => OnAnimationFrame(timestamp, generation));
    }

    private void OnAnimationFrame(TimeSpan timestamp, int generation)
    {
        if (_isDisposed || generation != _frameGeneration || State != MotionPlayerState.Playing)
        {
            return;
        }

        _frameRequested = false;
        if (_lastFrameTime is not { } lastFrameTime || timestamp <= lastFrameTime)
        {
            _lastFrameTime = timestamp;
            RequestNextFrame();
            return;
        }

        _lastFrameTime = timestamp;
        var remaining = Duration - Position;
        Position = timestamp - lastFrameTime >= remaining ? Duration : Position + (timestamp - lastFrameTime);

        var completed = Position >= Duration;
        if (completed)
        {
            InvalidateRequestedFrame();
            State = MotionPlayerState.Stopped;
        }

        PublishProgress(completed ? 1 : Position.Ticks / (double)Duration.Ticks);

        if (generation == _frameGeneration && State == MotionPlayerState.Playing)
        {
            RequestNextFrame();
        }
    }

    private void PublishProgress(double progress)
    {
        Progress = progress;
        _owner?.NotifyPlayerProgressChanged(this);
    }

    private TimeSpan GetPosition(double progress)
    {
        if (progress <= 0 || Duration <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (progress >= 1)
        {
            return Duration;
        }

        return TimeSpan.FromTicks((long)(Duration.Ticks * progress));
    }

    private void InvalidateRequestedFrame()
    {
        _frameGeneration++;
        _frameRequested = false;
        _lastFrameTime = null;
    }
}

/// <summary>
/// Identifies the current state of a <see cref="MotionPlayer"/>.
/// </summary>
public enum MotionPlayerState
{
    /// <summary>
    /// The player is not advancing.
    /// </summary>
    Stopped,

    /// <summary>
    /// The player is advancing on animation frames.
    /// </summary>
    Playing,

    /// <summary>
    /// The player retains its position until resumed.
    /// </summary>
    Paused,
}