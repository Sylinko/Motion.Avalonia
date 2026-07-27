using Avalonia;
using Avalonia.Controls;
using ShadUI;

namespace Motion.Avalonia.Demo.Views;

public partial class MainWindow : Window
{
    public MainWindow()
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
            ReferenceEquals(item, LayoutNavigationItem) ? 1 :
            ReferenceEquals(item, BasicControllerNavigationItem) ? 2 :
            ReferenceEquals(item, PropertiesNavigationItem) ? 3 :
            ReferenceEquals(item, PoseNavigationItem) ? 4 :
            Pages.SelectedIndex;
    }
}