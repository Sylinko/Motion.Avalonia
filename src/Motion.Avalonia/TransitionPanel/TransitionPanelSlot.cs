using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Metadata;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Represents the live layout proxy for a direct child of a <see cref="TransitionPanel"/>.
/// </summary>
public sealed class TransitionPanelSlot : Control
{
    /// <summary>
    /// Defines the <see cref="Target"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TargetProperty =
        AvaloniaProperty.Register<TransitionPanelSlot, string?>(nameof(Target));

    /// <summary>
    /// Gets or sets the name of the represented direct child.
    /// </summary>
    public string? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Gets the target property values represented by this layout state.
    /// </summary>
    [Content]
    public AvaloniaList<Setter> Setters { get; } = new();

    internal int SetterVersion { get; private set; }

    private static readonly HashSet<AvaloniaProperty> EndpointProperties = [];
    private readonly Dictionary<AvaloniaProperty, IDisposable?> _setterBindings = [];

    static TransitionPanelSlot()
    {
        AffectsMeasure<TransitionPanelSlot>(TargetProperty);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionPanelSlot"/> class.
    /// </summary>
    public TransitionPanelSlot()
    {
        Setters.CollectionChanged += OnSettersChanged;
    }

    internal IEnumerable<AvaloniaProperty> GetEndpointProperties() => _setterBindings.Keys;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var scene = this.GetVisualAncestors().OfType<TransitionPanelHost>().FirstOrDefault();
        return scene?.MeasureTarget(Target, availableSize) ?? default;
    }

    private void OnSettersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildSetters();
        SetterVersion++;
    }

    private void RebuildSetters()
    {
        foreach (var pair in _setterBindings)
        {
            pair.Value?.Dispose();
            ClearValue(pair.Key);
        }

        _setterBindings.Clear();

        foreach (var setter in Setters)
        {
            var property = setter.Property ??
                throw new InvalidOperationException($"{nameof(TransitionPanelSlot)} contains a Setter without a Property.");

            if (property.IsDirect)
            {
                throw new InvalidOperationException($"{nameof(TransitionPanelSlot)} cannot animate direct property '{property}'.");
            }

            if (!EndpointProperties.Contains(property))
            {
                if (AvaloniaPropertyRegistry.Instance.IsRegistered(this, property))
                {
                    throw new InvalidOperationException(
                        $"Property '{property}' already belongs to {nameof(TransitionPanelSlot)}. " +
                        "Set it directly on the slot instead of using a Setter.");
                }

                AvaloniaPropertyRegistry.Instance.Register(typeof(TransitionPanelSlot), property);
                EndpointProperties.Add(property);
            }

            if (_setterBindings.ContainsKey(property))
            {
                throw new InvalidOperationException($"{nameof(TransitionPanelSlot)} contains more than one Setter for property '{property}'.");
            }

            if (setter.Value is BindingBase binding)
            {
                _setterBindings.Add(property, Bind(property, binding));
                continue;
            }

            if (!property.IsValidValue(setter.Value))
            {
                throw new InvalidOperationException($"Value '{setter.Value}' is not valid for property '{property}'.");
            }

            _setterBindings.Add(property, SetValue(property, setter.Value));
        }
    }
}