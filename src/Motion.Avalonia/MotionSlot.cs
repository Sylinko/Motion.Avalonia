using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Represents the live layout and property proxy for a direct child of a <see cref="MotionPanel"/>.
/// </summary>
/// <remarks>
/// Layout properties such as <see cref="Layoutable.Margin"/> and alignment arrange the slot
/// itself. The following shortcut properties are copied into the target-property endpoint:
/// <see cref="Visual.Opacity"/>, <see cref="Visual.ClipToBounds"/>,
/// <see cref="Visual.Clip"/>, <see cref="Visual.OpacityMask"/>,
/// <see cref="Visual.Effect"/>, <see cref="Visual.RenderTransform"/>,
/// <see cref="Visual.RenderTransformOrigin"/>, <see cref="Visual.ZIndex"/>,
/// <see cref="InputElement.IsEnabled"/>, <see cref="InputElement.IsHitTestVisible"/>,
/// <see cref="InputElement.Cursor"/>, <see cref="InputElement.Focusable"/>,
/// <see cref="InputElement.IsTabStop"/>, and <see cref="InputElement.TabIndex"/>.
/// Use <see cref="Setters"/> for any other styled property.
/// </remarks>
/// <example>
/// A shortcut and a setter participate in the same pose evaluation:
/// <code><![CDATA[
/// <motion:MotionSlot
///   x:SetterTargetType="Border"
///   Target="Card"
///   Opacity="0.75"
///   MaxWidth="420">
///   <Setter Property="CornerRadius" Value="16"/>
/// </motion:MotionSlot>
/// ]]></code>
/// </example>
public sealed class MotionSlot : Control
{
    /// <summary>
    /// Defines the <see cref="Target"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TargetProperty =
        AvaloniaProperty.Register<MotionSlot, string?>(nameof(Target));

    /// <summary>
    /// Gets or sets the name of the represented direct child.
    /// </summary>
    public string? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Gets the additional target property values represented by this layout state.
    /// </summary>
    [Content]
    public AvaloniaList<Setter> Setters { get; } = new();

    internal event EventHandler? EndpointChanged;

    private static readonly AvaloniaProperty[] ShortcutProperties =
    [
        OpacityProperty,
        ClipToBoundsProperty,
        ClipProperty,
        OpacityMaskProperty,
        EffectProperty,
        RenderTransformProperty,
        RenderTransformOriginProperty,
        ZIndexProperty,
        IsEnabledProperty,
        IsHitTestVisibleProperty,
        CursorProperty,
        FocusableProperty,
        IsTabStopProperty,
        TabIndexProperty,
    ];

    private static readonly HashSet<AvaloniaProperty> ShortcutPropertySet = [.. ShortcutProperties];
    private static readonly HashSet<AvaloniaProperty> RegisteredSetterProperties = [];

    private readonly SetterTarget _setterTarget = new();
    private readonly Dictionary<AvaloniaProperty, IDisposable?> _setterBindings = [];

    static MotionSlot()
    {
        AffectsMeasure<MotionSlot>(TargetProperty);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionSlot"/> class.
    /// </summary>
    public MotionSlot()
    {
        Setters.CollectionChanged += OnSettersChanged;
        _setterTarget.PropertyChanged += OnSetterTargetPropertyChanged;
    }

    internal IReadOnlyDictionary<AvaloniaProperty, object?> CaptureEndpointValues()
    {
        var result = new Dictionary<AvaloniaProperty, object?>(_setterBindings.Count + ShortcutProperties.Length);

        foreach (var property in _setterBindings.Keys)
        {
            result.Add(property, _setterTarget.GetValue(property));
        }

        foreach (var property in ShortcutProperties)
        {
            if (!IsSet(property))
            {
                continue;
            }

            if (!result.TryAdd(property, GetValue(property)))
            {
                throw new InvalidOperationException(
                    $"{nameof(MotionSlot)} for target '{Target}' defines property '{property}' both directly and with a Setter.");
            }
        }

        return result;
    }

    internal int GetStateZIndex()
    {
        if (_setterBindings.ContainsKey(ZIndexProperty))
        {
            return _setterTarget.GetValue(ZIndexProperty);
        }

        return ZIndex;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var scene = this.GetVisualAncestors().OfType<MotionPanelHost>().FirstOrDefault();
        return scene?.MeasureTarget(Target, availableSize) ?? default;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (ShortcutPropertySet.Contains(change.Property))
        {
            EndpointChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSettersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildSetters();
        EndpointChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildSetters()
    {
        foreach (var pair in _setterBindings)
        {
            pair.Value?.Dispose();
            _setterTarget.ClearValue(pair.Key);
        }

        _setterBindings.Clear();

        foreach (var setter in Setters)
        {
            var property = setter.Property ?? throw new InvalidOperationException($"{nameof(MotionSlot)} contains a Setter without a Property.");

            if (MotionLayout.IsLayoutProperty(property))
            {
                throw new InvalidOperationException(
                    $"{nameof(MotionLayout)} property '{property.Name}' identifies an evaluated layout channel " +
                    $"and cannot be defined by a {nameof(Setter)}.");
            }

            if (property.IsDirect)
            {
                throw new InvalidOperationException($"{nameof(MotionSlot)} cannot animate direct property '{property}'.");
            }

            RegisterSetterProperty(property);

            if (_setterBindings.ContainsKey(property))
            {
                throw new InvalidOperationException($"{nameof(MotionSlot)} contains more than one Setter for property '{property}'.");
            }

            if (setter.Value is BindingBase binding)
            {
                _setterBindings.Add(property, _setterTarget.Bind(property, binding));
                continue;
            }

            if (!property.IsValidValue(setter.Value))
            {
                throw new InvalidOperationException($"Value '{setter.Value}' is not valid for property '{property}'.");
            }

            _setterBindings.Add(property, _setterTarget.SetValue(property, setter.Value));
        }
    }

    private static void RegisterSetterProperty(AvaloniaProperty property)
    {
        if (RegisteredSetterProperties.Add(property))
        {
            AvaloniaPropertyRegistry.Instance.Register(typeof(SetterTarget), property);
        }
    }

    private void OnSetterTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_setterBindings.ContainsKey(e.Property))
        {
            EndpointChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class SetterTarget : AvaloniaObject;
}