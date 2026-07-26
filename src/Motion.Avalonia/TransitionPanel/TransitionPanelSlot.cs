using Avalonia;
using Avalonia.Controls;
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

    static TransitionPanelSlot()
    {
        AffectsMeasure<TransitionPanelSlot>(TargetProperty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var scene = this.GetVisualAncestors().OfType<TransitionPanelHost>().FirstOrDefault();
        return scene?.MeasureTarget(Target, availableSize) ?? default;
    }
}