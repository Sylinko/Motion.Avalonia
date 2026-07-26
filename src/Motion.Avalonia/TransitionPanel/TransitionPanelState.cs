using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Describes one live layout state of a <see cref="TransitionPanel"/>.
/// </summary>
public sealed class TransitionPanelState : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="Key"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> KeyProperty =
        AvaloniaProperty.Register<TransitionPanelState, object?>(nameof(Key));

    /// <summary>
    /// Defines the <see cref="Template"/> property.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> TemplateProperty =
        AvaloniaProperty.Register<TransitionPanelState, IDataTemplate?>(nameof(Template));

    /// <summary>
    /// Gets or sets the value matched against <see cref="TransitionPanel.State"/>.
    /// </summary>
    public object? Key
    {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>
    /// Gets or sets the persistent layout template for this state.
    /// </summary>
    [Content]
    public IDataTemplate? Template
    {
        get => GetValue(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }
}