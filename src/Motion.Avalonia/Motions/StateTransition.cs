using Avalonia.Animation.Easings;

namespace Motion.Avalonia;

/// <summary>
/// Describes the immutable facts supplied when a <see cref="StateMotion"/> selects a transition.
/// </summary>
/// <param name="PreviousState">
/// The previously resolved state key. During an interruption this is the destination of the
/// active run, because that is the state represented by the current runtime graph.
/// </param>
/// <param name="RequestedState">The newly requested state key.</param>
/// <param name="IsFirstPresentation">Whether the motion has not completed its first presentation.</param>
/// <param name="IsInterrupted">Whether another animated transition is currently active.</param>
public readonly record struct StateTransitionContext(
    object? PreviousState,
    object? RequestedState,
    bool IsFirstPresentation,
    bool IsInterrupted
);

/// <summary>
/// Specifies the immutable policy captured for one state transition run.
/// </summary>
/// <remarks>
/// The duration, easing, and directed edge are sampled once. Changing their declarative sources
/// while a transition is running affects only a later transition.
/// </remarks>
public sealed class StateTransition
{
    /// <summary>
    /// Gets the state key presented by this transition.
    /// </summary>
    public object? TargetState { get; }

    /// <summary>
    /// Gets the captured transition duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the captured default easing. A null value is linear.
    /// </summary>
    public Easing? Easing { get; }

    /// <summary>
    /// Gets the directed edge whose compiled property metadata applies to this transition.
    /// </summary>
    public MotionEdge? Edge { get; }

    /// <summary>
    /// Initializes one immutable state-transition policy.
    /// </summary>
    public StateTransition(object? targetState, TimeSpan duration, Easing? easing = null, MotionEdge? edge = null)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "A state transition duration cannot be negative.");
        }

        TargetState = targetState;
        Duration = duration;
        Easing = easing;
        Edge = edge;
    }
}