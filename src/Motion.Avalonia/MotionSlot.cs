using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
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

    /// <summary>
    /// Gets an obsolete alias for <see cref="TargetStyles"/>.
    /// </summary>
    /// <remarks>
    /// Motion styles apply to the target control subtree, not to the layout slot itself.
    /// </remarks>
    [Obsolete("Use TargetStyles to style the target control or its descendants.")]
    public new Styles Styles => TargetStyles;

    /// <summary>
    /// Gets the styles whose effective setter values are projected onto the target subtree.
    /// </summary>
    public Styles TargetStyles { get; } = new();

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

    private MotionStyleScope? _styleScope;
    private StyledElement? _styleTarget;

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
        TargetStyles.CollectionChanged += OnTargetStylesChanged;
    }

    internal IReadOnlyDictionary<StyledElement, IReadOnlyDictionary<AvaloniaProperty, object?>> CaptureEndpointValues(StyledElement target)
    {
        if (_styleScope is null || !ReferenceEquals(_styleTarget, target))
        {
            ReleaseEndpointScope();
            _styleTarget = target;
            _styleScope = new MotionStyleScope(
                target,
                TargetStyles,
                CreateRootDefinitions(),
                OnStyleEndpointChanged,
                OnTargetStructureChanged);
        }

        return _styleScope.CaptureValues();
    }

    internal int GetStateZIndex(StyledElement target)
    {
        var values = CaptureEndpointValues(target);
        if (values.TryGetValue(target, out var rootValues) &&
            rootValues.TryGetValue(ZIndexProperty, out var value) &&
            value is int zIndex)
        {
            return zIndex;
        }

        return ZIndex;
    }

    internal void ReleaseEndpointScope()
    {
        _styleScope?.Dispose();
        _styleScope = null;
        _styleTarget = null;
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

        if (ShortcutPropertySet.Contains(change.Property) || change.Property == TargetProperty)
        {
            ReleaseEndpointScope();
            EndpointChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSettersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ValidateSetters();
        ReleaseEndpointScope();
        EndpointChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTargetStylesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ReleaseEndpointScope();
        EndpointChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<MotionEndpointDefinition> CreateRootDefinitions()
    {
        var result = new List<MotionEndpointDefinition>(Setters.Count + ShortcutProperties.Length);
        var properties = new HashSet<AvaloniaProperty>();

        foreach (var setter in Setters)
        {
            var property = ValidateSetter(setter);
            properties.Add(property);
            result.Add(new MotionEndpointDefinition(property, setter.Value));
        }

        foreach (var property in ShortcutProperties)
        {
            if (!IsSet(property))
            {
                continue;
            }

            if (!properties.Add(property))
            {
                throw new InvalidOperationException(
                    $"{nameof(MotionSlot)} for target '{Target}' defines property '{property}' both directly and with a Setter.");
            }

            result.Add(new MotionEndpointDefinition(property, GetValue(property)));
        }

        return result;
    }

    private void ValidateSetters()
    {
        var properties = new HashSet<AvaloniaProperty>();

        foreach (var setter in Setters)
        {
            var property = ValidateSetter(setter);
            if (!properties.Add(property))
            {
                throw new InvalidOperationException($"{nameof(MotionSlot)} contains more than one Setter for property '{property}'.");
            }
        }
    }

    private static AvaloniaProperty ValidateSetter(Setter setter)
    {
        var property = setter.Property ??
            throw new InvalidOperationException($"{nameof(MotionSlot)} contains a Setter without a Property.");

        MotionStyleCompiler.ValidateProperty(property);
        return property;
    }

    private void OnStyleEndpointChanged() => EndpointChanged?.Invoke(this, EventArgs.Empty);

    private void OnTargetStructureChanged()
    {
        ReleaseEndpointScope();
        EndpointChanged?.Invoke(this, EventArgs.Empty);
    }
}
