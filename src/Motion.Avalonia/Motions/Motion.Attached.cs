using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Styling;

namespace Motion.Avalonia;

public abstract partial class Motion
{
    /// <summary>
    /// Identifies the optional Motion identity attached to a styled target or layout slot.
    /// </summary>
    /// <remarks>
    /// When set, this value takes precedence over <see cref="StyledElement.Name"/> for Motion
    /// target lookup and interpolation matching. It is intended for dynamic item identities,
    /// such as an application model identifier, while preserving <c>x:Name</c> as the fallback.
    /// Changing it invalidates the owning logical Motion tree and causes a lazy realization rebuild.
    /// </remarks>
    public static readonly new AttachedProperty<string?> NameProperty =
        AvaloniaProperty.RegisterAttached<Motion, StyledElement, string?>("Name");

    /// <summary>
    /// Identifies the panel-wide desired-size channel evaluated during measure.
    /// </summary>
    public static readonly AttachedProperty<Size> DesiredSizeProperty =
        AvaloniaProperty.RegisterAttached<Motion, Control, Size>("DesiredSize");

    /// <summary>
    /// Identifies the per-target position channel evaluated during arrange.
    /// </summary>
    public static readonly AttachedProperty<Point> PositionProperty =
        AvaloniaProperty.RegisterAttached<Motion, Control, Point>("Position");

    /// <summary>
    /// Identifies the per-target size channel evaluated during measure and arrange.
    /// </summary>
    public static readonly AttachedProperty<Size> SizeProperty =
        AvaloniaProperty.RegisterAttached<Motion, Control, Size>("Size");

    /// <summary>
    /// Identifies the per-target presence channel used as a container-level visibility envelope.
    /// </summary>
    public static readonly AttachedProperty<double> PresenceProperty =
        AvaloniaProperty.RegisterAttached<Motion, Control, double>("Presence");

    static Motion()
    {
        NameProperty.Changed.AddClassHandler<StyledElement>(static (element, _) => MotionElement.InvalidateMotionTree(element));
    }

    private static readonly ConditionalWeakTable<Setter, SetterMetadata> SetterMetadataTable = new();
    private static readonly ConditionalWeakTable<MotionSlot, SlotMetadata> SlotMetadataTable = new();

    /// <summary>
    /// Gets the optional Motion identity attached to a styled element.
    /// </summary>
    public static string? GetName(StyledElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(NameProperty);
    }

    /// <summary>
    /// Sets the optional Motion identity used for target lookup and interpolation matching.
    /// </summary>
    public static void SetName(StyledElement element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(NameProperty, value);
    }

    /// <summary>
    /// Gets the layout position published to a target control.
    /// </summary>
    public static Point GetPosition(Control target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.GetValue(PositionProperty);
    }

    /// <summary>
    /// Sets the base layout position used when no motion track overrides the target.
    /// </summary>
    public static void SetPosition(Control target, Point value)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.SetValue(PositionProperty, value);
    }

    /// <summary>
    /// Gets the layout size published to a target control.
    /// </summary>
    public static Size GetSize(Control target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.GetValue(SizeProperty);
    }

    /// <summary>
    /// Sets the base layout size used when no motion track overrides the target.
    /// </summary>
    public static void SetSize(Control target, Size value)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Gets the target presence envelope, where one means fully present.
    /// </summary>
    public static double GetPresence(Control target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.GetValue(PresenceProperty);
    }

    /// <summary>
    /// Sets the base target presence envelope used when no motion track overrides the target.
    /// </summary>
    public static void SetPresence(Control target, double value)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Presence must be finite.");
        }

        target.SetValue(PresenceProperty, value);
    }

    /// <summary>
    /// Gets the effective Motion identity, preferring <see cref="NameProperty"/> over
    /// <see cref="StyledElement.Name"/>.
    /// </summary>
    internal static string? GetEffectiveName(StyledElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var motionName = GetName(element);
        return string.IsNullOrWhiteSpace(motionName) ? element.Name : motionName;
    }

    /// <summary>
    /// Gets the easing attached to a setter.
    /// </summary>
    public static Easing? GetEasing(Setter setter) =>
        SetterMetadataTable.TryGetValue(setter, out var metadata) ? metadata.Easing.GetValueOrDefault() : null;

    /// <summary>
    /// Sets the easing used by the segment arriving at a setter value.
    /// </summary>
    public static void SetEasing(Setter setter, Easing? value)
    {
        ArgumentNullException.ThrowIfNull(setter);
        var metadata = SetterMetadataTable.GetOrCreateValue(setter);
        metadata.Easing = new Optional<Easing?>(value);
    }

    /// <summary>
    /// Gets the value-space interpolator attached to a setter.
    /// </summary>
    public static MotionInterpolator? GetInterpolator(Setter setter) =>
        SetterMetadataTable.TryGetValue(setter, out var metadata) ? metadata.Interpolator.GetValueOrDefault() : null;

    /// <summary>
    /// Sets the value-space interpolator used by the segment arriving at a setter value.
    /// </summary>
    public static void SetInterpolator(Setter setter, MotionInterpolator? value)
    {
        ArgumentNullException.ThrowIfNull(setter);
        var metadata = SetterMetadataTable.GetOrCreateValue(setter);
        metadata.Interpolator = new Optional<MotionInterpolator?>(value);
    }

    /// <summary>
    /// Gets property-specific interpolation metadata for a slot.
    /// </summary>
    public static AvaloniaList<MotionInterpolation> GetInterpolations(MotionSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return SlotMetadataTable.GetValue(slot, static owner => new SlotMetadata(owner)).Interpolations;
    }

    /// <summary>
    /// Gets the explicit presence authored on a slot, or one when no value is set.
    /// </summary>
    public static double GetPresence(MotionSlot slot) =>
        SlotMetadataTable.TryGetValue(slot, out var metadata) && metadata.HasPresence ? metadata.Presence : 1;

    /// <summary>
    /// Sets the slot's container-level presence endpoint.
    /// </summary>
    public static void SetPresence(MotionSlot slot, double value)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Presence must be finite.");
        }

        var metadata = SlotMetadataTable.GetValue(slot, static owner => new SlotMetadata(owner));
        metadata.HasPresence = true;
        metadata.Presence = value;
        slot.NotifyMotionMetadataChanged();
    }

    internal static bool IsLayoutProperty(AvaloniaProperty property) =>
        property == DesiredSizeProperty ||
        property == PositionProperty ||
        property == SizeProperty ||
        property == PresenceProperty;

    internal static bool IsPanelProperty(AvaloniaProperty property) =>
        property == DesiredSizeProperty;

    internal static MotionInterpolationMetadata GetSetterMetadata(Setter setter)
    {
        if (!SetterMetadataTable.TryGetValue(setter, out var metadata))
        {
            return default;
        }

        return new MotionInterpolationMetadata(metadata.Easing, metadata.Interpolator);
    }

    internal static MotionInterpolationMetadata GetSlotMetadata(MotionSlot slot, AvaloniaProperty property)
    {
        if (!SlotMetadataTable.TryGetValue(slot, out var metadata))
        {
            return default;
        }

        MotionInterpolation? match = null;
        foreach (var interpolation in metadata.Interpolations)
        {
            if (interpolation.Property != property)
            {
                continue;
            }

            if (match is not null)
            {
                throw new InvalidOperationException(
                    $"{nameof(MotionSlot)} '{slot.Name}' contains more than one interpolation for property '{property}'.");
            }

            match = interpolation;
        }

        return match?.GetEndpointMetadata() ?? default;
    }

    internal static double GetAuthoredPresence(MotionSlot slot) => GetPresence(slot);

    private sealed class SetterMetadata
    {
        public Optional<Easing?> Easing { get; set; }

        public Optional<MotionInterpolator?> Interpolator { get; set; }
    }

    private sealed class SlotMetadata
    {
        public AvaloniaList<MotionInterpolation> Interpolations { get; } = [];

        public bool HasPresence { get; set; }

        public double Presence { get; set; } = 1;

        private readonly MotionSlot _owner;
        private readonly HashSet<MotionInterpolation> _attachedInterpolations = [];

        public SlotMetadata(MotionSlot owner)
        {
            _owner = owner;
            Interpolations.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var interpolation in _attachedInterpolations.Where(item => !Interpolations.Contains(item)).ToArray())
            {
                interpolation.PropertyChanged -= OnInterpolationChanged;
                _attachedInterpolations.Remove(interpolation);
            }

            foreach (var interpolation in Interpolations)
            {
                if (_attachedInterpolations.Add(interpolation))
                {
                    interpolation.PropertyChanged += OnInterpolationChanged;
                }
            }

            _owner.NotifyMotionMetadataChanged();
        }

        private void OnInterpolationChanged(object? sender, AvaloniaPropertyChangedEventArgs e) =>
            _owner.NotifyMotionMetadataChanged();
    }
}