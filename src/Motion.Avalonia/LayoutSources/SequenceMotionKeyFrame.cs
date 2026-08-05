using Avalonia.Animation;

namespace Motion.Avalonia;

/// <summary>
/// Places a sparse keyframe at normalized <see cref="Cue"/> progress.
/// </summary>
public sealed class SequenceMotionKeyFrame : MotionKeyFrame
{
    /// <summary>
    /// Gets or sets normalized keyframe progress.
    /// </summary>
    public Cue Cue
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
    public override string ToString() => $"{nameof(SequenceMotionKeyFrame)} at {Cue.CueValue:P0}";
}