using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Motion.Avalonia;

/// <summary>
/// Owns one live motion graph for a stable host topology.
/// </summary>
/// <remarks>
/// The runtime owns every animation-priority binding created for the motion. Evaluation changes
/// publish directly through the existing tracks; structural changes dispose the graph and prepare
/// a replacement runtime. Layout sources are declared first, create their own runtime layouts
/// through the compilation context, and are only then converted into tracks. Derive from this type
/// when a custom <see cref="Motion"/> needs runtime-owned behavior without taking ownership of the
/// panel lifecycle.
/// </remarks>
public class MotionRuntime : IDisposable
{
    /// <summary>
    /// Gets the panel-bound compilation context used by this runtime.
    /// </summary>
    /// <remarks>
    /// The context exposes target, layout hosting, and interpolation services without exposing the
    /// concrete motion panel implementation. The panel still owns the runtime's prepare, commit,
    /// and disposal sequence.
    /// </remarks>
    protected internal MotionCompilationContext CompilationContext { get; }

    /// <summary>
    /// Gets the tracks owned by this runtime.
    /// </summary>
    public IReadOnlyList<MotionTrack> Tracks => _tracks;

    internal bool IsBuilt { get; private set; }

    private readonly List<MotionTrack> _tracks = [];
    private readonly List<IMotionLayoutSource> _declaredLayoutSources = [];
    private readonly HashSet<IMotionLayoutSource> _declaredLayoutSourceSet = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<IMotionLayout> _layouts = [];
    private readonly Motion _motion;
    private readonly List<Func<IDisposable>> _activationFactories = [];
    private Action<Action<IMotionLayoutSource>>? _requiredLayoutSourceProvider;
    private readonly List<IMotionLayoutSource?> _requiredLayoutSourceBuffer = [];
    private readonly Action<IMotionLayoutSource> _collectRequiredLayoutSource;
    private readonly List<IDisposable> _activationResources = [];
    private static readonly Dictionary<Type, ITrackTypeDispatcher> TrackTypeDispatchers = [];
    private bool _isBuildingTracks;
    private bool _isPrepared;
    private bool _isCommitted;
    private bool _disposed;

    /// <summary>
    /// Initializes a runtime for one panel-bound motion graph.
    /// </summary>
    /// <param name="context">The host-bound context used to resolve targets and layout sources.</param>
    /// <remarks>
    /// Derived runtimes declare their layout sources during the first construction phase and
    /// create tracks during the second phase after the owning panel has realized those sources.
    /// The owning motion closes both phases before the runtime can be prepared.
    /// </remarks>
    protected internal MotionRuntime(MotionCompilationContext context)
    {
        CompilationContext = context ?? throw new ArgumentNullException(nameof(context));
        _motion = context.Motion;
        _collectRequiredLayoutSource = _requiredLayoutSourceBuffer.Add;
    }

    /// <summary>
    /// Declares one layout source from the owning motion definition.
    /// </summary>
    /// <param name="source">The layout source to register.</param>
    /// <remarks>
    /// Declaration records an existing definition object only. It does not create a runtime layout
    /// or a track. Sources must be declared before the runtime factory returns.
    /// </remarks>
    protected void DeclareLayoutSource(IMotionLayoutSource source) => DeclareLayoutSources([source]);

    /// <summary>
    /// Declares the live layout sources owned by the motion definition.
    /// </summary>
    /// <param name="sources">The existing layout sources to declare as one validated batch.</param>
    /// <remarks>
    /// The declaration set is copied by reference and closed before track construction. Runtime
    /// coordinates may select a subset later, but they cannot add a new source.
    /// </remarks>
    protected void DeclareLayoutSources(IEnumerable<IMotionLayoutSource> sources)
    {
        EnsureDeclarationOpen();
        ArgumentNullException.ThrowIfNull(sources);

        var additions = sources.ToArray();
        var uniqueSources = new HashSet<IMotionLayoutSource>(_declaredLayoutSourceSet, ReferenceEqualityComparer.Instance);
        foreach (var source in additions)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (!uniqueSources.Add(source))
            {
                throw new InvalidOperationException(
                    $"A motion runtime cannot declare the same {nameof(IMotionLayoutSource)} instance more than once.");
            }
        }

        foreach (var source in additions)
        {
            _declaredLayoutSourceSet.Add(source);
        }

        _declaredLayoutSources.AddRange(additions);
    }

    /// <summary>
    /// Adds an inactive track whose binding lifetime will be owned by this runtime.
    /// </summary>
    /// <remarks>
    /// Track construction is intentionally available only from <see cref="BuildTracks"/>. The
    /// runtime first declares and realizes every layout source, so a track can resolve its target
    /// and channel manifest without creating a layout as a side effect.
    /// </remarks>
    /// <typeparam name="TTrack">The concrete track type.</typeparam>
    /// <param name="track">The track to register.</param>
    /// <returns>The registered track.</returns>
    protected TTrack AddTrack<TTrack>(TTrack track) where TTrack : MotionTrack
    {
        EnsureTrackConstruction();
        ArgumentNullException.ThrowIfNull(track);
        _tracks.Add(track);
        return track;
    }

    /// <summary>
    /// Creates and registers a track after the property's value type has been resolved from the
    /// channel.
    /// </summary>
    /// <param name="channel">The compiled output channel.</param>
    /// <returns>The registered track.</returns>
    /// <remarks>
    /// The type bridge is a private implementation detail. The concrete runtime remains
    /// responsible for selecting the track semantics through <see cref="CreateTrackCore{T}"/>.
    /// </remarks>
    protected MotionTrack CreateTrack(MotionChannel channel)
    {
        EnsureTrackConstruction();
        ArgumentNullException.ThrowIfNull(channel.Property);

        var track = GetTrackTypeDispatcher(channel.Property.PropertyType).Create(this, channel) ??
            throw new InvalidOperationException("A motion runtime track creator must return a track.");
        return AddTrack(track);
    }

    /// <summary>
    /// Creates one strongly typed track for a channel discovered by this runtime.
    /// </summary>
    /// <typeparam name="T">The channel property's declared value type.</typeparam>
    /// <param name="channel">The compiled output channel.</param>
    /// <returns>The new track, which the non-generic <see cref="CreateTrack"/> method registers.</returns>
    /// <remarks>
    /// Override this method when the runtime discovers heterogeneous property types. A runtime
    /// that already knows its value type can construct a track directly and pass it to
    /// <see cref="AddTrack{TTrack}"/>.
    /// </remarks>
    protected virtual MotionTrack<T> CreateTrackCore<T>(MotionChannel channel) =>
        throw new NotSupportedException(
            $"{GetType().Name} must override {nameof(CreateTrackCore)} to create a dynamic '{typeof(T).Name}' track.");

    /// <summary>
    /// Registers a factory for a runtime subscription.
    /// </summary>
    /// <param name="factory">Creates a disposable subscription during preparation.</param>
    protected void AddActivation(Func<IDisposable> factory)
    {
        EnsureDefinitionMutable();
        ArgumentNullException.ThrowIfNull(factory);
        _activationFactories.Add(factory);
    }

    /// <summary>
    /// Selects the layout sources required by the current runtime coordinate.
    /// </summary>
    /// <param name="provider">Adds the sources required for the current coordinate.</param>
    /// <remarks>
    /// The provider is invoked synchronously during each required-source collection. It must call
    /// the supplied callback before returning; retaining the callback or invoking it later would
    /// outlive the runtime's reusable selection buffer.
    /// </remarks>
    protected void SetRequiredLayoutSourceProvider(Action<Action<IMotionLayoutSource>> provider)
    {
        EnsureDefinitionMutable();
        ArgumentNullException.ThrowIfNull(provider);
        if (_requiredLayoutSourceProvider is not null)
        {
            throw new InvalidOperationException(
                "A motion runtime can define only one required-layout-source provider.");
        }

        _requiredLayoutSourceProvider = provider;
    }

    /// <summary>
    /// Completes the two-phase runtime construction controlled by the owning motion.
    /// </summary>
    /// <remarks>
    /// The declaration set is closed first. Each source then creates its layout through the
    /// compilation context. Only after every realization succeeds is the derived runtime allowed
    /// to build tracks and read layout frames.
    /// </remarks>
    internal void Build()
    {
        EnsureDefinitionMutable();
        CompilationContext.RealizeLayoutSources(_declaredLayoutSources);
        _layouts = [.. CompilationContext.Layouts];

        _isBuildingTracks = true;
        try
        {
            BuildTracks();
        }
        finally
        {
            _isBuildingTracks = false;
        }

        _tracks.TrimExcess();
        _declaredLayoutSources.TrimExcess();
        _activationFactories.TrimExcess();
        IsBuilt = true;
    }

    /// <summary>
    /// Builds the tracks after all declared layout sources have been realized.
    /// </summary>
    /// <remarks>
    /// This is the second construction phase of one runtime object. It is intentionally separate
    /// from source declaration so a runtime cannot create a layout as a side effect of creating a
    /// track. The default implementation permits an empty runtime.
    /// </remarks>
    protected virtual void BuildTracks() { }

    /// <summary>
    /// Adds the live layout sources needed at the motion's current evaluation coordinate.
    /// </summary>
    internal void CollectRequiredLayoutSources(Action<IMotionLayoutSource> add)
    {
        EnsureBuilt();
        ArgumentNullException.ThrowIfNull(add);

        _requiredLayoutSourceBuffer.Clear();
        if (_requiredLayoutSourceProvider is not null)
        {
            _requiredLayoutSourceProvider(_collectRequiredLayoutSource);
        }
        else
        {
            _requiredLayoutSourceBuffer.AddRange(_declaredLayoutSources);
        }

        foreach (var source in _requiredLayoutSourceBuffer)
        {
            if (source is null || !_declaredLayoutSourceSet.Contains(source))
            {
                var description = source?.Description ?? "<null>";
                throw new InvalidOperationException(
                    $"The required-layout-source provider returned an undeclared {nameof(IMotionLayoutSource)} '{description}'.");
            }

            add(source);
        }
    }

    /// <summary>
    /// Prepares the candidate graph without publishing values to target properties.
    /// </summary>
    internal void Prepare()
    {
        EnsureBuilt();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isPrepared)
        {
            throw new InvalidOperationException("A motion runtime can only be prepared once.");
        }

        foreach (var track in Tracks)
        {
            track.Prepare();
            track.Activate();
        }

        foreach (var factory in _activationFactories)
        {
            _activationResources.Add(
                factory() ?? throw new InvalidOperationException("A motion activation factory must return a lifetime."));
        }

        foreach (var track in Tracks)
        {
            track.ValidatePreparedValue();
        }

        _isPrepared = true;
    }

    /// <summary>
    /// Commits a prepared graph and publishes its current values for the first time.
    /// </summary>
    internal void Commit()
    {
        EnsureBuilt();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isPrepared)
        {
            throw new InvalidOperationException("A motion runtime must be prepared before it can commit.");
        }

        if (_isCommitted)
        {
            throw new InvalidOperationException("A motion runtime can only be committed once.");
        }

        SubscribeLayouts();
        foreach (var track in Tracks)
        {
            track.EnablePublication();
        }

        try
        {
            Publish();
            _isCommitted = true;
        }
        catch
        {
            foreach (var track in Tracks)
            {
                track.DisablePublication();
            }

            UnsubscribeLayouts();
            throw;
        }
    }

    /// <summary>
    /// Runs commit-only reconciliation after the previous runtime has been released.
    /// </summary>
    internal void CompleteCommit()
    {
        EnsureBuilt();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isCommitted)
        {
            throw new InvalidOperationException("A motion runtime must be committed before completion.");
        }

        OnCommitted();
    }

    /// <summary>
    /// Called after this runtime has been committed and the previous runtime has been released.
    /// </summary>
    /// <remarks>
    /// This is a notification hook, not a replacement for track publication. The panel does not
    /// roll back a committed runtime if an override throws.
    /// </remarks>
    protected virtual void OnCommitted() { }

    /// <summary>
    /// Evaluates and publishes every track in the motion runtime.
    /// </summary>
    internal void Publish()
    {
        EnsureBuilt();
        foreach (var track in Tracks)
        {
            track.Publish();
        }
    }

    internal void PublishMeasureTracks()
    {
        EnsureBuilt();
        foreach (var track in Tracks)
        {
            if (track.Property == Motion.DesiredSizeProperty)
            {
                track.Publish();
            }
        }
    }

    /// <summary>
    /// Notifies the motion runtime that its latest values have completed a layout presentation.
    /// </summary>
    internal void Presented()
    {
        EnsureBuilt();
        OnPresented();
    }

    /// <summary>
    /// Called after the panel has presented the runtime's latest layout values.
    /// </summary>
    protected virtual void OnPresented() { }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isCommitted = false;
        try
        {
            UnsubscribeLayouts();
            for (var i = _activationResources.Count - 1; i >= 0; i--)
            {
                _activationResources[i].Dispose();
            }

            _activationResources.Clear();
            foreach (var track in Tracks)
            {
                track.Dispose();
            }

            _requiredLayoutSourceBuffer.Clear();
            _declaredLayoutSourceSet.Clear();
            _declaredLayoutSources.Clear();
            CompilationContext.Dispose();
        }
        finally
        {
            OnDisposed();
        }
    }

    /// <summary>
    /// Called after this runtime has released its tracks, subscriptions, and compilation context.
    /// </summary>
    protected virtual void OnDisposed() { }

    private void EnsureBuilt()
    {
        if (!IsBuilt)
        {
            throw new InvalidOperationException("A motion runtime must be built before it can be used.");
        }
    }

    private void EnsureDefinitionMutable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBuilt)
        {
            throw new InvalidOperationException("A motion runtime cannot be changed after it has been built.");
        }
    }

    private void EnsureDeclarationOpen()
    {
        EnsureDefinitionMutable();
        if (_isBuildingTracks)
        {
            throw new InvalidOperationException("A motion runtime cannot declare a layout source after BuildTracks has started.");
        }
    }

    private void EnsureTrackConstruction()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isBuildingTracks)
        {
            throw new InvalidOperationException("Motion tracks can only be created during BuildTracks.");
        }
    }

    private void SubscribeLayouts()
    {
        foreach (var layout in _layouts)
        {
            layout.EndpointChanged += OnLayoutEndpointChanged;
            layout.EndpointStructureChanged += OnLayoutEndpointStructureChanged;
        }
    }

    private void UnsubscribeLayouts()
    {
        foreach (var layout in _layouts)
        {
            layout.EndpointChanged -= OnLayoutEndpointChanged;
            layout.EndpointStructureChanged -= OnLayoutEndpointStructureChanged;
        }
    }

    private void OnLayoutEndpointChanged(object? sender, EventArgs e) => Publish();

    private void OnLayoutEndpointStructureChanged(object? sender, EventArgs e) => _motion.InvalidateHost();

    private static ITrackTypeDispatcher GetTrackTypeDispatcher(Type propertyType)
    {
        if (TrackTypeDispatchers.TryGetValue(propertyType, out var dispatcher))
        {
            return dispatcher;
        }

        var dispatcherType = typeof(TrackTypeDispatcher<>).MakeGenericType(propertyType);
        dispatcher = (ITrackTypeDispatcher)Activator.CreateInstance(dispatcherType)!;
        TrackTypeDispatchers.Add(propertyType, dispatcher);
        return dispatcher;
    }

    private interface ITrackTypeDispatcher
    {
        MotionTrack Create(MotionRuntime runtime, MotionChannel channel);
    }

    private sealed class TrackTypeDispatcher<T> : ITrackTypeDispatcher
    {
        public MotionTrack Create(MotionRuntime runtime, MotionChannel channel) =>
            runtime.CreateTrackCore<T>(channel);
    }
}

/// <summary>
/// Supplies panel-owned targets, live layout frames, and interpolation policy while a motion is
/// being compiled and evaluated.
/// </summary>
public sealed class MotionCompilationContext
{
    private readonly MotionItemsPanel _panel;
    private readonly Motion _motion;
    private readonly Dictionary<InterpolationLookup, MotionInterpolationMetadata> _motionInterpolations = [];
    private readonly Dictionary<IMotionLayoutSource, IMotionLayout> _layouts = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IMotionLayoutSource, Size> _measuredSizes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IMotionLayoutSource, Size> _capturedLayoutSizes = new(ReferenceEqualityComparer.Instance);
    private bool _hasPresentedLayoutSources;
    private bool _layoutSourcesRealized;
    private bool _disposed;
    private Size _presentedLayoutSourceSize;

    internal MotionCompilationContext(MotionItemsPanel panel, Motion motion)
    {
        _panel = panel;
        _motion = motion;
        BuildInterpolationIndex(motion.Interpolations, _motionInterpolations, motion.GetType().Name);
    }

    internal Motion Motion => _motion;

    internal MotionItemsPanel Panel => _panel;

    internal int TargetCount => _panel.TargetCount;

    internal IReadOnlyCollection<IMotionLayout> Layouts => _layouts.Values;

    /// <summary>
    /// Gets the data context inherited by layout roots attached to the owning panel.
    /// </summary>
    public object? DataContext => _panel.DataContext;

    /// <summary>
    /// Creates the built-in live layout implementation for an Avalonia data template.
    /// </summary>
    /// <param name="template">The template whose control tree supplies layout slots.</param>
    /// <param name="description">A concise description used by validation diagnostics.</param>
    /// <returns>A runtime-owned attached layout.</returns>
    public IMotionLayout CreateTemplateLayout(
        IDataTemplate? template,
        string description)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return MotionPanelHost.Create(this, template, description);
    }

    /// <summary>
    /// Attaches a custom layout root to the owning panel's logical and visual trees.
    /// </summary>
    /// <remarks>
    /// The attached root inherits the panel's Avalonia context and presentation source, including
    /// resources, theme, visual-tree lifetime, and layout scaling. The returned lifetime must be
    /// disposed by the <see cref="IMotionLayout"/> that requested the attachment.
    /// </remarks>
    /// <param name="root">The unattached layout root.</param>
    /// <returns>A lifetime that removes the root from both trees.</returns>
    public IDisposable AttachLayoutRoot(Control root)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(root);
        return _panel.AttachLayoutRoot(root);
    }

    /// <summary>
    /// Realizes the declared live layout templates used by the motion runtime.
    /// </summary>
    internal void RealizeLayoutSources(IReadOnlyList<IMotionLayoutSource> sources)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sources);

        if (_layoutSourcesRealized)
        {
            throw new InvalidOperationException("Layout sources can only be realized once for a compilation context.");
        }

        var sourceSet = new HashSet<IMotionLayoutSource>(ReferenceEqualityComparer.Instance);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (!sourceSet.Add(source))
            {
                throw new InvalidOperationException(
                    $"{_motion.GetType().Name} cannot contain the same {nameof(IMotionLayoutSource)} instance more than once.");
            }
        }

        _layoutSourcesRealized = true;

        foreach (var source in sources)
        {
            var layout = source.CreateLayout(this) ??
                throw new InvalidOperationException($"{source.Description} returned a null motion layout.");
            try
            {
                _layouts.Add(source, layout);
            }
            catch
            {
                layout.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// Gets the latest reusable layout frame captured for a source.
    /// </summary>
    public MotionLayoutFrame GetLayoutFrame(IMotionLayoutSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);
        if (!_layouts.TryGetValue(source, out var layout))
        {
            throw new InvalidOperationException($"{source.Description} is not registered with the current motion compilation.");
        }

        return layout.CaptureLayout();
    }

    /// <summary>
    /// Gets the latest desired size measured for a source.
    /// </summary>
    public Size GetMeasuredSize(IMotionLayoutSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);
        return _measuredSizes.GetValueOrDefault(source);
    }

    internal void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var pair in _layouts.ToArray())
        {
            RemoveLayout(pair.Key, pair.Value);
        }

        _measuredSizes.Clear();
        _capturedLayoutSizes.Clear();
    }

    /// <summary>
    /// Resolves the target object for a compiled track channel.
    /// </summary>
    /// <param name="channel">The compiled output channel.</param>
    internal AvaloniaObject ResolveTrackTarget(MotionChannel channel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(channel.Property);
        return _panel.ResolveTrackDestination(channel);
    }

    /// <summary>
    /// Resolves and validates the strongly typed property for a compiled track channel.
    /// </summary>
    /// <typeparam name="T">The channel property's declared value type.</typeparam>
    /// <param name="channel">The compiled output channel.</param>
    internal AvaloniaProperty<T> GetTrackProperty<T>(MotionChannel channel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return channel.Property as AvaloniaProperty<T> ??
            throw new InvalidOperationException(
                $"Channel property '{channel.Property}' does not declare value type '{typeof(T)}'.");
    }

    /// <summary>
    /// Gets the Motion-level interpolation fallback for one channel.
    /// </summary>
    /// <param name="channel">The compiled output channel.</param>
    internal MotionInterpolationMetadata GetMotionInterpolation(MotionChannel channel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var lookup = new InterpolationLookup(channel.TargetName, channel.Property);
        return _motionInterpolations.GetValueOrDefault(lookup);
    }

    /// <summary>
    /// Requests layout when the sources required by the current runtime coordinate are not ready.
    /// </summary>
    /// <remarks>
    /// Custom motions should call this after publishing a coordinate that may select a different
    /// subset of the layout sources registered with their runtime.
    /// </remarks>
    public void EnsureRequiredLayoutSources()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _panel.EnsureRequiredLayoutSources(this);
    }

    /// <summary>
    /// Resolves a stable direct panel child by the effective Motion identity used by motion
    /// layout slots.
    /// </summary>
    /// <param name="name">The non-empty Motion identity of the target child.</param>
    /// <returns>The resolved live target control.</returns>
    public Control ResolveTarget(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _panel.ResolveTarget(name);
    }

    internal void MeasureLayoutSources(IReadOnlyList<IMotionLayoutSource> sources, Size availableSize)
    {
        _measuredSizes.Clear();
        foreach (var source in sources)
        {
            var layout = GetLayout(source);
            layout.Measure(availableSize);
            _measuredSizes.Add(source, layout.DesiredSize);
        }
    }

    internal void CaptureLayoutSources(IReadOnlyList<IMotionLayoutSource> sources, Size finalSize)
    {
        foreach (var source in sources)
        {
            var layout = GetLayout(source);
            layout.Arrange(new Rect(finalSize));
            layout.CaptureLayout();
            _capturedLayoutSizes[source] = finalSize;
        }

        _hasPresentedLayoutSources = true;
        _presentedLayoutSourceSize = finalSize;
    }

    internal bool AreLayoutSourcesReady(IReadOnlyList<IMotionLayoutSource> sources)
    {
        if (!_hasPresentedLayoutSources)
        {
            return false;
        }

        foreach (var source in sources)
        {
            var layout = GetLayout(source);
            if (!layout.IsMeasureValid ||
                !layout.IsArrangeValid ||
                !_measuredSizes.ContainsKey(source) ||
                !_capturedLayoutSizes.TryGetValue(source, out var capturedSize) ||
                capturedSize != _presentedLayoutSourceSize)
            {
                return false;
            }
        }

        return true;
    }

    private IMotionLayout GetLayout(IMotionLayoutSource source) =>
        _layouts.TryGetValue(source, out var layout) ?
            layout :
            throw new InvalidOperationException($"{source.Description} is not registered with the current motion compilation.");

    private void RemoveLayout(IMotionLayoutSource source, IMotionLayout layout)
    {
        _layouts.Remove(source);
        _capturedLayoutSizes.Remove(source);
        _measuredSizes.Remove(source);
        layout.Dispose();
    }

    internal static void BuildInterpolationIndex(
        IReadOnlyCollection<MotionInterpolation> source,
        Dictionary<InterpolationLookup, MotionInterpolationMetadata> destination,
        string ownerName)
    {
        foreach (var interpolation in source)
        {
            ValidateInterpolation(interpolation, ownerName);
            var lookup = new InterpolationLookup(interpolation.Target, interpolation.Property!);
            if (!destination.TryAdd(lookup, interpolation.GetEndpointMetadata()))
            {
                throw new InvalidOperationException(
                    $"{ownerName} contains more than one interpolation for {DescribeChannel(interpolation.Target, interpolation.Property!)}.");
            }
        }
    }

    private static void ValidateInterpolation(MotionInterpolation interpolation, string ownerName)
    {
        if (interpolation.Property is not { } property)
        {
            throw new InvalidOperationException($"Every {nameof(MotionInterpolation)} in {ownerName} must specify a Property.");
        }

        if (Motion.IsPanelProperty(property))
        {
            if (interpolation.Target is not null)
            {
                throw new InvalidOperationException(
                    $"{nameof(Motion)}.{property.Name} is panel-wide, so its interpolation must not specify a Target.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(interpolation.Target))
        {
            throw new InvalidOperationException($"An interpolation for property '{property}' must specify a non-empty Target.");
        }
    }

    private static string DescribeChannel(string? target, AvaloniaProperty property) =>
        target is null ? $"panel property '{property}'" : $"target '{target}', property '{property}'";

    /// <summary>
    /// Identifies one target/property interpolation entry in a runtime-local lookup table.
    /// </summary>
    /// <remarks>
    /// The lookup is intentionally scoped to one runtime and stores only authoring metadata; it
    /// does not retain a layout realization or a live target reference.
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    internal readonly record struct InterpolationLookup(
        string? Target,
        AvaloniaProperty Property
    );
}