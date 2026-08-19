using NarutoLauncher.Services;

namespace NarutoLauncher.ViewModels;

public class SettingsViewModel
{
    public SettingsService Settings => App.CurrentApp.Settings;
}
