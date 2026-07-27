using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Motion.Avalonia.Demo.Views;

public partial class BasicControllerPage : UserControl
{
    public BasicControllerPage()
    {
        InitializeComponent();
    }

    private void OnStateButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string state })
        {
            return;
        }

        DirectedController.State = state;
        ControllerStateLabel.Text = state;
    }
}