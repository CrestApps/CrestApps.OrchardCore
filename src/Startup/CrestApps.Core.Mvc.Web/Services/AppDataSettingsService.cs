namespace CrestApps.Core.Mvc.Web.Services;

public sealed class AppDataSettingsService<T>
    where T : new()
{
    private readonly IConfiguration _configuration;
    private readonly AppDataConfigurationFileService _configurationFileService;
    private readonly AppDataSettingsSectionResolver _sectionResolver;

    public AppDataSettingsService(
        IConfiguration configuration,
        AppDataConfigurationFileService configurationFileService,
        AppDataSettingsSectionResolver sectionResolver)
    {
        _configuration = configuration;
        _configurationFileService = configurationFileService;
        _sectionResolver = sectionResolver;
    }

    public async Task<T> GetAsync()
    {
        var sectionKey = _sectionResolver.GetSectionKey<T>();

        return await _configurationFileService.ReadSectionAsync<T>(sectionKey) ??
            _configuration.GetSection(sectionKey).Get<T>() ??
            new T();
    }

    public Task SaveAsync(T settings)
    {
        var sectionKey = _sectionResolver.GetSectionKey<T>();

        return _configurationFileService.SaveSectionAsync(sectionKey, settings);
    }
}
