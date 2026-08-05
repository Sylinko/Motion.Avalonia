using Avalonia;
using Avalonia.Controls;
using ShadUI;

namespace Motion.Avalonia.Demo.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Navigation.PropertyChanged += OnNavigationPropertyChanged;
        Navigation.SelectedItem = OverviewNavigationItem;
    }

    private void OnNavigationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != NavigationBar.SelectedItemProperty || e.NewValue is not NavigationBarItem item)
        {
            return;
        }

        Pages.SelectedIndex =
            ReferenceEquals(item, OverviewNavigationItem) ? 0 :
            ReferenceEquals(item, InteractionMotionNavigationItem) ? 1 :
            ReferenceEquals(item, LayoutNavigationItem) ? 2 :
            ReferenceEquals(item, StateMotionNavigationItem) ? 3 :
            ReferenceEquals(item, PropertiesNavigationItem) ? 4 :
            ReferenceEquals(item, KeyFrameMotionNavigationItem) ? 5 :
            Pages.SelectedIndex;
    }
}
