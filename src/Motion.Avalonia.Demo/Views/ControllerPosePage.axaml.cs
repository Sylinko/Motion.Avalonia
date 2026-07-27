using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Motion.Avalonia.Extensions;
using ShadUI;

namespace Motion.Avalonia.Demo.Views;

public partial class ControllerPosePage : UserControl
{
    private static readonly string[] StateNames = ["Orbit", "Columns", "Focus", "Stack"];
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(500);
    private static readonly Easing AnimationEasing = new CubicEaseInOut();

    private bool _isAnimating;

    public ControllerPosePage()
    {
        InitializeComponent();
        UpdateProgress(PoseSlider.Value);
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) =>
        AnimateToState(GetCurrentStateIndex() - 1).Detach();

    private void OnNextClick(object? sender, RoutedEventArgs e) =>
        AnimateToState(GetCurrentStateIndex() + 1).Detach();

    private void OnRandomClick(object? sender, RoutedEventArgs e)
    {
        var current = GetCurrentStateIndex();
        var target = Random.Shared.Next(StateNames.Length - 1);
        if (target >= current)
        {
            target++;
        }

        AnimateToState(target).Detach();
    }

    private void OnPoseSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        UpdateProgress(e.NewValue);

    private void UpdateProgress(double progress)
    {
        PoseProgressLabel.Text = $"Progress {progress:0.00}";
        PoseStateLabel.Text = StateNames[GetStateIndex(progress)];
    }

    private int GetCurrentStateIndex() => GetStateIndex(PoseSlider.Value);

    private static int GetStateIndex(double progress) =>
        Math.Clamp((int)Math.Round(progress * (StateNames.Length - 1)), 0, StateNames.Length - 1);

    private Task AnimateToState(int stateIndex)
    {
        if (_isAnimating)
        {
            return Task.CompletedTask;
        }

        var targetIndex = Math.Clamp(stateIndex, 0, StateNames.Length - 1);
        var target = targetIndex / (double)(StateNames.Length - 1);
        if (PoseSlider.Value.IsCloseTo(target))
        {
            return Task.CompletedTask;
        }

        return AnimateSliderTo(target);
    }

    private async Task AnimateSliderTo(double target)
    {
        _isAnimating = true;
        PoseSlider.IsEnabled = false;

        try
        {
            await PoseSlider
                .Animate(RangeBase.ValueProperty)
                .From(PoseSlider.Value)
                .To(target)
                .WithDuration(AnimationDuration)
                .WithEasing(AnimationEasing)
                .RunAsync();
            PoseSlider.SetCurrentValue(RangeBase.ValueProperty, target);
        }
        finally
        {
            PoseSlider.IsEnabled = true;
            _isAnimating = false;
        }
    }
}