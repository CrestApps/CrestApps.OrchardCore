using CrestApps.Core.AI.Security;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Services;

internal sealed class AIVisitorIdentityOptionsConfiguration : IConfigureOptions<AIVisitorIdentityOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVisitorIdentityOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public AIVisitorIdentityOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Configures visitor identity options from Orchard site settings.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(AIVisitorIdentityOptions options)
    {
        var settings = _siteService.GetSettings<AIVisitorIdentityOptions>();

        options.CookieName = settings.CookieName;
        options.CookieLifetime = settings.CookieLifetime;
        options.RemoteAddressMode = settings.RemoteAddressMode;
        options.RemoteAddressHashSalt = settings.RemoteAddressHashSalt;
    }
}
