using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Styling;
using ShadUI;
using Tabler.Avalonia;

namespace Motion.Avalonia.Demo.Views;

public partial class MainWindow : ShadWindow
{
    public MainWindow()
    {
        InitializeComponent();
        UpdateThemeIcon();
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
}
