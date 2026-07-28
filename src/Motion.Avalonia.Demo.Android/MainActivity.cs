using Android.Content.PM;
using Avalonia.Android;

namespace Motion.Avalonia.Demo.Android;

[Activity(
    Label = "Motion.Avalonia",
    Theme = "@style/MotionTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity;