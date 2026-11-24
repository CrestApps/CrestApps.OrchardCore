using CrestApps.OrchardCore.AI.Chat.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Tells the admin chat widget which site and tenant the user is managing.
/// </summary>
public sealed class SitePageContextProvider : IAIChatPageContextProvider
{
    private readonly ISiteService _siteService;
    private readonly ShellSettings _shellSettings;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SitePageContextProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="shellSettings">The tenant settings.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SitePageContextProvider(
        ISiteService siteService,
        ShellSettings shellSettings,
        IStringLocalizer<SitePageContextProvider> stringLocalizer)
    {
        _siteService = siteService;
        _shellSettings = shellSettings;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task PopulateAsync(AIChatPageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var site = await _siteService.GetSiteSettingsAsync();
        var siteName = string.IsNullOrWhiteSpace(site.SiteName) ? _shellSettings.Name : site.SiteName;

        context.Add(S["Site the user is managing"].Value, S["{0} (tenant {1})", siteName, _shellSettings.Name].Value);
    }
}
