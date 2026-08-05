using Avalonia.Animation;

namespace Motion.Avalonia;

/// <summary>
/// Supplies sparse keyframes and publishes the current normalized progress of a keyframe motion.
/// </summary>
/// <remarks>
/// A subscription must immediately publish the current progress and then publish every later
/// change. Published values and the cues returned by <see cref="GetCue"/> share the inclusive
/// normalized range from zero to one.
/// </remarks>
public interface IKeyFrameMotion : IObservable<double>
{
    /// <summary>
    /// Gets the keyframes in ascending cue order.
    /// </summary>
    IEnumerable<MotionKeyFrame> KeyFrames { get; }

    /// <summary>
    /// Gets the normalized cue of one keyframe owned by this motion.
    /// </summary>
    /// <param name="keyFrame">The keyframe whose cue should be resolved.</param>
    Cue GetCue(MotionKeyFrame keyFrame);
}