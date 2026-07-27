using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Motion.Avalonia.Demo.Views;

public partial class OverviewPage : UserControl
{
    private bool _isSynchronizingProgress;

    public OverviewPage()
    {
        InitializeComponent();
    }

    private void OnShowcaseScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var offset = ShowcaseScrollViewer.Offset.Y;
        var scrollable = GetScrollableHeight();
        var progress = scrollable > 0 ? Math.Clamp(offset / scrollable, 0, 1) : 0;

        UpdateShowcase(offset, progress);
    }

    private void OnShowcaseProgressSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSynchronizingProgress)
        {
            return;
        }

        ScrollToProgress(e.NewValue);
    }

    private void OnImmersiveClick(object? sender, RoutedEventArgs e) =>
        ScrollToProgress(0);

    private void OnDiscussionClick(object? sender, RoutedEventArgs e) =>
        ScrollToProgress(1);

    private void ScrollToProgress(double progress)
    {
        var normalizedProgress = Math.Clamp(progress, 0, 1);
        var offset = normalizedProgress * GetScrollableHeight();
        ShowcaseScrollViewer.Offset = new Vector(0, offset);
        UpdateShowcase(offset, normalizedProgress);
    }

    private void UpdateShowcase(double offset, double progress)
    {
        ShowcaseScrollStage.ScrollOffset = offset;
        ShowcaseController.Progress = progress;

        _isSynchronizingProgress = true;
        ShowcaseProgressSlider.Value = progress;
        _isSynchronizingProgress = false;

        ShowcaseProgressLabel.Text = $"{progress:0.00}";
        ShowcaseStateLabel.Text =
            progress < 0.29 ? "Immersive" :
            progress < 0.79 ? "Reveal" :
            "Discussion";
    }

    private double GetScrollableHeight() =>
        Math.Max(0, ShowcaseScrollViewer.Extent.Height - ShowcaseScrollViewer.Viewport.Height);
}