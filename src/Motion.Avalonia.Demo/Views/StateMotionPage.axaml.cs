using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Motion.Avalonia.Demo.Views;

public partial class StateMotionPage : UserControl
{
    public StateMotionPage()
    {
        InitializeComponent();
    }

    private void OnStateButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string state })
        {
            return;
        }

        DirectedMotion.State = state;
        MotionStateLabel.Text = state;
    }
}
