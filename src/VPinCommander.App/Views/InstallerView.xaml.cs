using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VPinCommander.App.Views;

public partial class InstallerView : UserControl
{
    public InstallerView()
    {
        InitializeComponent();
    }

    // Media playback is a view-only concern; MediaElement has no MVVM-friendly transport controls.

    private void PlayPreview_Click(object sender, RoutedEventArgs e)
    {
        PlayerStatus.Text = string.Empty;
        PreviewPlayer.Play();
    }

    private void StopPreview_Click(object sender, RoutedEventArgs e)
    {
        PreviewPlayer.Stop();
    }

    private void PreviewPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        PlayerStatus.Text = string.Empty;
    }

    private void PreviewPlayer_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        PlayerStatus.Text = "Cannot play this format.";
    }
}
