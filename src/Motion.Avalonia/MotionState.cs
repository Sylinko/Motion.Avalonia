using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Describes one live layout state of a <see cref="MotionPanel"/>.
/// </summary>
/// <remarks>
/// The template is instantiated once while the state belongs to a panel. Its bindings and
/// responsive layout remain active even while another state is presented.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// <motion:MotionState Key="Expanded">
///   <DataTemplate>
///     <Grid ColumnDefinitions="2*,*">
///       <motion:MotionSlot Target="Editor"/>
///       <motion:MotionSlot Grid.Column="1" Target="Preview"/>
///     </Grid>
///   </DataTemplate>
/// </motion:MotionState>
/// ]]></code>
/// </example>
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
    /// Gets or sets the key referenced by controller-produced poses.
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