using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Motion.Avalonia;

/// <summary>
/// Owns motion items and presents them through a <see cref="MotionItemsPanel"/>.
/// </summary>
/// <remarks>
/// Direct <see cref="Control"/> items retain Avalonia's normal own-container semantics: the item
/// remains a logical child of this control and remains in the items panel's <c>Children</c>
/// collection. The panel may add a visual-only <see cref="MotionItemContainer"/> envelope around
/// it so presence and paint order can be composed without changing logical ownership.
///
/// Non-control data items receive a <see cref="MotionItemContainer"/> from the normal
/// <see cref="ItemsControl"/> generator and are materialized through its data template.
/// </remarks>
public class MotionPanel : ItemsControl
{
    /// <summary>
    /// Defines the <see cref="Motion"/> property.
    /// </summary>
    public static readonly StyledProperty<Motion?> MotionProperty = AvaloniaProperty.Register<MotionPanel, Motion?>(nameof(Motion));

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    static MotionPanel()
    {
        ItemsPanelProperty.OverrideDefaultValue<MotionPanel>(new FuncTemplate<Panel?>(static () => new MotionItemsPanel()));
        MotionProperty.Changed.AddClassHandler<MotionPanel>(static (panel, _) => panel.OnMotionChanged());
        Motion.DesiredSizeProperty.Changed.AddClassHandler<MotionPanel>(static (panel, _) =>
        {
            panel.Presenter?.InvalidateMeasure();
            panel.InvalidateMeasure();
        });
    }

    /// <summary>
    /// Gets or sets the declarative motion source.
    /// </summary>
    public Motion? Motion
    {
        get => GetValue(MotionProperty);
        set => SetValue(MotionProperty, value);
    }

    internal MotionItemsPanel? ItemsHost => ItemsPanelRoot as MotionItemsPanel;

    private Motion? _attachedMotion;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionPanel"/> class.
    /// </summary>
    public MotionPanel() { }

    /// <summary>
    /// Invalidates the active motion owned by this panel.
    /// </summary>
    internal void InvalidateMotion(Motion motion)
    {
        if (!ReferenceEquals(Motion, motion))
        {
            return;
        }

        ItemsHost?.InvalidateMotion(motion);
        Presenter?.InvalidateMeasure();
        InvalidateMeasure();
    }

    /// <summary>
    /// Forwards a target identity change to the realized items panel.
    /// </summary>
    internal void InvalidateMotionIdentity(StyledElement source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ItemsHost?.InvalidateMotionIdentity(source);
        Presenter?.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void OnMotionChanged()
    {
        if (_attachedMotion is not null)
        {
            LogicalChildren.Remove(_attachedMotion);
            _attachedMotion = null;
        }

        if (Motion is { } motion)
        {
            if (motion.Parent is not null)
            {
                throw new InvalidOperationException($"{motion.GetType().Name} already belongs to another logical parent.");
            }

            LogicalChildren.Add(motion);
            _attachedMotion = motion;
        }

        ItemsHost?.InvalidateMotion();
        Presenter?.InvalidateMeasure();
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new MotionItemContainer();
}