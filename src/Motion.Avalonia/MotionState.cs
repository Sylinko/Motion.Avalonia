using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Describes one live layout state of a <see cref="MotionPanel"/>.
/// </summary>
public sealed class MotionState : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="Key"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> KeyProperty =
        AvaloniaProperty.Register<MotionState, object?>(nameof(Key));

    /// <summary>
    /// Defines the <see cref="Template"/> property.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> TemplateProperty =
        AvaloniaProperty.Register<MotionState, IDataTemplate?>(nameof(Template));

    /// <summary>
    /// Gets or sets the value matched against <see cref="MotionPanel.State"/>.
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