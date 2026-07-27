using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Motion.Avalonia.Demo.Views;

public partial class LayoutMorphingPage : UserControl
{
    public LayoutMorphingPage()
    {
        InitializeComponent();
    }

    private void OnLayoutModeButtonClick(object? sender, RoutedEventArgs e)
    {
        var isStacked = Equals(ResponsiveController.State, "Stacked");
        ResponsiveController.State = isStacked ? "Wide" : "Stacked";
        LayoutModeButton.Content = isStacked ? "Use stacked layout" : "Use wide layout";
    }

    private void OnViewportWidthChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (ResponsiveViewport is null || ViewportWidthLabel is null)
        {
            return;
        }

        ResponsiveViewport.Width = e.NewValue;
        ViewportWidthLabel.Text = $"{e.NewValue:0} px";
    }
}