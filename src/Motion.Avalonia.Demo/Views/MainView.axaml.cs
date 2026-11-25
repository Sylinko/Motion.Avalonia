using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using ShadUI;
using Tabler.Avalonia;

namespace Motion.Avalonia.Demo.Views;

public partial class MainView : UserControl
{
    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<MainView, bool>(nameof(ShowHeader), defaultValue: true);

    public bool ShowHeader
    {
        get => GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    public MainView()
    {
        InitializeComponent();
        Navigation.PropertyChanged += OnNavigationPropertyChanged;
        Navigation.SelectedItem = OverviewNavigationItem;
        UpdateThemeIcon();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowHeaderProperty && HeaderBar is not null)
        {
            HeaderBar.IsVisible = ShowHeader;
        }
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var isDark = app.ActualThemeVariant == ThemeVariant.Dark;
        app.RequestedThemeVariant = isDark ? ThemeVariant.Light : ThemeVariant.Dark;
        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        ThemeToggleIcon.Kind = isDark ? TablerIconKind.Moon : TablerIconKind.Sun;
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
            ReferenceEquals(item, KeyFrameMotionNavigationItem) ? 2 :
            Pages.SelectedIndex;
    }
}
