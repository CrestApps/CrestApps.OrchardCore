namespace CrestApps.Core.Mvc.Web.Services;

public sealed class AppDataSettingsRegistration
{
    public AppDataSettingsRegistration(Type settingsType, string sectionKey)
    {
        SettingsType = settingsType;
        SectionKey = sectionKey;
    }

    public Type SettingsType { get; }

    public string SectionKey { get; }
}
