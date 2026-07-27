using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Motion.Avalonia;

/// <summary>
/// Applies pose-evaluated property values without replacing a target's base values.
/// </summary>
internal sealed class MotionPanelPropertyTarget(Control target, MotionPanelItemContainer container) : IDisposable
{
    private readonly Dictionary<AvaloniaProperty, MotionPropertyOutput> _outputs = [];

    public object? GetBaseValue(AvaloniaProperty property) =>
        GetOrCreateOutput(property).BaseValue;

    public void Apply(IReadOnlyDictionary<AvaloniaProperty, object?> values)
    {
        foreach (var pair in values)
        {
            GetOrCreateOutput(pair.Key).Publish(pair.Value);
        }

        foreach (var property in _outputs.Keys.Where(property => !values.ContainsKey(property)).ToArray())
        {
            if (_outputs.Remove(property, out var output)) output.Dispose();
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

        var destination = property == Visual.ZIndexProperty ? container : target;
        if (!AvaloniaPropertyRegistry.Instance.IsRegistered(destination, property))
        {
            throw new InvalidOperationException($"Property '{property}' is not registered on motion target '{target.Name}'.");
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
        private readonly List<IObserver<object?>> _observers = [];
        private object? _value;
        private bool _hasValue;

        public IDisposable Subscribe(IObserver<object?> observer)
        {
            _observers.Add(observer);
            if (_hasValue)
            {
                observer.OnNext(_value);
            }

            return new Subscription(_observers, observer);
        }

        public void Publish(object? value)
        {
            _value = value;
            _hasValue = true;

            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        private sealed class Subscription(List<IObserver<object?>> observers, IObserver<object?> observer) : IDisposable
        {
            public void Dispose() => observers.Remove(observer);
        }
    }
}