using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Motion.Avalonia.Demo.Controls;

namespace Motion.Avalonia.Demo.Views;

public partial class ControllerPosePage : UserControl
{
    private static readonly string[] StateNames = ["Orbit", "Columns", "Focus", "Stack"];

    private bool _isSynchronizingSlider;

    public ControllerPosePage()
    {
        InitializeComponent();
        PoseController.PropertyChanged += OnPoseControllerPropertyChanged;
        UpdateProgress(PoseController.Progress);
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) =>
        PoseController.AnimateTo(Math.Clamp((int)Math.Round(PoseController.Progress) - 1, 0, 3));

    private void OnNextClick(object? sender, RoutedEventArgs e) =>
        PoseController.AnimateTo(Math.Clamp((int)Math.Round(PoseController.Progress) + 1, 0, 3));

    private void OnRandomClick(object? sender, RoutedEventArgs e)
    {
        var current = Math.Clamp((int)Math.Round(PoseController.Progress), 0, 3);
        var target = Random.Shared.Next(3);
        if (target >= current)
        {
            target++;
        }

        PoseController.AnimateTo(target);
    }

    private void OnPoseSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isSynchronizingSlider)
        {
            PoseController.Progress = e.NewValue;
        }
    }

    private void OnPoseControllerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == FourStateMotionController.ProgressProperty)
        {
            UpdateProgress(PoseController.Progress);
        }
    }

    private void UpdateProgress(double progress)
    {
        _isSynchronizingSlider = true;
        PoseSlider.Value = progress;
        _isSynchronizingSlider = false;

        PoseProgressLabel.Text = $"Progress {progress:0.00}";
        PoseStateLabel.Text = StateNames[Math.Clamp((int)Math.Round(progress), 0, 3)];
    }
}