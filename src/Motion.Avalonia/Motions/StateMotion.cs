using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Metadata;
using Motion.Avalonia.Utilities;

namespace Motion.Avalonia;

/// <summary>
/// Transitions between complete named layout states.
/// </summary>
/// <remarks>
/// A target omitted from a state is absent from that state. The first presented state is applied
/// immediately. Later changes use <see cref="Duration"/>, the inherited <see cref="Motion.Easing"/>,
/// and optional directed <see cref="MotionEdge"/> overrides. When a transition is interrupted,
/// each compiled track freezes its last published value and evaluates only the new target state
/// while the replacement transition is running.
/// </remarks>
public class StateMotion : PlayerDrivenMotion
{
    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> StateProperty =
        AvaloniaProperty.Register<StateMotion, object?>(nameof(State));

    /// <summary>
    /// Defines the <see cref="Duration"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<StateMotion, TimeSpan>(
            nameof(Duration),
            TimeSpan.FromMilliseconds(300),
            validate: static value => value >= TimeSpan.Zero);

    /// <summary>
    /// Gets or sets the requested state key.
    /// </summary>
    public object? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Gets or sets the default state-transition duration. Zero applies the state immediately.
    /// </summary>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets the complete states owned by this motion.
    /// </summary>
    [Content]
    public AvaloniaList<MotionState> States => _states;

    /// <summary>
    /// Gets the directed state-transition overrides.
    /// </summary>
    public AvaloniaList<MotionEdge> Edges => _edges;

    private readonly LogicalChildrenList<MotionState> _states;
    private readonly LogicalChildrenList<MotionEdge> _edges;
    private int _initializationDepth;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateMotion"/> class.
    /// </summary>
    public StateMotion()
    {
        _states = new LogicalChildrenList<MotionState>(this);
        _edges = new LogicalChildrenList<MotionEdge>(this);
        States.CollectionChanged += HandleStatesChanged;
        Edges.CollectionChanged += HandleEdgesChanged;
    }

    /// <inheritdoc />
    public override void BeginInit()
    {
        base.BeginInit();
        _initializationDepth++;
    }

    /// <inheritdoc />
    public override void EndInit()
    {
        if (_initializationDepth == 0)
        {
            base.EndInit();
            return;
        }

        if (_initializationDepth == 1)
        {
            ValidateUniqueDefinitions();
            InvalidateMotion();
        }

        _initializationDepth--;
        base.EndInit();
    }

    /// <inheritdoc />
    protected override MotionRuntime CreateRuntime(MotionCompilationContext context) => new StateMotionRuntime(context, this);

    /// <summary>
    /// Creates the immutable policy used for one requested state change.
    /// </summary>
    /// <remarks>
    /// Override this method to redirect a state, choose custom timing, or select a different
    /// directed edge. The returned policy is captured by the internal transition run and is not
    /// read again while that run is active.
    /// </remarks>
    protected virtual StateTransition CreateTransition(StateTransitionContext context)
    {
        var edge = FindEdge(context.PreviousState, context.RequestedState);
        return new StateTransition(
            context.RequestedState,
            edge?.Duration ?? Duration,
            edge is { HasEasing: true } ? edge.Easing : Easing,
            edge);
    }

    /// <summary>
    /// Invokes the protected transition-policy hook for the active runtime.
    /// </summary>
    /// <remarks>
    /// The runtime owns requested-state observation and per-track execution. Keeping this narrow
    /// bridge here preserves the protected extension point without making the runtime depend on
    /// the implementation details of a particular derived motion.
    /// </remarks>
    internal StateTransition CreateTransitionForRuntime(StateTransitionContext context) => CreateTransition(context);

    private MotionEdge? FindEdge(object? from, object? to)
    {
        MotionEdge? result = null;
        foreach (var edge in Edges)
        {
            if (!Equals(edge.From, from) || !Equals(edge.To, to))
            {
                continue;
            }

            if (result is not null)
            {
                throw new InvalidOperationException($"{nameof(StateMotion)} contains more than one edge from '{from}' to '{to}'.");
            }

            result = edge;
        }

        return result;
    }

    private void HandleStatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_initializationDepth > 0)
        {
            return;
        }

        ValidateUniqueDefinitions();
        InvalidateMotion();
    }

    private void HandleEdgesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_initializationDepth > 0)
        {
            return;
        }

        ValidateUniqueDefinitions();
        InvalidateMotion();
    }

    private void ValidateUniqueDefinitions()
    {
        var states = new HashSet<MotionState>(ReferenceEqualityComparer.Instance);
        foreach (var state in States)
        {
            if (!states.Add(state))
            {
                throw new InvalidOperationException($"{nameof(StateMotion)} cannot contain the same {nameof(MotionState)} instance more than once.");
            }
        }

        var edges = new HashSet<MotionEdge>(ReferenceEqualityComparer.Instance);
        foreach (var edge in Edges)
        {
            if (!edges.Add(edge))
            {
                throw new InvalidOperationException($"{nameof(StateMotion)} cannot contain the same {nameof(MotionEdge)} instance more than once.");
            }
        }
    }
}