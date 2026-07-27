using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Motion.Avalonia.Demo.Views;

public partial class TargetPropertiesPage : UserControl
{
    public TargetPropertiesPage()
    {
        InitializeComponent();
    }

    private void OnFormModeButtonClick(object? sender, RoutedEventArgs e)
    {
        var isEditing = Equals(FormController.State, "Editing");
        FormController.State = isEditing ? "Review" : "Editing";
        FormModeLabel.Text = isEditing ? "Reviewing" : "Editing";

        if (sender is Button button)
        {
            button.Content = isEditing ? "Back to editing" : "Review project";
        }
    }
}