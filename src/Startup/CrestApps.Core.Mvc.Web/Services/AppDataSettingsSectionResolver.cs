namespace CrestApps.Core.Mvc.Web.Services;

public sealed class AppDataSettingsSectionResolver
{
    private readonly Dictionary<Type, string> _sectionKeys;

    public AppDataSettingsSectionResolver(IEnumerable<AppDataSettingsRegistration> registrations)
    {
        _sectionKeys = registrations.ToDictionary(
            registration => registration.SettingsType,
            registration => registration.SectionKey);
    }

    public string GetSectionKey<T>() => GetSectionKey(typeof(T));

    public string GetSectionKey(Type settingsType)
    {
        if (_sectionKeys.TryGetValue(settingsType, out var sectionKey))
        {
            return sectionKey;
        }

        throw new InvalidOperationException($"No App_Data appsettings section is registered for settings type '{settingsType.FullName}'.");
    }
}
