namespace Motion.Avalonia;

/// <summary>
/// Places a sparse keyframe at an absolute <see cref="KeyTime"/>.
/// </summary>
public sealed class TimelineMotionKeyFrame : MotionKeyFrame
{
    /// <summary>
    /// Gets or sets the absolute keyframe time.
    /// </summary>
    public TimeSpan KeyTime
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateMotion();
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(TimelineMotionKeyFrame)} at {KeyTime}";
}