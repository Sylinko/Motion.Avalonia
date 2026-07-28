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
    private readonly List<INotifyCollectionChanged> _logicalChildren = [];
    private readonly Action _endpointChanged;
    private readonly Action _structureChanged;
    private readonly MotionStyleProxy _root;
    private IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>>? _capturedValues;
    private bool _captureDirty = true;
    private bool _initialized;
    private bool _disposed;

    public MotionStyleScope(
        StyledElement target,
        Styles styles,
        IReadOnlyList<MotionEndpointDefinition> rootDefinitions,
        Action endpointChanged,
        Action structureChanged)
    {
        _endpointChanged = endpointChanged;
        _structureChanged = structureChanged;
        _root = BuildProxyTree(target);
        try
        {
            MotionStyleCompiler.AddCompiledStyles(styles, _root);
            MotionStyleCompiler.AddRootDefinitions(rootDefinitions, _root);

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

    public IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>> CaptureValues()
    {
        if (!_captureDirty && _capturedValues is not null)
        {
            return _capturedValues;
        }

        var result = _channels.Where(pair => pair.Value.HasValue).GroupBy(pair => pair.Key.Target).ToDictionary(
            group => group.Key,
            IReadOnlyDictionary<AvaloniaProperty, object?> (group) => group.ToDictionary(pair => pair.Key.Property, pair => pair.Value.Value));

        _capturedValues = result;
        _captureDirty = false;
        return result;
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
        _capturedValues = null;
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
                NotifyEndpointChanged();
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
            NotifyEndpointChanged();
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

    private readonly record struct EndpointKey(
        StyledElement Target,
        AvaloniaProperty Property
    );

    private sealed class EndpointChannel : IDisposable
    {
        public bool HasValue { get; private set; }

        public object? Value { get; private set; }

        private readonly StyledElement _target;
        private readonly Action _changed;
        private MotionEndpointDefinition? _definition;
        private IDisposable? _binding;
        private bool _suppressChanges;

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