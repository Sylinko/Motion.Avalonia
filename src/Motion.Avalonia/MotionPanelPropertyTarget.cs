using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Styling;
using Motion.Avalonia.Extensions;

namespace Motion.Avalonia;

/// <summary>
/// Applies live state property values to one transition target without replacing its base values.
/// </summary>
internal sealed class MotionPanelPropertyTarget(Control target) : IDisposable
{
    private readonly PropertyProxy _proxy = new();
    private readonly Dictionary<AvaloniaProperty, IDisposable> _endpointBindings = new();
    private readonly Dictionary<AvaloniaProperty, IDisposable> _outputBindings = new();
    private MotionSlot? _slot;
    private CancellationTokenSource? _animationCancellation;
    private int _slotVersion = -1;
    private int _animationGeneration;

    public void Update(MotionSlot? slot, TimeSpan duration, Easing? easing)
    {
        if (ReferenceEquals(_slot, slot) && _slotVersion == slot?.SetterVersion)
        {
            return;
        }

        var nextProperties = slot?.GetEndpointProperties().ToHashSet() ?? [];
        ValidateProperties(nextProperties);

        var properties = _outputBindings.Keys.Concat(nextProperties).Distinct().ToArray();
        var currentValues = properties.ToDictionary(property => property, target.GetValue);

        CancelAnimation();
        ClearEndpointBindings();
        var targetValues = PrepareOutputs(slot, nextProperties, properties, currentValues);
        var removedProperties = properties.Where(property => !nextProperties.Contains(property)).ToArray();
        _slot = slot;
        _slotVersion = slot?.SetterVersion ?? -1;

        if (properties.Length == 0)
        {
            return;
        }

        if (easing is null || duration <= TimeSpan.Zero)
        {
            ApplyEndpointBindings(slot, nextProperties);
            RemoveOutputs(removedProperties);
            return;
        }

        var from = new KeyFrame { Cue = new Cue(0) };
        var to = new KeyFrame { Cue = new Cue(1) };

        foreach (var property in properties)
        {
            from.Setters.Add(new Setter(property, currentValues[property]));
            to.Setters.Add(new Setter(property, targetValues[property]));
        }

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Both,
            PlaybackBehavior = PlaybackBehavior.Always,
            Children = { from, to },
        };
        var cancellation = new CancellationTokenSource();
        _animationCancellation = cancellation;
        var generation = ++_animationGeneration;
        CompleteAnimationAsync(
            animation.RunAsync(_proxy, cancellation.Token),
            cancellation,
            generation,
            slot,
            nextProperties,
            removedProperties).Detach(IExceptionHandler.DangerouslyIgnoreAllException);
    }

    public void CompleteImmediately()
    {
        CancelAnimation();
        var properties = _slot?.GetEndpointProperties().ToHashSet() ?? [];
        ApplyEndpointBindings(_slot, properties);
        RemoveOutputs(_outputBindings.Keys.Where(property => !properties.Contains(property)).ToArray());
    }

    public void Dispose()
    {
        CancelAnimation();

        foreach (var binding in _endpointBindings.Values)
        {
            binding.Dispose();
        }

        foreach (var binding in _outputBindings.Values)
        {
            binding.Dispose();
        }

        _endpointBindings.Clear();
        _outputBindings.Clear();
    }

    private Dictionary<AvaloniaProperty, object?> PrepareOutputs(
        MotionSlot? slot,
        IReadOnlySet<AvaloniaProperty> nextProperties,
        IReadOnlyCollection<AvaloniaProperty> properties,
        IReadOnlyDictionary<AvaloniaProperty, object?> currentValues)
    {
        var targetValues = new Dictionary<AvaloniaProperty, object?>(properties.Count);

        foreach (var property in properties)
        {
            AvaloniaPropertyRegistry.Instance.Register(typeof(PropertyProxy), property);
            _proxy.ClearValue(property);
            _proxy.SetValue(property, currentValues[property]);

            if (nextProperties.Contains(property))
            {
                targetValues.Add(property, slot!.GetValue(property));
            }
            else
            {
                _outputBindings[property].Dispose();
                _outputBindings.Remove(property);
                targetValues.Add(property, target.GetValue(property));
            }

            if (!_outputBindings.ContainsKey(property))
            {
                BindOutput(property);
            }
        }

        return targetValues;
    }

    private void ApplyEndpointBindings(
        MotionSlot? slot,
        IEnumerable<AvaloniaProperty> properties)
    {
        ClearEndpointBindings();

        if (slot is null)
        {
            return;
        }

        foreach (var property in properties)
        {
            _proxy.ClearValue(property);
            _endpointBindings.Add(property, _proxy.Bind(property, slot.GetObservable(property)));
        }
    }

    private void ClearEndpointBindings()
    {
        foreach (var binding in _endpointBindings.Values)
        {
            binding.Dispose();
        }

        _endpointBindings.Clear();
    }

    private void BindOutput(AvaloniaProperty property)
    {
        _outputBindings.Add(property, target.Bind(property, _proxy.GetObservable(property), BindingPriority.Animation));
    }

    private void ValidateProperties(IEnumerable<AvaloniaProperty> properties)
    {
        foreach (var property in properties)
        {
            if (property.IsDirect)
            {
                throw new InvalidOperationException($"{nameof(MotionSlot)} cannot animate direct property '{property}'.");
            }

            if (!AvaloniaPropertyRegistry.Instance.IsRegistered(target, property))
            {
                throw new InvalidOperationException($"Property '{property}' is not registered on transition target '{target.Name}'.");
            }
        }
    }

    private async Task CompleteAnimationAsync(
        Task animation,
        CancellationTokenSource cancellation,
        int generation,
        MotionSlot? slot,
        IReadOnlySet<AvaloniaProperty> endpointProperties,
        IReadOnlyCollection<AvaloniaProperty> removedProperties)
    {
        try
        {
            await animation;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Interruption is expected when a new state starts from the current visual value.
        }
        finally
        {
            cancellation.Dispose();

            if (generation == _animationGeneration)
            {
                _animationCancellation = null;
                ApplyEndpointBindings(slot, endpointProperties);
                RemoveOutputs(removedProperties);
            }
        }
    }

    private void RemoveOutputs(IEnumerable<AvaloniaProperty> properties)
    {
        foreach (var property in properties)
        {
            if (_outputBindings.Remove(property, out var output))
            {
                output.Dispose();
            }

            _proxy.ClearValue(property);
        }
    }

    private void CancelAnimation()
    {
        _animationGeneration++;
        _animationCancellation?.Cancel();
        _animationCancellation?.Dispose();
        _animationCancellation = null;
    }

    private sealed class PropertyProxy : Animatable;
}