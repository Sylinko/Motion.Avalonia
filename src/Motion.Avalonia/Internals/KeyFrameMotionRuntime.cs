namespace Motion.Avalonia;

/// <summary>
/// Builds and evaluates the shared sparse tracks used by sequence and timeline motions.
/// </summary>
internal sealed class KeyFrameMotionRuntime<TMotion> : MotionRuntime where TMotion : Motion, IKeyFrameMotion
{
    private readonly MotionCompilationContext _context;
    private readonly TMotion _motion;
    private readonly MotionKeyFrame[] _keyFrames;
    private readonly List<IKeyFrameMotionTrack> _keyFrameTracks = [];

    internal KeyFrameMotionRuntime(MotionCompilationContext context, TMotion motion) : base(context)
    {
        _context = context;
        _motion = motion;
        _keyFrames = [.. _motion.KeyFrames];
        DeclareLayoutSources(_keyFrames);
    }

    protected override void BuildTracks()
    {
        foreach (var channel in DiscoverChannels(_keyFrames))
        {
            var track = CreateTrack(channel);
            if (track is not IKeyFrameMotionTrack keyFrameTrack)
            {
                throw new InvalidOperationException($"Keyframe track '{track.GetType().Name}' does not support keyframe motion execution.");
            }

            _keyFrameTracks.Add(keyFrameTrack);
        }

        SetRequiredLayoutSourceProvider(CollectRequiredKeyFrameLayoutSources);
        AddActivation(() => _motion.Subscribe(new ProgressObserver(this)));
    }

    protected override MotionTrack<T> CreateTrackCore<T>(MotionChannel channel)
    {
        var keys = BuildKeys<T>(channel);
        return new KeyFrameMotionTrack<T>(_context, channel, keys, _motion.Easing, keyFrame => GetEndpoint(channel, keyFrame));
    }

    protected override void OnDisposed() => _keyFrameTracks.Clear();

    private void CollectRequiredKeyFrameLayoutSources(Action<IMotionLayoutSource> add)
    {
        foreach (var track in _keyFrameTracks)
        {
            track.CollectRequiredLayoutSources(add);
        }
    }

    private void Publish(double progress)
    {
        if (!double.IsFinite(progress) || progress is < 0 or > 1)
        {
            throw new InvalidOperationException($"{_motion.GetType().Name} published invalid normalized progress '{progress}'.");
        }

        foreach (var track in _keyFrameTracks)
        {
            track.Publish(progress);
        }

        _context.EnsureRequiredLayoutSources();
    }

    private MotionEndpointValue GetEndpoint(MotionChannel channel, MotionKeyFrame keyFrame)
    {
        if (channel.Property == Motion.DesiredSizeProperty)
        {
            return new MotionEndpointValue(_context.GetMeasuredSize(keyFrame), default);
        }

        var frame = _context.GetLayoutFrame(keyFrame);
        if (channel.LayoutTarget is { } targetName)
        {
            var entry = frame.Entries[targetName];
            if (channel.Property == Motion.PositionProperty)
            {
                return new MotionEndpointValue(entry.Bounds.Position, entry.PositionMetadata);
            }

            if (channel.Property == Motion.SizeProperty)
            {
                return new MotionEndpointValue(entry.Bounds.Size, entry.SizeMetadata);
            }

            return new MotionEndpointValue(entry.Presence, entry.PresenceMetadata);
        }

        return frame.PropertyValues[channel.PropertyTarget!][channel.Property];
    }

    private KeyFrameMotionTrack<T>.Key[] BuildKeys<T>(MotionChannel channel)
    {
        var result = new List<KeyFrameMotionTrack<T>.Key>();
        foreach (var keyFrame in _keyFrames)
        {
            var cue = _motion.GetCue(keyFrame);
            if (!TryGetEndpoint(channel, keyFrame, out var endpoint))
            {
                continue;
            }

            var key = new KeyFrameMotionTrack<T>.Key(keyFrame, cue, endpoint);
            if (result.Count > 0 && result[^1].Cue == cue)
            {
                result[^1] = key;
            }
            else
            {
                result.Add(key);
            }
        }

        return [.. result];
    }

    private List<MotionChannel> DiscoverChannels(MotionKeyFrame[] keyFrames)
    {
        var channels = new List<MotionChannel>();
        var channelSet = new HashSet<MotionChannel>();
        foreach (var keyFrame in keyFrames)
        {
            AddChannel(channels, channelSet, MotionChannel.DesiredSize);
            var frame = _context.GetLayoutFrame(keyFrame);
            foreach (var entry in frame.Entries)
            {
                AddChannel(channels, channelSet, MotionChannel.Layout(entry.Key, Motion.PositionProperty));
                AddChannel(channels, channelSet, MotionChannel.Layout(entry.Key, Motion.SizeProperty));
                AddChannel(channels, channelSet, MotionChannel.Layout(entry.Key, Motion.PresenceProperty));
            }

            foreach (var targetValues in frame.PropertyValues)
            {
                foreach (var propertyValue in targetValues.Value)
                {
                    AddChannel(channels, channelSet, MotionChannel.ForProperty(targetValues.Key, propertyValue.Key));
                }
            }
        }

        return channels;
    }

    private bool TryGetEndpoint(MotionChannel channel, MotionKeyFrame keyFrame, out MotionEndpointValue endpoint)
    {
        if (channel.Property == Motion.DesiredSizeProperty)
        {
            endpoint = new MotionEndpointValue(_context.GetMeasuredSize(keyFrame), default);
            return true;
        }

        var frame = _context.GetLayoutFrame(keyFrame);
        if (channel.LayoutTarget is { } targetName)
        {
            if (!frame.Entries.TryGetValue(targetName, out var entry))
            {
                endpoint = default;
                return false;
            }

            endpoint = channel.Property switch
            {
                _ when channel.Property == Motion.PositionProperty => new MotionEndpointValue(entry.Bounds.Position, entry.PositionMetadata),
                _ when channel.Property == Motion.SizeProperty => new MotionEndpointValue(entry.Bounds.Size, entry.SizeMetadata),
                _ => new MotionEndpointValue(entry.Presence, entry.PresenceMetadata),
            };
            return true;
        }

        if (channel.PropertyTarget is { } propertyTarget &&
            frame.PropertyValues.TryGetValue(propertyTarget, out var values) &&
            values.TryGetValue(channel.Property, out endpoint))
        {
            return true;
        }

        endpoint = default;
        return false;
    }

    private static void AddChannel(List<MotionChannel> channels, HashSet<MotionChannel> channelSet, MotionChannel channel)
    {
        if (channelSet.Add(channel))
        {
            channels.Add(channel);
        }
    }

    /// <summary>
    /// Forwards the motion's normalized progress stream into the compiled track graph.
    /// </summary>
    private sealed class ProgressObserver(KeyFrameMotionRuntime<TMotion> owner) : IObserver<double>
    {
        public void OnNext(double value) => owner.Publish(value);

        public void OnError(Exception error) =>
            throw new InvalidOperationException($"{typeof(TMotion).Name} terminated its progress stream with an error.", error);

        public void OnCompleted()
        {
        }
    }
}