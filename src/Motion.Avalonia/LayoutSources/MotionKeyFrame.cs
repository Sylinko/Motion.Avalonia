using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Motion.Avalonia;

/// <summary>
/// Defines the live sparse content and incoming timing metadata shared by motion keyframes.
/// </summary>
/// <remarks>
/// Easing belongs to the segment arriving at this keyframe. Each property is compiled into its
/// own track, so a property omitted from this template simply has no key at this coordinate.
/// </remarks>
public abstract class MotionKeyFrame : MotionElement, IMotionLayoutSource
{
    /// <summary>
    /// Defines the <see cref="Template"/> property.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> TemplateProperty =
        AvaloniaProperty.Register<MotionKeyFrame, IDataTemplate?>(nameof(Template));

    /// <summary>
    /// Defines the <see cref="Easing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> EasingProperty =
        AvaloniaProperty.Register<MotionKeyFrame, Easing?>(nameof(Easing));

    static MotionKeyFrame()
    {
        AffectsMotion<MotionKeyFrame>(TemplateProperty, EasingProperty);
    }

    /// <summary>
    /// Gets or sets the persistent sparse layout template.
    /// </summary>
    [Content]
    public IDataTemplate? Template
    {
        get => GetValue(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the default easing for property segments arriving at this keyframe.
    /// A null value falls through to the owning motion and ultimately resolves to linear.
    /// </summary>
    public Easing? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }

    IDataTemplate? IMotionLayoutSource.Template => Template;

    string IMotionLayoutSource.Description => ToString() ?? GetType().Name;
}