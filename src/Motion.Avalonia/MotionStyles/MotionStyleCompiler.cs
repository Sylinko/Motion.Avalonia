using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Motion.Avalonia;

/// <summary>
/// Clones native Avalonia styles while replacing original setters with endpoint shadow setters.
/// </summary>
/// <remarks>
/// Avalonia continues to own selector matching and cascade resolution. Motion only interprets
/// the winning <see cref="MotionEndpointDefinition"/>.
/// </remarks>
internal static class MotionStyleCompiler
{
    public static void AddCompiledStyles(Styles source, MotionStyleProxy destination)
    {
        CopyResources(source.Resources, destination.Resources);

        foreach (var style in source)
        {
            destination.Styles.Add(CloneStyle(style));
        }
    }

    public static void AddRootDefinitions(IReadOnlyList<MotionEndpointDefinition> definitions, MotionStyleProxy destination)
    {
        if (definitions.Count == 0)
        {
            return;
        }

        var style = new Style();
        foreach (var definition in definitions)
        {
            style.Setters.Add(CreateSetter(definition));
        }

        destination.Styles.Add(style);
    }

    public static void ValidateProperty(AvaloniaProperty property)
    {
        if (MotionLayout.IsLayoutProperty(property))
        {
            throw new InvalidOperationException(
                $"{nameof(MotionLayout)} property '{property.Name}' identifies an evaluated layout channel " +
                $"and cannot be defined by a {nameof(Setter)}.");
        }

        if (property == Animatable.TransitionsProperty)
        {
            throw new NotSupportedException($"{nameof(MotionSlot)} target styles cannot define Avalonia transitions.");
        }

        if (property.IsDirect)
        {
            throw new InvalidOperationException($"{nameof(MotionSlot)} cannot animate direct property '{property}'.");
        }
    }

    private static Style CloneStyle(IStyle source)
    {
        if (source is not Style style)
        {
            throw new NotSupportedException(
                $"{nameof(MotionSlot)} target styles currently support native {nameof(Style)} entries only, not '{source.GetType()}'.");
        }

        if (style.Animations.Count > 0)
        {
            throw new NotSupportedException($"{nameof(MotionSlot)} target styles cannot contain Avalonia animations.");
        }

        ValidateSelector(style.Selector);
        var result = new Style
        {
            Selector = style.Selector,
        };
        CopyResources(style.Resources, result.Resources);

        foreach (var setterBase in style.Setters)
        {
            if (setterBase is not Setter setter)
            {
                throw new NotSupportedException($"{nameof(MotionSlot)} target styles currently support native {nameof(Setter)} entries only.");
            }

            var property = setter.Property ??
                throw new InvalidOperationException($"{nameof(MotionSlot)} target style contains a Setter without a Property.");
            ValidateProperty(property);
            result.Setters.Add(CreateSetter(new MotionEndpointDefinition(property, setter.Value)));
        }

        foreach (var child in style.Children)
        {
            result.Children.Add(CloneStyle(child));
        }

        return result;
    }

    private static Setter CreateSetter(MotionEndpointDefinition definition) =>
        new(MotionShadowPropertyRegistry.GetOrCreate(definition.Property), definition);

    private static void ValidateSelector(Selector? selector)
    {
        if (selector is null)
        {
            return;
        }

        // Selector.ToString represents the complete selector graph, including :not() and OR
        // branches. Using its public grammar keeps this setup check safe for trimmed/AOT builds.
        var text = selector.ToString();
        if (text.Contains('[') || text.Contains("/template/", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"{nameof(MotionSlot)} target styles do not support selector '{text}'. " +
                "Property-value and template traversal selectors require target state that is not represented by the shadow tree.");
        }
    }

    private static void CopyResources(IResourceDictionary source, IResourceDictionary destination)
    {
        foreach (var pair in source)
        {
            destination.Add(pair.Key, pair.Value);
        }
    }
}