using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Data;

namespace Motion.Avalonia;

/// <summary>
/// Identifies one motion output channel.
/// </summary>
public readonly record struct MotionChannel(string? LayoutTarget, StyledElement? PropertyTarget, AvaloniaProperty Property)
{
    /// <summary>
    /// Gets the panel-wide desired-size channel.
    /// </summary>
    public static MotionChannel DesiredSize { get; } = new(null, null, Motion.DesiredSizeProperty);

    /// <summary>
    /// Gets the effective target identity used for interpolation lookup.
    /// </summary>
    /// <remarks>
    /// Layout channels already carry their direct-child identity. Property channels resolve
    /// <see cref="Motion.NameProperty"/> first and fall back to <see cref="StyledElement.Name"/>.
    /// The value is read while a track is created, so an identity mutation invalidates the
    /// owning motion before a replacement runtime is built.
    /// </remarks>
    public string? TargetName => LayoutTarget ?? (PropertyTarget is { } target ? Motion.GetEffectiveName(target) : null);

    /// <summary>
    /// Gets whether the channel reads its endpoint from a live layout template.
    /// </summary>
    public bool IsLayout => LayoutTarget is not null || Property == Motion.DesiredSizeProperty;

    /// <summary>
    /// Creates a layout channel for a named direct panel child.
    /// </summary>
    public static MotionChannel Layout(string target, AvaloniaProperty property) =>
        new(target, null, property);

    /// <summary>
    /// Creates a property channel for a resolved element in the target subtree.
    /// </summary>
    public static MotionChannel ForProperty(StyledElement target, AvaloniaProperty property) =>
        new(null, target, property);
}

/// <summary>
/// Represents one target-property channel in a motion runtime.
/// </summary>
/// <remarks>
/// The non-generic base is the runtime's common ownership boundary and public lifecycle
/// contract. A runtime normally invokes the lifecycle in this order:
/// <c>Prepare</c>, <c>Activate</c>, <c>ValidatePreparedValue</c>, <c>EnablePublication</c>,
/// and then <c>Publish</c> until disposal. The methods are public so custom runtimes and
/// advanced track implementations can participate directly; implementations normally derive
/// from <see cref="MotionTrack{T}"/> to obtain the strongly typed binding implementation.
/// </remarks>
public abstract class MotionTrack : IDisposable
{
    /// <summary>
    /// Gets the object receiving the animation-priority binding.
    /// </summary>
    public AvaloniaObject Target { get; }

    /// <summary>
    /// Gets the animated Avalonia property.
    /// </summary>
    public AvaloniaProperty Property { get; }

    /// <summary>
    /// Initializes an inactive track for one compiled motion channel.
    /// </summary>
    /// <param name="context">The panel-bound compilation context.</param>
    /// <param name="channel">The target-property channel represented by this track.</param>
    protected MotionTrack(MotionCompilationContext context, MotionChannel channel)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(channel.Property);

        var target = context.ResolveTrackTarget(channel);
        var property = channel.Property;

        if (property.IsDirect)
        {
            throw new InvalidOperationException($"{nameof(MotionTrack)} cannot animate direct property '{property}'.");
        }

        if (!AvaloniaPropertyRegistry.Instance.IsRegistered(target, property))
        {
            throw new InvalidOperationException($"Property '{property}' is not registered on motion target '{target.GetType().Name}'.");
        }

        Target = target;
        Property = property;
    }

    /// <summary>
    /// Prepares the track without publishing a value.
    /// </summary>
    /// <remarks>
    /// Preparation captures the current non-animation base value and evaluates the first track
    /// value without sending it to the target. Calling this method again is idempotent for the
    /// built-in typed track implementation.
    /// </remarks>
    public abstract void Prepare();

    /// <summary>
    /// Validates the first prepared value before a candidate is committed.
    /// </summary>
    /// <remarks>
    /// This method is intended to be called after <see cref="Prepare"/> and before
    /// <see cref="EnablePublication"/>. Implementations should throw when their prepared value
    /// is not valid, allowing the owning runtime to discard the candidate safely.
    /// </remarks>
    public abstract void ValidatePreparedValue();

    /// <summary>
    /// Connects the track to its target and runtime publishers.
    /// </summary>
    public abstract void Activate();

    /// <summary>
    /// Enables publication after the candidate has passed preparation.
    /// </summary>
    /// <remarks>
    /// Before this method is called, <see cref="Publish"/> must not change the target. The
    /// built-in implementation also permits <see cref="DisablePublication"/> during rollback.
    /// </remarks>
    public abstract void EnablePublication();

    /// <summary>
    /// Disables publication while a candidate is being rolled back.
    /// </summary>
    /// <remarks>
    /// Disabling publication is safe to repeat and is also used while disposing a failed
    /// candidate.
    /// </remarks>
    public abstract void DisablePublication();

    /// <summary>
    /// Evaluates and publishes the current value.
    /// </summary>
    public abstract void Publish();

    /// <inheritdoc />
    public abstract void Dispose();
}

/// <summary>
/// Owns one typed animation-priority property binding.
/// </summary>
/// <remarks>
/// Derived tracks define only how a value is evaluated. This base class owns the Avalonia
/// binding, follows changes to the non-animation base value, suppresses duplicate publications,
/// and releases the binding with the owning motion runtime.
/// </remarks>
/// <typeparam name="T">The declared Avalonia property value type.</typeparam>
public abstract class MotionTrack<T> : MotionTrack, IObservable<T>
{
    /// <summary>
    /// Gets the animated property.
    /// </summary>
    public new AvaloniaProperty<T> Property { get; }

    private IDisposable? _binding;
    private IObserver<T>? _observer;
    private Optional<T> _lastValue;
    private T _baseValue = default!;
    private readonly MotionInterpolationMetadata _motionInterpolation;
    private bool _isPrepared;
    private bool _isActive;
    private bool _publicationEnabled;
    private bool _disposed;

    /// <summary>
    /// Initializes an inactive track for one compiled motion channel.
    /// </summary>
    /// <param name="context">The panel-bound compilation context.</param>
    /// <param name="channel">The target-property channel owned by this track.</param>
    /// <remarks>
    /// Target resolution, property type validation, and Motion-level interpolation fallback are
    /// completed before the track is registered with its runtime. A track therefore never needs a
    /// second initialization or an attached plan.
    /// </remarks>
    protected MotionTrack(MotionCompilationContext context, MotionChannel channel) : base(context, channel)
    {
        _motionInterpolation = context.GetMotionInterpolation(channel);
        Property = context.GetTrackProperty<T>(channel);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_observer is not null)
        {
            throw new InvalidOperationException("A motion track supports exactly one animation-priority property binding.");
        }

        _observer = observer;
        return new Subscription(this, observer);
    }

    /// <inheritdoc />
    public override void Publish()
    {
        if (_disposed || !_isActive || !_publicationEnabled)
        {
            return;
        }

        var value = EvaluateValue();

        var publishedValue = new Optional<T>(value);
        if (_lastValue == publishedValue)
        {
            return;
        }

        _lastValue = publishedValue;
        _observer?.OnNext(value);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _publicationEnabled = false;
        if (_isActive)
        {
            Target.PropertyChanged -= HandleTargetPropertyChanged;
        }

        _binding?.Dispose();
        _binding = null;
        _observer = null;
    }

    /// <inheritdoc />
    public override void Activate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isActive)
        {
            throw new InvalidOperationException("A motion track can only be activated once.");
        }

        if (!_isPrepared)
        {
            Prepare();
        }

        Target.PropertyChanged += HandleTargetPropertyChanged;
        try
        {
            _binding = Target.Bind(Property, this, BindingPriority.Animation);
            _isActive = true;
        }
        catch
        {
            Target.PropertyChanged -= HandleTargetPropertyChanged;
            _binding?.Dispose();
            _binding = null;
            throw;
        }
    }

    /// <inheritdoc />
    public override void EnablePublication() => _publicationEnabled = true;

    /// <inheritdoc />
    public override void DisablePublication() => _publicationEnabled = false;

    /// <inheritdoc />
    public override void Prepare()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isPrepared)
        {
            return;
        }

        _baseValue = ReadBaseValue();
        EvaluateValue();
        _isPrepared = true;
    }

    /// <inheritdoc />
    public override void ValidatePreparedValue()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isPrepared)
        {
            throw new InvalidOperationException("A motion track must be prepared before its current value is validated.");
        }

        EvaluateValue();
    }

    /// <summary>
    /// Evaluates this track from the property's current non-animation base value.
    /// </summary>
    /// <param name="baseValue">The value that would apply without this animation-priority track.</param>
    /// <returns>The value to publish at animation priority.</returns>
    /// <remarks>
    /// This is the main typed extension point for a custom track. It may be called during
    /// preparation, validation, and publication, so it should be deterministic for the current
    /// runtime state and must return a value accepted by <see cref="Property"/>.
    /// </remarks>
    public abstract T Evaluate(T baseValue);

    /// <summary>
    /// Interpolates two values through the policy compiled for this track's channel.
    /// </summary>
    /// <param name="oldValue">The source endpoint.</param>
    /// <param name="newValue">The destination endpoint.</param>
    /// <param name="progress">The run progress before scoped easing.</param>
    /// <returns>The value produced by the resolved interpolation policy.</returns>
    /// <remarks>
    /// The default implementation uses the track's endpoint, transition, Motion-level, and then
    /// Avalonia fallback policies. Override this method when a track owns a different value-space
    /// algorithm; otherwise custom tracks automatically use the same default interpolator as the
    /// built-in tracks.
    /// </remarks>
    public virtual T InterpolateValue(T oldValue, T newValue, double progress) =>
        InterpolateValue(oldValue, newValue, progress, progress);

    /// <summary>
    /// Interpolates two values with an already inherited outer progress.
    /// </summary>
    /// <param name="oldValue">The source endpoint.</param>
    /// <param name="newValue">The destination endpoint.</param>
    /// <param name="rawProgress">The progress before property or motion easing.</param>
    /// <param name="inheritedProgress">
    /// The progress supplied by an outer transition policy when no more specific easing exists.
    /// </param>
    /// <param name="endpointMetadata">The metadata authored on the destination endpoint.</param>
    /// <param name="transitionMetadata">The metadata supplied by the active transition scope.</param>
    /// <returns>The value produced by the resolved interpolation policy.</returns>
    /// <remarks>
    /// If no explicit interpolator is selected, the default implementation delegates to
    /// <see cref="MotionAvaloniaInterpolator"/>, which adapts Avalonia's registered animator for
    /// the declared property type. A metadata entry whose interpolator is explicitly <see langword="null"/>
    /// intentionally has the same result: it resets to that default policy rather than disabling
    /// interpolation.
    /// </remarks>
    public virtual T InterpolateValue(
        T oldValue,
        T newValue,
        double rawProgress,
        double inheritedProgress,
        MotionInterpolationMetadata endpointMetadata = default,
        MotionInterpolationMetadata transitionMetadata = default)
    {
        var metadata = MergeInterpolation(endpointMetadata, transitionMetadata);
        metadata = MergeInterpolation(metadata, _motionInterpolation);
        var progress = metadata.Easing.HasValue ?
            metadata.Easing.Value?.Ease(rawProgress) ?? rawProgress :
            inheritedProgress;
        var interpolator = metadata.Interpolator.HasValue ?
            metadata.Interpolator.Value ?? MotionAvaloniaInterpolator.Default :
            MotionAvaloniaInterpolator.Default;
        var value = interpolator.InterpolateCore(
            Property,
            progress,
            oldValue,
            newValue);
        return MotionTrackValue.Convert<T>(value);
    }

    private static MotionInterpolationMetadata MergeInterpolation(MotionInterpolationMetadata current, MotionInterpolationMetadata fallback) =>
        new(current.Easing.HasValue ? current.Easing : fallback.Easing, current.Interpolator.HasValue ? current.Interpolator : fallback.Interpolator);

    private T EvaluateValue()
    {
        var value = Evaluate(_baseValue);
        ValidateValue(value);
        return value;
    }

    private void ValidateValue(T value)
    {
        if (!Property.IsValidValue(value))
        {
            throw new InvalidOperationException($"Motion track for property '{Property}' produced an invalid value.");
        }
    }

    private T ReadBaseValue()
    {
        var baseValue = Target.GetBaseValue(Property);
        return MotionTrackValue.Convert<T>(baseValue.HasValue ? baseValue.Value : Target.GetValue(Property));
    }

    private void HandleTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Property && e.Priority > BindingPriority.Animation)
        {
            _baseValue = ReadBaseValue();
        }
    }

    private void Unsubscribe(IObserver<T> observer)
    {
        if (ReferenceEquals(_observer, observer))
        {
            _observer = null;
        }
    }

    private sealed class Subscription(MotionTrack<T> owner, IObserver<T> observer) : IDisposable
    {
        private MotionTrack<T>? _owner = owner;

        public void Dispose()
        {
            _owner?.Unsubscribe(observer);
            _owner = null;
        }
    }
}

/// <summary>
/// Evaluates one sparse, ordered keyframe channel for a sequence or timeline motion.
/// </summary>
/// <remarks>
/// Sequences and timelines both use this same normalized track type. The motion runtime
/// publishes current progress; the track owns segment lookup, endpoint refresh, easing,
/// interpolation, and publication through <see cref="MotionTrack{T}"/>.
/// </remarks>
/// <typeparam name="T">The declared Avalonia property value type.</typeparam>
public sealed class KeyFrameMotionTrack<T> : MotionTrack<T>, IKeyFrameMotionTrack
{
    /// <summary>
    /// Represents one initialized endpoint in a sparse keyframe track.
    /// </summary>
    public sealed class Key
    {
        /// <summary>
        /// Gets the declarative keyframe that supplied this key, when one exists.
        /// </summary>
        public MotionKeyFrame? Source { get; }

        /// <summary>
        /// Gets the normalized cue of this key.
        /// </summary>
        public Cue Cue { get; }

        /// <summary>
        /// Gets the latest endpoint value.
        /// </summary>
        public T Value { get; private set; }

        /// <summary>
        /// Gets the endpoint's explicit easing and interpolation metadata.
        /// </summary>
        public MotionInterpolationMetadata Metadata { get; private set; }

        internal bool HasKeyFrameEasing { get; }

        internal Easing? KeyFrameEasing { get; }

        /// <summary>
        /// Initializes a standalone key for a custom motion runtime.
        /// </summary>
        /// <param name="cue">The normalized key position.</param>
        /// <param name="value">The endpoint value.</param>
        /// <param name="metadata">
        /// Optional easing and interpolation metadata, including explicit-null semantics.
        /// </param>
        public Key(Cue cue, T value, MotionInterpolationMetadata metadata = default)
            : this(null, cue, value, metadata)
        {
        }

        internal Key(MotionKeyFrame source, Cue cue, MotionEndpointValue endpoint)
            : this(
                source,
                cue,
                MotionTrackValue.Convert<T>(endpoint.Value),
                endpoint.Metadata,
                source.IsSet(MotionKeyFrame.EasingProperty),
                source.Easing)
        {
        }

        private Key(
            MotionKeyFrame? source,
            Cue cue,
            T value,
            MotionInterpolationMetadata metadata,
            bool hasKeyFrameEasing = false,
            Easing? keyFrameEasing = null)
        {
            Source = source;
            Cue = cue;
            Value = value;
            Metadata = metadata;
            HasKeyFrameEasing = hasKeyFrameEasing;
            KeyFrameEasing = keyFrameEasing;
        }

        /// <summary>
        /// Updates a key whose endpoint is supplied by a live custom source.
        /// </summary>
        /// <param name="value">The latest endpoint value.</param>
        /// <param name="metadata">The latest endpoint metadata.</param>
        public void Update(T value, MotionInterpolationMetadata metadata = default)
        {
            Value = value;
            Metadata = metadata;
        }

        internal void UpdateValue(MotionEndpointValue endpoint) =>
            Value = MotionTrackValue.Convert<T>(endpoint.Value);
    }

    /// <summary>
    /// Gets the sparse, cue-ordered keys evaluated by this track.
    /// </summary>
    public IReadOnlyList<Key> Keys { get; }

    /// <summary>
    /// Gets the current normalized progress.
    /// </summary>
    public double Progress { get; private set; }

    private readonly Func<MotionKeyFrame, MotionEndpointValue>? _getEndpoint;
    private readonly Easing? _defaultEasing;

    /// <summary>
    /// Initializes a keyframe track for one compiled motion channel.
    /// </summary>
    /// <param name="context">The panel-bound compilation context.</param>
    /// <param name="channel">The target-property channel represented by this track.</param>
    /// <param name="keys">The strictly increasing sparse keys for the channel.</param>
    /// <param name="defaultEasing">The motion-level easing used when a key does not override it.</param>
    /// <param name="getEndpoint">
    /// Optionally refreshes a source key's live endpoint before it is evaluated. This is useful
    /// when a key refers to a live layout source; omit it for standalone custom keys.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public KeyFrameMotionTrack(
        MotionCompilationContext context,
        MotionChannel channel,
        IReadOnlyList<Key> keys,
        Easing? defaultEasing = null,
        Func<MotionKeyFrame, MotionEndpointValue>? getEndpoint = null)
        : base(context, channel)
    {
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
        {
            throw new ArgumentException("A keyframe track must contain at least one key.", nameof(keys));
        }

        for (var i = 1; i < keys.Count; i++)
        {
            if (keys[i].Cue.CueValue <= keys[i - 1].Cue.CueValue)
            {
                throw new ArgumentException("Keyframe track cues must be strictly increasing.", nameof(keys));
            }
        }

        _getEndpoint = getEndpoint;
        _defaultEasing = defaultEasing;
        Keys = keys;
    }

    /// <summary>
    /// Evaluates and publishes this track at one normalized motion progress.
    /// </summary>
    /// <param name="progress">The inclusive normalized progress from zero to one.</param>
    public void Publish(double progress)
    {
        if (!double.IsFinite(progress) || progress is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                "Keyframe motion progress must be finite and between zero and one.");
        }

        Progress = progress;
        base.Publish();
    }

    /// <inheritdoc />
    public override T Evaluate(T baseValue)
    {
        var segment = FindSegment(Progress);
        Refresh(segment.Source);
        if (ReferenceEquals(segment.Source, segment.Target))
        {
            return segment.Source.Value;
        }

        Refresh(segment.Target);
        var easing = segment.Target.HasKeyFrameEasing ? segment.Target.KeyFrameEasing : _defaultEasing;
        return InterpolateValue(
            segment.Source.Value,
            segment.Target.Value,
            segment.Progress,
            easing?.Ease(segment.Progress) ?? segment.Progress,
            segment.Target.Metadata);
    }

    void IKeyFrameMotionTrack.CollectRequiredLayoutSources(Action<IMotionLayoutSource> add)
    {
        var segment = FindSegment(Progress);
        if (segment.Source.Source is { } source)
        {
            add(source);
        }

        if (segment.Target.Source is { } target && !ReferenceEquals(segment.Source.Source, target))
        {
            add(target);
        }
    }

    void IKeyFrameMotionTrack.Publish(double progress) => Publish(progress);

    private void Refresh(Key key)
    {
        if (_getEndpoint is not null && key.Source is { } source)
        {
            key.UpdateValue(_getEndpoint(source));
        }
    }

    private TrackSegment FindSegment(double progress)
    {
        var lower = 0;
        var upper = Keys.Count - 1;
        while (lower <= upper)
        {
            var middle = lower + (upper - lower) / 2;
            if (Keys[middle].Cue.CueValue <= progress)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle - 1;
            }
        }

        var sourceIndex = Math.Max(upper, 0);
        var source = Keys[sourceIndex];
        if (sourceIndex == Keys.Count - 1 || progress <= source.Cue.CueValue)
        {
            return new TrackSegment(source, source, 0);
        }

        var target = Keys[sourceIndex + 1];
        var segmentProgress =
            (progress - source.Cue.CueValue) /
            (target.Cue.CueValue - source.Cue.CueValue);
        return new TrackSegment(source, target, segmentProgress);
    }

    private readonly record struct TrackSegment(
        Key Source,
        Key Target,
        double Progress
    );
}

/// <summary>
/// Supplies the built-in keyframe motion runtime with the layout sources needed by its current segment.
/// </summary>
internal interface IKeyFrameMotionTrack
{
    void Publish(double progress);

    void CollectRequiredLayoutSources(Action<IMotionLayoutSource> add);
}

/// <summary>
/// Converts values at the untyped compilation boundary into a track's declared value type.
/// </summary>
internal static class MotionTrackValue
{
    public static T Convert<T>(object? value)
    {
        if (value is T typedValue)
        {
            return typedValue;
        }

        if (value is null && default(T) is null)
        {
            return default!;
        }

        throw new InvalidOperationException($"Motion endpoint value '{value?.GetType()}' is incompatible with track type '{typeof(T)}'.");
    }
}