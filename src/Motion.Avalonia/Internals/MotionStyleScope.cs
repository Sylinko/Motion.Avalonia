using System.Collections.Specialized;
using Avalonia;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Motion.Avalonia;

/// <summary>
/// Owns the isolated style tree and live endpoint values for one slot and one real target subtree.
/// </summary>
/// <remarks>
/// A scope lives as long as its slot targets the same subtree. It is rebuilt when the target
/// structure or style definitions change and disposes all endpoint binding expressions.
/// </remarks>
internal sealed class MotionStyleScope : IDisposable
{
    private readonly Dictionary<StyledElement, MotionStyleProxy> _proxies = [];
    private readonly Dictionary<EndpointKey, EndpointChannel> _channels = [];
    private readonly Dictionary<StyledElement, Dictionary<AvaloniaProperty, MotionEndpointValue>> _captureBuffers = [];
    private readonly Dictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, MotionEndpointValue>> _capturedValues = [];
    private readonly List<Dictionary<AvaloniaProperty, MotionEndpointValue>> _activeCaptureBuffers = [];
    private readonly List<INotifyCollectionChanged> _logicalChildren = [];
    private readonly Action _endpointChanged;
    private readonly Action _endpointStructureChanged;
    private readonly Action _structureChanged;
    private readonly MotionStyleProxy _root;
    private readonly bool _initialized;
    private bool _captureDirty = true;
    private bool _disposed;

    public MotionStyleScope(
        StyledElement target,
        Styles styles,
        IReadOnlyList<MotionEndpointDefinition> rootDefinitions,
        Action endpointChanged,
        Action endpointStructureChanged,
        Action structureChanged)
    {
        _endpointChanged = endpointChanged;
        _endpointStructureChanged = endpointStructureChanged;
        _structureChanged = structureChanged;
        _root = BuildProxyTree(target);
        try
        {
            _root.CopyStyles(styles);
            _root.AddRootDefinitions(rootDefinitions);

            foreach (var proxy in _proxies.Values)
            {
                proxy.PropertyChanged += OnProxyPropertyChanged;
            }

            _root.ApplyStylesRecursively();
            _initialized = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, MotionEndpointValue>> CaptureValues()
    {
        if (!_captureDirty)
        {
            return _capturedValues;
        }

        foreach (var values in _activeCaptureBuffers)
        {
            values.Clear();
        }

        _activeCaptureBuffers.Clear();
        _capturedValues.Clear();
        foreach (var pair in _channels)
        {
            if (!pair.Value.HasValue)
            {
                continue;
            }

            if (!_captureBuffers.TryGetValue(pair.Key.Target, out var values))
            {
                values = [];
                _captureBuffers.Add(pair.Key.Target, values);
            }

            if (!_capturedValues.ContainsKey(pair.Key.Target))
            {
                values.Clear();
                _capturedValues.Add(pair.Key.Target, values);
                _activeCaptureBuffers.Add(values);
            }

            values.Add(pair.Key.Property, new MotionEndpointValue(pair.Value.Value, pair.Value.Metadata));
        }

        _captureDirty = false;
        return _capturedValues;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var children in _logicalChildren)
        {
            children.CollectionChanged -= OnLogicalChildrenChanged;
        }

        foreach (var proxy in _proxies.Values)
        {
            proxy.PropertyChanged -= OnProxyPropertyChanged;
        }

        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }

        _channels.Clear();
        _proxies.Clear();
        _captureBuffers.Clear();
        _capturedValues.Clear();
        _activeCaptureBuffers.Clear();
        _root.Dispose();
    }

    private MotionStyleProxy BuildProxyTree(StyledElement target)
    {
        var result = new MotionStyleProxy(target);
        _proxies.Add(target, result);

        var children = ((ILogical)target).LogicalChildren;
        if (children is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnLogicalChildrenChanged;
            _logicalChildren.Add(observable);
        }

        foreach (var child in children.OfType<StyledElement>())
        {
            result.AddChild(BuildProxyTree(child));
        }

        return result;
    }

    private void OnProxyPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not MotionStyleProxy proxy || !MotionShadowPropertyRegistry.TryGetSource(e.Property, out var property))
        {
            return;
        }

        var definition = e.NewValue as MotionEndpointDefinition;
        var key = new EndpointKey(proxy.Target, property);

        if (definition is null)
        {
            if (_channels.Remove(key, out var oldChannel))
            {
                oldChannel.Dispose();
                NotifyEndpointStructureChanged();
            }

            return;
        }

        if (_channels.TryGetValue(key, out var channel))
        {
            channel.SetDefinition(definition);
        }
        else
        {
            channel = new EndpointChannel(proxy.Target, definition, NotifyEndpointChanged);
            _channels.Add(key, channel);
            NotifyEndpointStructureChanged();
        }
    }

    private void OnLogicalChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_disposed)
        {
            _structureChanged();
        }
    }

    private void NotifyEndpointChanged()
    {
        _captureDirty = true;

        if (_initialized && !_disposed)
        {
            _endpointChanged();
        }
    }

    private void NotifyEndpointStructureChanged()
    {
        _captureDirty = true;

        if (_initialized && !_disposed)
        {
            _endpointStructureChanged();
        }
    }

    private readonly record struct EndpointKey(
        StyledElement Target,
        AvaloniaProperty Property
    );

    private sealed class EndpointChannel : IDisposable
    {
        public bool HasValue { get; private set; }

        public object? Value { get; private set; }

        public MotionInterpolationMetadata Metadata => _definition?.Metadata ?? default;

        private readonly StyledElement _target;
        private readonly Action _changed;
        private readonly bool _suppressChanges;
        private MotionEndpointDefinition? _definition;
        private IDisposable? _binding;

        public EndpointChannel(StyledElement target, MotionEndpointDefinition definition, Action changed)
        {
            _target = target;
            _changed = changed;
            _suppressChanges = true;
            try
            {
                SetDefinition(definition);
            }
            finally
            {
                _suppressChanges = false;
            }
        }

        public void SetDefinition(MotionEndpointDefinition definition)
        {
            if (Equals(_definition, definition))
            {
                return;
            }

            _definition = definition;
            _binding?.Dispose();
            _binding = null;

            if (!AvaloniaPropertyRegistry.Instance.IsRegistered(_target, definition.Property))
            {
                throw new InvalidOperationException(
                    $"Property '{definition.Property}' is not registered on motion target '{_target.Name ?? _target.GetType().Name}'.");
            }

            if (definition.Value is BindingBase binding)
            {
                SetValue(AvaloniaProperty.UnsetValue);
                _binding = MotionBindingEvaluator.Observe(binding, _target, definition.Property, SetValue);
                return;
            }

            if (!definition.Property.IsValidValue(definition.Value))
            {
                throw new InvalidOperationException($"Value '{definition.Value}' is not valid for property '{definition.Property}'.");
            }

            SetValue(definition.Value);
        }

        public void Dispose()
        {
            _binding?.Dispose();
            _binding = null;
        }

        private void SetValue(object? value)
        {
            if (value is BindingNotification notification)
            {
                value = notification.HasValue ? notification.Value : AvaloniaProperty.UnsetValue;
            }

            var hasValue = value != AvaloniaProperty.UnsetValue && value != BindingOperations.DoNothing;
            if (HasValue == hasValue && (!hasValue || Equals(Value, value)))
            {
                return;
            }

            HasValue = hasValue;
            Value = hasValue ? value : null;
            if (!_suppressChanges)
            {
                _changed();
            }
        }
    }
}