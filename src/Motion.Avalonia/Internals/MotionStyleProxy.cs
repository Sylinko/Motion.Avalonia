using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Motion.Avalonia;

/// <summary>
/// Mirrors the selector-relevant identity of one real element in an isolated motion style tree.
/// </summary>
/// <remarks>
/// The proxy carries the real style key, name, classes, pseudo-classes, and logical hierarchy,
/// but only receives motion shadow properties. Original properties are never registered on it.
/// </remarks>
internal sealed class MotionStyleProxy : StyledElement, IDisposable
{
    public StyledElement Target { get; }

    protected override Type StyleKeyOverride { get; }

    private bool _disposed;

    public MotionStyleProxy(StyledElement target)
    {
        Target = target;
        StyleKeyOverride = target.StyleKey;
        Name = target.Name;
        SynchronizeClasses();
        target.Classes.CollectionChanged += HandleTargetClassesChanged;
    }

    public static void ValidateProperty(AvaloniaProperty property)
    {
        if (Motion.IsLayoutProperty(property))
        {
            throw new InvalidOperationException(
                $"{nameof(Motion)} property '{property.Name}' identifies an evaluated layout channel and cannot be defined by a {nameof(Setter)}.");
        }

        if (property == TransitionsProperty)
        {
            throw new NotSupportedException($"{nameof(MotionSlot)} target styles cannot define Avalonia transitions.");
        }

        if (property.IsDirect)
        {
            throw new InvalidOperationException($"{nameof(MotionSlot)} cannot animate direct property '{property}'.");
        }
    }

    public void CopyStyles(Styles source)
    {
        if (((IResourceProvider)source).HasResources)
        {
            StylesHelper.CopyResources(source.Resources, Resources);
        }

        foreach (var style in source)
        {
            Styles.Add(StylesHelper.CloneStyle(style));
        }
    }

    public void AddRootDefinitions(IReadOnlyList<MotionEndpointDefinition> rootDefinitions)
    {
        if (rootDefinitions.Count == 0)
        {
            return;
        }

        var style = new Style();
        foreach (var definition in rootDefinitions)
        {
            style.Setters.Add(StylesHelper.CreateSetter(definition));
        }

        Styles.Add(style);
    }

    public void AddChild(MotionStyleProxy child) => LogicalChildren.Add(child);

    public void ApplyStylesRecursively()
    {
        ApplyStyling();

        foreach (var child in ((ILogical)this).LogicalChildren.OfType<MotionStyleProxy>())
        {
            child.ApplyStylesRecursively();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Target.Classes.CollectionChanged -= HandleTargetClassesChanged;

        foreach (var child in ((ILogical)this).LogicalChildren.OfType<MotionStyleProxy>().ToArray())
        {
            child.Dispose();
        }

        LogicalChildren.Clear();
    }

    private void HandleTargetClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeClasses();

    private void SynchronizeClasses()
    {
        foreach (var item in Classes.ToArray())
        {
            if (item.StartsWith(':'))
            {
                PseudoClasses.Remove(item);
            }
            else
            {
                Classes.Remove(item);
            }
        }

        foreach (var item in Target.Classes)
        {
            if (item.StartsWith(':'))
            {
                PseudoClasses.Add(item);
            }
            else
            {
                Classes.Add(item);
            }
        }
    }

    private static class StylesHelper
    {
        public static void CopyResources(IResourceDictionary source, IResourceDictionary destination)
        {
            foreach (var pair in source)
            {
                destination.Add(pair.Key, pair.Value);
            }
        }

        public static Style CloneStyle(IStyle source)
        {
            if (source is not Style style)
            {
                throw new NotSupportedException(
                    $"{nameof(MotionSlot)} target styles currently support native {nameof(Style)} entries only, not '{source.GetType()}'.");
            }

            // get style.Setters or style.Animations will create a List.
            if (GetStyleBaseAnimationsNoAlloc(style) is { Count: > 0 })
            {
                throw new NotSupportedException($"{nameof(MotionSlot)} target styles cannot contain Avalonia animations.");
            }

            ValidateSelector(style.Selector);
            var result = new Style
            {
                Selector = style.Selector,
            };

            if (((IResourceNode)style).HasResources)
            {
                CopyResources(style.Resources, result.Resources);
            }

            if (style.HasSettersOrAnimations)
            {
                // Must be setters. Animations throws.

                if (result.Setters is List<SetterBase> settersList)
                {
                    settersList.Capacity = style.Setters.Count;
                }

                foreach (var setterBase in style.Setters)
                {
                    if (setterBase is not Setter setter)
                    {
                        throw new NotSupportedException(
                            $"{nameof(MotionSlot)} target styles currently support native {nameof(Setter)} entries only.");
                    }

                    var property = setter.Property ?? throw new InvalidOperationException($"{nameof(MotionSlot)} contains a non-property Setter.");
                    ValidateProperty(property);
                    result.Setters.Add(CreateSetter(new MotionEndpointDefinition(property, setter.Value, Motion.GetSetterMetadata(setter))));
                }
            }

            if (style.HasChildren)
            {
                foreach (var child in style.Children)
                {
                    result.Children.Add(CloneStyle(child));
                }
            }

            return result;
        }

        public static Setter CreateSetter(MotionEndpointDefinition definition) =>
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

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_animations")]
        private static extern ref List<IAnimation>? GetStyleBaseAnimationsNoAlloc(StyleBase styleBase);
    }
}