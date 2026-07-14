using VPinCommander.Core.Persistence;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Integrations;

namespace VPinCommander.App.ViewModels;

public sealed class PinballYViewModel : FrontEndPageViewModel
{
    public PinballYViewModel(PinballYIntegration integration, IInventoryStore store, ISettingsService settingsService)
        : base(integration, store, settingsService)
    {
    }
}
