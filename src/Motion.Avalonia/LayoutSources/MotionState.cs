using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Describes one complete live layout state owned by <see cref="StateMotion"/>.
/// </summary>
/// <remarks>
/// The template is instantiated once while the owning motion belongs to a panel. Its bindings and
/// responsive layout remain active even while another state is presented.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// <motion:MotionState Key="Expanded">
///   <DataTemplate>
///     <Grid ColumnDefinitions="2*,*">
///       <motion:MotionSlot x:Name="Editor"/>
///       <motion:MotionSlot Grid.Column="1" x:Name="Preview"/>
///     </Grid>
///   </DataTemplate>
/// </motion:MotionState>
/// ]]></code>
/// </example>
public sealed class MotionState : MotionElement, IMotionLayoutSource
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

    static MotionState()
    {
        AffectsMotion<MotionState>(KeyProperty, TemplateProperty);
    }

    /// <summary>
    /// Gets or sets the key selected by the owning <see cref="StateMotion"/>.
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

    IDataTemplate? IMotionLayoutSource.Template => Template;

    string IMotionLayoutSource.Description => $"state '{Key}'";
}