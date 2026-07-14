using VPinCommander.Core.Persistence;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Integrations;

namespace VPinCommander.App.ViewModels;

public sealed class PinballXViewModel : FrontEndPageViewModel
{
    public PinballXViewModel(PinballXIntegration integration, IInventoryStore store, ISettingsService settingsService)
        : base(integration, store, settingsService)
    {
    }
}
