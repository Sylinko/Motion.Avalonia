using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Motion.Avalonia.Demo.Views;

public partial class LayoutMorphingPage : UserControl
{
    private const double FpsSampleWindowSeconds = 0.4;

    private TopLevel? _topLevel;
    private TimeSpan? _sampleWindowStart;
    private int _frameCount;
    private bool _isFrameRequested;

    public LayoutMorphingPage()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        ResetFpsSampling();
        RequestNextFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel = null;
        _isFrameRequested = false;
        ResetFpsSampling();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && IsEffectivelyVisible)
        {
            ResetFpsSampling();
            RequestNextFrame();
        }
    }

    private void RequestNextFrame()
    {
        if (_isFrameRequested || _topLevel is null || !IsEffectivelyVisible)
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

        if (_sampleWindowStart is not { } windowStart)
        {
            _sampleWindowStart = timestamp;
            _frameCount = 0;
        }
        else
        {
            _frameCount++;
            var elapsedSeconds = (timestamp - windowStart).TotalSeconds;
            if (elapsedSeconds >= FpsSampleWindowSeconds)
            {
                var fps = Math.Max(1, Math.Round(_frameCount / elapsedSeconds));
                FpsValueLabel.Text = $"{fps:0}";
                _sampleWindowStart = timestamp;
                _frameCount = 0;
            }
        }

        RequestNextFrame();
    }

    private void ResetFpsSampling()
    {
        _sampleWindowStart = null;
        _frameCount = 0;
    }

    private void OnLayoutModeButtonClick(object? sender, RoutedEventArgs e)
    {
        var isStacked = Equals(ResponsiveMotion.State, "Stacked");
        ResponsiveMotion.State = isStacked ? "Wide" : "Stacked";
        LayoutModeButton.Content = isStacked ? "Use stacked layout" : "Use wide layout";
        RulerModeLabel.Text = isStacked ? "WIDE" : "STACKED";
    }

    private void OnViewportWidthChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        ApplyViewportWidth(e.NewValue);

    private void OnPresetNarrowClick(object? sender, RoutedEventArgs e) =>
        ViewportWidthSlider.Value = 560;

    private void OnPresetMediumClick(object? sender, RoutedEventArgs e) =>
        ViewportWidthSlider.Value = 740;

    private void OnPresetWideClick(object? sender, RoutedEventArgs e) =>
        ViewportWidthSlider.Value = 920;

    private void ApplyViewportWidth(double width)
    {
        if (ResponsiveViewport is null || ViewportWidthLabel is null)
        {
            return;
        }

        ResponsiveViewport.Width = width;
        ViewportRuler.Width = width;
        var label = $"{width:0} px";
        ViewportWidthLabel.Text = label;
        RulerWidthLabel.Text = label;
    }
}
