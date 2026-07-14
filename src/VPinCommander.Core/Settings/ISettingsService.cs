namespace VPinCommander.Core.Settings;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
