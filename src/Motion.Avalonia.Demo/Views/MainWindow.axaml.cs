using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Motion.Avalonia.Demo.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnModeButtonClick(object? sender, RoutedEventArgs e)
    {
        var isQuick = Equals(EditorPanel.State, "Quick");
        EditorPanel.State = isQuick ? "Advanced" : "Quick";
        ModeButton.Content = isQuick ? "Switch to quick" : "Switch to advanced";
    }
}
