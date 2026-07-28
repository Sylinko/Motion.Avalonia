using Avalonia;
using Avalonia.Data;

namespace Motion.Avalonia;

/// <summary>
/// Applies pose-evaluated property values without replacing a target's base values.
/// </summary>
internal sealed class MotionPropertyTarget(
    StyledElement target,
    MotionPanelItemContainer? layoutContainer
) : IDisposable
{
    private readonly Dictionary<AvaloniaProperty, MotionPropertyOutput> _outputs = [];
    private readonly List<AvaloniaProperty> _obsoleteProperties = [];

    public object? GetBaseValue(AvaloniaProperty property) =>
        GetOrCreateOutput(property).BaseValue;

    public void Apply(IReadOnlyDictionary<AvaloniaProperty, object?> values)
    {
        foreach (var pair in values)
        {
            GetOrCreateOutput(pair.Key).Publish(pair.Value);
        }

        _obsoleteProperties.Clear();
        foreach (var property in _outputs.Keys)
        {
            if (!values.ContainsKey(property))
            {
                _obsoleteProperties.Add(property);
            }
        }

        foreach (var property in _obsoleteProperties)
        {
            if (_outputs.Remove(property, out var output))
            {
                output.Dispose();
            }
        }
    }

    public void Dispose()
    {
        foreach (var output in _outputs.Values)
        {
            output.Dispose();
        }

        _outputs.Clear();
    }

    private MotionPropertyOutput GetOrCreateOutput(AvaloniaProperty property)
    {
        if (_outputs.TryGetValue(property, out var result))
        {
            return result;
        }

        if (property.IsDirect)
        {
            throw new InvalidOperationException($"{nameof(MotionPanel)} cannot animate direct property '{property}'.");
        }

        var destination = property == Visual.ZIndexProperty && layoutContainer is not null ? layoutContainer : target;
        if (!AvaloniaPropertyRegistry.Instance.IsRegistered(destination, property))
        {
            throw new InvalidOperationException(
                $"Property '{property}' is not registered on motion target '{target.Name ?? target.GetType().Name}'.");
        }

        result = new MotionPropertyOutput(destination, property);
        _outputs.Add(property, result);
        return result;
    }

    private sealed class MotionPropertyOutput : IDisposable
    {
        public object? BaseValue { get; private set; }

        private readonly AvaloniaObject _destination;
        private readonly AvaloniaProperty _property;
        private readonly MotionValueSubject _subject = new();
        private readonly IDisposable _binding;

        public MotionPropertyOutput(AvaloniaObject destination, AvaloniaProperty property)
        {
            _destination = destination;
            _property = property;
            BaseValue = destination.GetValue(property);
            destination.PropertyChanged += OnPropertyChanged;
            _binding = destination.Bind(property, _subject, BindingPriority.Animation);
        }

        public void Publish(object? value) => _subject.Publish(value);

        public void Dispose()
        {
            _destination.PropertyChanged -= OnPropertyChanged;
            _binding.Dispose();
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == _property && e.Priority != BindingPriority.Animation)
            {
                BaseValue = e.NewValue;
            }
        }
    }

    private sealed class MotionValueSubject : IObservable<object?>
    {
        private IObserver<object?>[] _observers = [];
        private object? _value;
        private bool _hasValue;

        public IDisposable Subscribe(IObserver<object?> observer)
        {
            var observers = new IObserver<object?>[_observers.Length + 1];
            _observers.CopyTo(observers, 0);
            observers[^1] = observer;
            _observers = observers;

            if (_hasValue)
            {
                observer.OnNext(_value);
            }

            return new Subscription(this, observer);
        }

        public void Publish(object? value)
        {
            _value = value;
            _hasValue = true;

            foreach (var observer in _observers)
            {
                observer.OnNext(value);
            }
        }

        private void Unsubscribe(IObserver<object?> observer)
        {
            var index = Array.IndexOf(_observers, observer);
            if (index < 0)
            {
                return;
            }

            if (_observers.Length == 1)
            {
                _observers = [];
                return;
            }

            var observers = new IObserver<object?>[_observers.Length - 1];
            Array.Copy(_observers, 0, observers, 0, index);
            Array.Copy(_observers, index + 1, observers, index, _observers.Length - index - 1);
            _observers = observers;
        }

        private sealed class Subscription(MotionValueSubject owner, IObserver<object?> observer) : IDisposable
        {
            private MotionValueSubject? _owner = owner;

            public void Dispose()
            {
                _owner?.Unsubscribe(observer);
                _owner = null;
            }
        }
    }
}