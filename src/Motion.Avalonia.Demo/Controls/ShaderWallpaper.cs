using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Motion.Avalonia.Demo.Controls;

/// <summary>
/// Wallpaper compositions rendered by <see cref="ShaderWallpaper"/>.
/// </summary>
public enum ShaderWallpaperPattern
{
    /// <summary>
    /// A few flowing chromatic ribbons with crisp edges over a near-black canvas.
    /// </summary>
    ChromaticRibbons,

    /// <summary>
    /// A fine dot grid lit by two soft aurora glows, with thin orbit lines.
    /// </summary>
    DotGridAurora,

    /// <summary>
    /// Flat Swiss-style geometric shapes that drift slowly.
    /// </summary>
    FlatGeometric,
}

/// <summary>
/// Renders a slowly animated, flat-shaded Skia wallpaper. The wallpaper deliberately contains crisp edges and
/// fine detail so that frosted-glass surfaces drawn with <see cref="Visual.BackdropEffect"/> visibly diffuse it.
/// </summary>
/// <remarks>
/// Rendering is driven by <see cref="TopLevel.RequestAnimationFrame"/> only while the control is attached and
/// effectively visible, so hidden pages do not keep the compositor busy.
/// </remarks>
public sealed class ShaderWallpaper : Control
{
    /// <summary>
    /// Defines the <see cref="Pattern"/> property.
    /// </summary>
    public static readonly StyledProperty<ShaderWallpaperPattern> PatternProperty =
        AvaloniaProperty.Register<ShaderWallpaper, ShaderWallpaperPattern>(nameof(Pattern));

    /// <summary>
    /// Defines the <see cref="Speed"/> property.
    /// </summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<ShaderWallpaper, double>(nameof(Speed), 1.0);

    /// <summary>
    /// Gets or sets the wallpaper composition.
    /// </summary>
    public ShaderWallpaperPattern Pattern
    {
        get => GetValue(PatternProperty);
        set => SetValue(PatternProperty, value);
    }

    /// <summary>
    /// Gets or sets the animation speed multiplier. Zero freezes the wallpaper.
    /// </summary>
    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    private const string SkslSource = """
        uniform float2 size;
        uniform float uTime;
        uniform float pattern;

        float hash(float2 p) {
            p = fract(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return fract(p.x * p.y);
        }

        // Anti-aliased fill for a signed distance in pixels (negative inside).
        float aa(float d) { return 1.0 - smoothstep(-0.8, 0.8, d); }

        float3 ribbons(float2 frag, float2 res, float t) {
            float2 uv = frag / res;
            float3 col = mix(float3(0.020, 0.022, 0.040), float3(0.050, 0.040, 0.090), uv.y);
            for (int i = 0; i < 4; i++) {
                float fi = float(i);
                float phase = fi * 1.7;
                float x = uv.x * 6.2831;
                float center = res.y * (0.20 + fi * 0.19
                    + 0.10 * sin(x * (0.55 + fi * 0.12) + t * (0.25 + fi * 0.05) + phase)
                    + 0.04 * sin(x * 1.3 - t * 0.2 + phase * 2.0));
                float width = res.y * (0.020 + 0.050 * (0.5 + 0.5 * sin(x * 0.7 + t * 0.3 + phase)));
                float mask = aa(abs(frag.y - center) - width);
                float across = clamp((frag.y - center) / max(width, 1.0), -1.0, 1.0);
                float twist = 0.72 + 0.28 * cos(across * 1.57 + x * 0.9 + t * 0.4 + phase);
                float3 a = (i == 0) ? float3(0.98, 0.42, 0.55) : (i == 1) ? float3(0.55, 0.40, 1.00) : (i == 2) ? float3(0.22, 0.78, 0.98) : float3(0.98, 0.72, 0.36);
                float3 b = (i == 0) ? float3(0.62, 0.30, 0.98) : (i == 1) ? float3(0.22, 0.60, 1.00) : (i == 2) ? float3(0.30, 0.95, 0.75) : float3(0.98, 0.42, 0.55);
                float3 ribbon = mix(a, b, uv.x) * twist;
                // Screen blend keeps overlaps luminous instead of muddy.
                col = mix(col, 1.0 - (1.0 - col) * (1.0 - ribbon), mask * 0.88);
            }
            return col;
        }

        float3 dotGrid(float2 frag, float2 res, float t) {
            float2 uv = frag / res;
            float2 aspect = float2(res.x / res.y, 1.0);
            float3 col = float3(0.025, 0.027, 0.040);
            float2 c1 = float2(0.28 + 0.06 * sin(t * 0.20), 0.35 + 0.05 * cos(t * 0.17));
            float2 c2 = float2(0.74 + 0.05 * cos(t * 0.15), 0.62 + 0.06 * sin(t * 0.21));
            float g1 = exp(-6.0 * dot((uv - c1) * aspect, (uv - c1) * aspect));
            float g2 = exp(-5.0 * dot((uv - c2) * aspect, (uv - c2) * aspect));
            col += (float3(0.35, 0.30, 1.00) * g1 + float3(0.10, 0.85, 0.80) * g2) * 0.42;

            float spacing = 20.0;
            float2 cell = (fract(frag / spacing) - 0.5) * spacing;
            float lit = 0.10 + 1.6 * (g1 + g2);
            col += float3(0.85, 0.90, 1.00) * aa(length(cell) - 1.25) * lit * 0.55;

            float r = length(frag - res * float2(0.5, 0.55));
            float sweep = 0.5 + 0.5 * sin(t * 0.3);
            col += float3(1.0) * aa(abs(r - res.y * (0.40 + 0.02 * sweep)) - 0.6) * 0.18;
            col += float3(1.0) * aa(abs(r - res.y * 0.70) - 0.6) * 0.10;
            return col;
        }

        float3 geometric(float2 frag, float2 res, float t) {
            float s = res.y;
            float3 col = float3(0.045, 0.045, 0.060);
            float3 indigo = float3(0.31, 0.30, 0.86);
            float3 coral = float3(0.96, 0.43, 0.38);
            float3 amber = float3(0.98, 0.75, 0.32);
            float3 teal = float3(0.13, 0.72, 0.66);
            float3 lilac = float3(0.70, 0.56, 0.98);

            float2 c1 = float2(res.x * (0.16 + 0.02 * sin(t * 0.30)), s * 0.98);
            col = mix(col, indigo, aa(length(frag - c1) - s * 0.62));
            float2 c2 = float2(res.x * (0.80 + 0.015 * sin(t * 0.22)), s * (0.06 + 0.03 * cos(t * 0.25)));
            col = mix(col, coral, aa(length(frag - c2) - s * 0.42));
            float2 c3 = float2(res.x * (0.52 + 0.03 * sin(t * 0.20)), s * (0.64 + 0.02 * cos(t * 0.27)));
            col = mix(col, amber, aa(length(frag - c3) - s * 0.16));
            float band = abs((frag.x - frag.y * 0.55) - res.x * (0.64 + 0.02 * cos(t * 0.22))) - s * 0.05;
            col = mix(col, teal, aa(band) * 0.95);
            float2 c4 = float2(res.x * 0.93, s * (0.88 + 0.02 * sin(t * 0.18)));
            col = mix(col, lilac, aa(abs(length(frag - c4) - s * 0.24) - s * 0.035));
            for (int i = 0; i < 6; i++) {
                float y = s * (0.10 + float(i) * 0.035);
                float line = aa(abs(frag.y - y) - 1.0) * step(frag.x, res.x * 0.28) * step(res.x * 0.05, frag.x);
                col = mix(col, float3(0.92), line * 0.6);
            }
            return col;
        }

        half4 main(float2 frag) {
            float3 col;
            if (pattern < 0.5) col = ribbons(frag, size, uTime);
            else if (pattern < 1.5) col = dotGrid(frag, size, uTime);
            else col = geometric(frag, size, uTime);
            // Static film grain; it also prevents 8-bit banding in the dark gradients.
            col += (hash(frag) - 0.5) * 0.018;
            return half4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    private static readonly Lazy<SKRuntimeEffect?> CompiledShader = new(() =>
        SKRuntimeEffect.CreateShader(SkslSource, out _));

    private TopLevel? _topLevel;
    private TimeSpan? _startTimestamp;
    private float _elapsedSeconds;
    private bool _isFrameRequested;

    static ShaderWallpaper()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ShaderWallpaper>(false);
        ClipToBoundsProperty.OverrideDefaultValue<ShaderWallpaper>(true);
        AffectsRender<ShaderWallpaper>(PatternProperty);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (CompiledShader.Value is { } effect)
        {
            context.Custom(new WallpaperDrawOperation(bounds, effect, Pattern, _elapsedSeconds));
            return;
        }

        // Fallback when runtime shaders are unavailable on the current backend.
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(10, 11, 18)), bounds);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _startTimestamp = null;
        RequestNextFrame();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel = null;
        _isFrameRequested = false;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Resume the frame loop when a hidden page becomes visible again.
        if (change.Property == IsVisibleProperty && IsEffectivelyVisible)
        {
            RequestNextFrame();
        }
    }

    private void RequestNextFrame()
    {
        if (_isFrameRequested || _topLevel is null)
        {
            return;
        }

        _isFrameRequested = true;
        _topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        _isFrameRequested = false;
        if (_topLevel is null || !IsEffectivelyVisible)
        {
            return;
        }

        _startTimestamp ??= timestamp;
        _elapsedSeconds = (float)((timestamp - _startTimestamp.Value).TotalSeconds * Speed);
        InvalidateVisual();
        RequestNextFrame();
    }

    private sealed class WallpaperDrawOperation(Rect bounds, SKRuntimeEffect effect, ShaderWallpaperPattern pattern, float time)
        : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } leaseFeature)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var width = (float)bounds.Width;
            var height = (float)bounds.Height;
            using var uniforms = new SKRuntimeEffectUniforms(effect);
            uniforms["size"] = new[] { width, height };
            uniforms["uTime"] = time;
            uniforms["pattern"] = (float)pattern;
            using var shader = effect.ToShader(uniforms);
            using var paint = new SKPaint();
            paint.Shader = shader;
            lease.SkCanvas.DrawRect(0, 0, width, height, paint);
        }
    }
}
