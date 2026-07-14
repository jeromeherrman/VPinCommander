using CommunityToolkit.Mvvm.ComponentModel;

namespace VPinCommander.App.ViewModels;

/// <summary>Base for navigable pages shown in the sidebar.</summary>
public abstract class PageViewModel : ObservableObject
{
    public abstract string Title { get; }

    /// <summary>Called when the page becomes the current page.</summary>
    public virtual Task OnActivatedAsync() => Task.CompletedTask;
}
