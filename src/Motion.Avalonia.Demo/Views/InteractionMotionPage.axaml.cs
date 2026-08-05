using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Motion.Avalonia.Demo.Views;

public partial class InteractionMotionPage : UserControl
{
    private static readonly TimeSpan SimulatedUploadDuration = TimeSpan.FromSeconds(2.2);
    private static readonly TimeSpan ConfirmationDelay = TimeSpan.FromMilliseconds(520);
    private static readonly TimeSpan ReplayResetDelay = TimeSpan.FromMilliseconds(360);
    private static readonly TimeSpan ArrowLandingTime = TimeSpan.FromMilliseconds(620);

    private CancellationTokenSource? _uploadCancellationSource;
    private bool _isUploading;

    public InteractionMotionPage()
    {
        InitializeComponent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _uploadCancellationSource?.Cancel();
        UploadLoop.Player.Stop();
        UploadLoop.Time = TimeSpan.Zero;
        UploadLifecycleMotion.State = "Ready";
        UploadButton.IsEnabled = true;
        _isUploading = false;
        base.OnDetachedFromVisualTree(e);
    }

    private async void OnUploadButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_isUploading)
        {
            return;
        }

        _isUploading = true;
        UploadButton.IsEnabled = false;

        var cancellationSource = new CancellationTokenSource();
        _uploadCancellationSource = cancellationSource;
        try
        {
            if (Equals(UploadLifecycleMotion.State, "Complete"))
            {
                UploadLifecycleMotion.State = "Ready";
                await Task.Delay(ReplayResetDelay, cancellationSource.Token);
            }

            UploadLoop.Time = TimeSpan.Zero;
            UploadLoop.Player.Play();
            UploadLifecycleMotion.State = "Uploading";

            await Task.Delay(SimulatedUploadDuration, cancellationSource.Token);

            UploadLoop.Player.Pause();
            UploadLoop.Time = ArrowLandingTime;
            UploadLifecycleMotion.State = "Confirming";

            await Task.Delay(ConfirmationDelay, cancellationSource.Token);

            UploadLifecycleMotion.State = "Complete";
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_uploadCancellationSource, cancellationSource))
            {
                _uploadCancellationSource = null;
                UploadButton.IsEnabled = true;
                _isUploading = false;
            }

            cancellationSource.Dispose();
        }
    }
}
