using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Owns the live execution of one compiled <see cref="StateMotion"/>.
/// </summary>
/// <remarks>
/// State definitions are compiled into one stable track per target-property channel. A state
/// change only changes each track's runtime endpoint; it does not rebuild the graph. When a run is
/// interrupted, every track keeps its last published value as its source while the destination
/// endpoint continues to be read from the latest layout frame.
/// </remarks>
internal sealed class StateMotionRuntime : MotionRuntime
{
    private readonly MotionCompilationContext _context;
    private readonly StateMotion _motion;
    private readonly MotionState[] _stateDefinitions;
    private readonly Dictionary<MotionStateKey, MotionState> _states = [];
    private readonly Dictionary<MotionState, Dictionary<MotionChannel, MotionInterpolationMetadata>> _stateMetadata =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MotionEdge, Dictionary<MotionCompilationContext.InterpolationLookup, MotionInterpolationMetadata>>
        _edgeInterpolations =
            new(ReferenceEqualityComparer.Instance);
    private readonly List<IStateMotionTrack> _tracks = [];
    private MotionState? _resolvedState;
    private StateTransitionRun? _activeTransition;
    private bool _hasPresented;
    private bool _isCommitted;
    private bool _ownsPlayerPlayback;
    private bool _disposed;

    internal StateMotionRuntime(MotionCompilationContext context, StateMotion motion) : base(context)
    {
        _context = context;
        _motion = motion;
        _stateDefinitions = [.. _motion.States];
        foreach (var edge in _motion.Edges)
        {
            var interpolations = new Dictionary<MotionCompilationContext.InterpolationLookup, MotionInterpolationMetadata>();
            MotionCompilationContext.BuildInterpolationIndex(
                edge.Interpolations,
                interpolations,
                edge.ToString() ?? nameof(MotionEdge));
            _edgeInterpolations.Add(edge, interpolations);
        }

        DeclareLayoutSources(_stateDefinitions);
    }

    protected override void BuildTracks()
    {
        var channels = new List<MotionChannel> { MotionChannel.DesiredSize };
        var channelSet = new HashSet<MotionChannel> { MotionChannel.DesiredSize };

        foreach (var state in _stateDefinitions)
        {
            if (!_states.TryAdd(new MotionStateKey(state.Key), state))
            {
                throw new InvalidOperationException($"{nameof(StateMotion)} contains multiple states with key '{state.Key}'.");
            }

            var frame = _context.GetLayoutFrame(state);
            CaptureMetadata(state, frame);
            CollectChannels(frame, channels, channelSet);
        }

        foreach (var channel in channels)
        {
            var track = CreateTrack(channel);
            if (track is not IStateMotionTrack stateTrack)
            {
                throw new InvalidOperationException(
                    $"State track '{track.GetType().Name}' does not support state-motion execution.");
            }

            _tracks.Add(stateTrack);
        }

        SetRequiredLayoutSourceProvider(CollectRequiredStateLayoutSources);
        AddActivation(Activate);
    }

    protected override void OnCommitted() => _isCommitted = true;

    protected override void OnPresented() => _hasPresented = true;

    protected override void OnDisposed()
    {
        if (_disposed)
        {
            return;
        }

        var shouldStopPlayer = _isCommitted && _ownsPlayerPlayback;
        _disposed = true;
        _isCommitted = false;
        _ownsPlayerPlayback = false;
        _activeTransition = null;

        if (shouldStopPlayer)
        {
            _motion.Player.Stop();
        }

        _tracks.Clear();
        _states.Clear();
        _stateMetadata.Clear();
        _edgeInterpolations.Clear();
    }

    protected override MotionTrack<T> CreateTrackCore<T>(MotionChannel channel) =>
        new StateMotionTrack<T>(_context, channel, this);

    private IDisposable Activate()
    {
        var stateSubscription =
            _motion.GetObservable(StateMotion.StateProperty).Subscribe(new StateObserver(this));
        _motion.Player.Tick += OnPlayerTick;
        return new RuntimeActivation(this, stateSubscription);
    }

    private void OnRequestedState(object? requestedState)
    {
        if (_disposed)
        {
            return;
        }

        var isFirstPresentation = !_hasPresented || _resolvedState is null;
        var previousState = _activeTransition?.TargetState.Key ?? _resolvedState?.Key;
        var transition = _motion.CreateTransitionForRuntime(
            new StateTransitionContext(
                previousState,
                requestedState,
                isFirstPresentation,
                _activeTransition is not null));

        if (!TryFindState(transition.TargetState, out var targetState))
        {
            // An unknown request must not tear down either a stable state or an active run. A
            // derived StateMotion can still redirect an unknown request through its policy hook.
            return;
        }

        if (!_isCommitted || isFirstPresentation)
        {
            StopTransition();
            ApplyState(targetState);
            return;
        }

        if (transition.Duration <= TimeSpan.Zero)
        {
            StopTransition();
            ApplyState(targetState);
            return;
        }

        BeginTransition(targetState, transition);
    }

    private void BeginTransition(MotionState targetState, StateTransition transition)
    {
        foreach (var track in _tracks)
        {
            track.BeginTransition(targetState, transition);
        }

        _activeTransition = new StateTransitionRun(targetState, transition);
        _motion.Player.Reset();
        _motion.Player.Play();
        _ownsPlayerPlayback = true;
        _context.EnsureRequiredLayoutSources();
    }

    private void OnPlayerTick(MotionPlayer sender, TimeSpan time, TimeSpan deltaTime)
    {
        if (!_isCommitted || _activeTransition is not { } transition)
        {
            return;
        }

        if (time >= transition.Policy.Duration)
        {
            foreach (var track in _tracks)
            {
                track.CompleteTransition();
            }

            _resolvedState = transition.TargetState;
            _activeTransition = null;
            PublishState();
            StopOwnedPlayer();
            return;
        }

        var progress = time.Ticks / (double)transition.Policy.Duration.Ticks;
        foreach (var track in _tracks)
        {
            track.SetTransitionProgress(progress);
        }

        PublishState();
    }

    private void ApplyState(MotionState state)
    {
        _resolvedState = state;
        foreach (var track in _tracks)
        {
            track.ApplyState(state);
        }

        PublishState();
    }

    private void StopTransition()
    {
        _activeTransition = null;
        if (_ownsPlayerPlayback)
        {
            StopOwnedPlayer();
        }
    }

    private void StopOwnedPlayer()
    {
        _ownsPlayerPlayback = false;
        _motion.Player.Stop();
    }

    private void PublishState()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var track in _tracks)
        {
            track.Publish();
        }

        _context.EnsureRequiredLayoutSources();
    }

    private void CollectRequiredStateLayoutSources(Action<IMotionLayoutSource> add)
    {
        var state = _activeTransition?.TargetState ?? _resolvedState;
        if (state is not null)
        {
            add(state);
        }
    }

    private ChannelSample EvaluateState(MotionChannel channel, MotionState? state)
    {
        if (state is null)
        {
            return channel.Property == Motion.DesiredSizeProperty ? new ChannelSample(true, default(Size), default) : default;
        }

        if (channel.Property == Motion.DesiredSizeProperty)
        {
            return new ChannelSample(true, _context.GetMeasuredSize(state), default);
        }

        var metadata = GetMetadata(state, channel);
        var frame = _context.GetLayoutFrame(state);
        if (channel.LayoutTarget is { } targetName)
        {
            if (!frame.Entries.TryGetValue(targetName, out var entry))
            {
                return default;
            }

            if (channel.Property == Motion.PositionProperty)
            {
                return new ChannelSample(true, entry.Bounds.Position, metadata);
            }

            if (channel.Property == Motion.SizeProperty)
            {
                return new ChannelSample(true, entry.Bounds.Size, metadata);
            }

            return new ChannelSample(true, entry.Presence, metadata);
        }

        if (channel.PropertyTarget is { } propertyTarget &&
            frame.PropertyValues.TryGetValue(propertyTarget, out var values) &&
            values.TryGetValue(channel.Property, out var endpoint))
        {
            return new ChannelSample(true, endpoint.Value, metadata);
        }

        return default;
    }

    private bool TryFindState(object? key, out MotionState state)
    {
        if (_states.TryGetValue(new MotionStateKey(key), out var value))
        {
            state = value;
            return true;
        }

        state = null!;
        return false;
    }

    private MotionInterpolationMetadata GetMetadata(MotionState state, MotionChannel channel) =>
        _stateMetadata[state].GetValueOrDefault(channel);

    private MotionInterpolationMetadata GetEdgeMetadata(MotionEdge? edge, MotionChannel channel)
    {
        if (edge is null || !_edgeInterpolations.TryGetValue(edge, out var interpolations))
        {
            return default;
        }

        return interpolations.GetValueOrDefault(
            new MotionCompilationContext.InterpolationLookup(channel.TargetName, channel.Property));
    }

    private static void CollectChannels(MotionLayoutFrame frame, List<MotionChannel> channels, HashSet<MotionChannel> channelSet)
    {
        foreach (var entry in frame.Entries)
        {
            AddChannel(MotionChannel.Layout(entry.Key, Motion.PositionProperty), channels, channelSet);
            AddChannel(MotionChannel.Layout(entry.Key, Motion.SizeProperty), channels, channelSet);
            AddChannel(MotionChannel.Layout(entry.Key, Motion.PresenceProperty), channels, channelSet);
        }

        foreach (var targetValues in frame.PropertyValues)
        {
            foreach (var propertyValue in targetValues.Value)
            {
                AddChannel(MotionChannel.ForProperty(targetValues.Key, propertyValue.Key), channels, channelSet);
            }
        }
    }

    private void CaptureMetadata(MotionState state, MotionLayoutFrame frame)
    {
        var metadata = new Dictionary<MotionChannel, MotionInterpolationMetadata>();
        foreach (var entry in frame.Entries)
        {
            metadata.Add(MotionChannel.Layout(entry.Key, Motion.PositionProperty), entry.Value.PositionMetadata);
            metadata.Add(MotionChannel.Layout(entry.Key, Motion.SizeProperty), entry.Value.SizeMetadata);
            metadata.Add(MotionChannel.Layout(entry.Key, Motion.PresenceProperty), entry.Value.PresenceMetadata);
        }

        foreach (var targetValues in frame.PropertyValues)
        {
            foreach (var propertyValue in targetValues.Value)
            {
                metadata.Add(
                    MotionChannel.ForProperty(targetValues.Key, propertyValue.Key),
                    propertyValue.Value.Metadata);
            }
        }

        _stateMetadata.Add(state, metadata);
    }

    private static void AddChannel(MotionChannel channel, List<MotionChannel> channels, HashSet<MotionChannel> channelSet)
    {
        if (channelSet.Add(channel))
        {
            channels.Add(channel);
        }
    }

    private readonly record struct MotionStateKey(object? Value)
    {
        public bool Equals(MotionStateKey other) => Equals(Value, other.Value);

        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    }

    private readonly record struct ChannelSample(
        bool HasValue,
        object? Value,
        MotionInterpolationMetadata Metadata
    );

    private sealed record StateTransitionRun(MotionState TargetState, StateTransition Policy);

    /// <summary>
    /// Provides the small lifecycle surface needed by a state track without exposing the runtime
    /// implementation to the general-purpose track API.
    /// </summary>
    private interface IStateMotionTrack
    {
        void Publish();

        void ApplyState(MotionState state);

        void BeginTransition(MotionState targetState, StateTransition transition);

        void SetTransitionProgress(double progress);

        void CompleteTransition();
    }

    /// <summary>
    /// Evaluates one state channel and retains its last published value for interruption starts.
    /// </summary>
    internal sealed class StateMotionTrack<T> : MotionTrack<T>, IStateMotionTrack
    {
        private readonly StateMotionRuntime _owner;
        private readonly MotionChannel _channel;
        private MotionState? _state;
        private TrackTransition? _transition;
        private T _currentValue = default!;
        private bool _hasCurrentValue;

        internal StateMotionTrack(
            MotionCompilationContext context,
            MotionChannel channel,
            StateMotionRuntime owner)
            : base(context, channel)
        {
            _owner = owner;
            _channel = channel;
        }

        void IStateMotionTrack.ApplyState(MotionState state)
        {
            _state = state;
            _transition = null;
        }

        void IStateMotionTrack.BeginTransition(MotionState targetState, StateTransition transition)
        {
            if (!_hasCurrentValue)
            {
                throw new InvalidOperationException(
                    $"State track for '{Property}' cannot start a transition before its first value was published.");
            }

            _state = targetState;
            _transition = new TrackTransition(
                _currentValue,
                targetState,
                transition,
                _owner.GetEdgeMetadata(transition.Edge, _channel));
        }

        void IStateMotionTrack.SetTransitionProgress(double progress)
        {
            if (_transition is { } transition)
            {
                transition.Progress = progress;
            }
        }

        void IStateMotionTrack.CompleteTransition()
        {
            if (_transition is { } transition)
            {
                _state = transition.TargetState;
                _transition = null;
            }
        }

        public override T Evaluate(T baseValue)
        {
            var value = _transition is { } transition ?
                EvaluateTransition(transition, baseValue) :
                EvaluateState(baseValue);
            _currentValue = value;
            _hasCurrentValue = true;
            return value;
        }

        private T EvaluateState(T baseValue)
        {
            var sample = _owner.EvaluateState(_channel, _state);
            var value = sample.HasValue ? sample.Value : GetFallback(_channel, baseValue).Value;
            return MotionTrackValue.Convert<T>(value);
        }

        private T EvaluateTransition(TrackTransition transition, T baseValue)
        {
            var target = _owner.EvaluateState(_channel, transition.TargetState);
            var source = new ChannelSample(true, transition.SourceValue, default);
            if (_channel.IsLayout && _channel.Property != Motion.DesiredSizeProperty)
            {
                if (!target.HasValue)
                {
                    target = _channel.Property == Motion.PresenceProperty ?
                        new ChannelSample(true, 0d, default) :
                        source;
                }
            }
            else if (!target.HasValue)
            {
                target = GetFallback(_channel, baseValue);
            }

            var inheritedProgress = transition.Policy.Easing?.Ease(transition.Progress) ?? transition.Progress;
            return InterpolateValue(
                MotionTrackValue.Convert<T>(source.Value),
                MotionTrackValue.Convert<T>(target.Value),
                transition.Progress,
                inheritedProgress,
                target.Metadata,
                transition.EdgeMetadata);
        }

        private static ChannelSample GetFallback(MotionChannel channel, object? baseValue)
        {
            if (channel.Property == Motion.DesiredSizeProperty)
            {
                return new ChannelSample(true, default(Size), default);
            }

            if (channel.LayoutTarget is not null)
            {
                if (channel.Property == Motion.PositionProperty)
                {
                    return new ChannelSample(true, default(Point), default);
                }

                if (channel.Property == Motion.SizeProperty)
                {
                    return new ChannelSample(true, default(Size), default);
                }

                return new ChannelSample(true, 0d, default);
            }

            return new ChannelSample(true, baseValue, default);
        }

        private sealed class TrackTransition(
            T sourceValue,
            MotionState targetState,
            StateTransition policy,
            MotionInterpolationMetadata edgeMetadata
        )
        {
            public T SourceValue { get; } = sourceValue;

            public MotionState TargetState { get; } = targetState;

            public StateTransition Policy { get; } = policy;

            public MotionInterpolationMetadata EdgeMetadata { get; } = edgeMetadata;

            public double Progress { get; set; }
        }
    }

    private sealed class StateObserver(StateMotionRuntime owner) : IObserver<object?>
    {
        public void OnNext(object? value) => owner.OnRequestedState(value);

        public void OnError(Exception error) =>
            throw new InvalidOperationException($"{nameof(StateMotion)} state observation failed.", error);

        public void OnCompleted()
        {
        }
    }

    private sealed class RuntimeActivation(StateMotionRuntime owner, IDisposable stateSubscription) : IDisposable
    {
        private StateMotionRuntime? _owner = owner;
        private IDisposable? _stateSubscription = stateSubscription;

        public void Dispose()
        {
            if (_owner is not { } owner)
            {
                return;
            }

            owner._motion.Player.Tick -= owner.OnPlayerTick;
            _stateSubscription?.Dispose();
            _stateSubscription = null;
            _owner = null;
        }
    }
}