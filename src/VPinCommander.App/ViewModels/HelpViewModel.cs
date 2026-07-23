using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Help;

namespace VPinCommander.App.ViewModels;

public partial class HelpViewModel : PageViewModel
{
    public override string Title => "Help";

    public IReadOnlyList<HelpTopic> Topics => HelpContent.Topics;

    [ObservableProperty] private HelpTopic? _selectedTopic;

    public HelpViewModel()
    {
        SelectedTopic = Topics.FirstOrDefault();
    }

    [RelayCommand]
    private void OpenUserGuide() => Open(HelpContent.UserGuideUrl);

    [RelayCommand]
    private void ReportIssue() => Open(HelpContent.IssuesUrl);

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening a browser is best-effort; nothing to recover here.
        }
    }
}
