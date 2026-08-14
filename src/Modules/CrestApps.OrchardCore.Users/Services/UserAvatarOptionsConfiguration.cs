using CrestApps.OrchardCore.Users.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Represents the user avatar options configuration.
/// </summary>
public sealed class UserAvatarOptionsConfiguration : IConfigureOptions<UserAvatarOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAvatarOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public UserAvatarOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(UserAvatarOptions options)
    {
        var settings = _siteService.GetSettings<UserAvatarOptions>();

        options.Required = settings.Required;
        options.UseDefaultStyle = settings.UseDefaultStyle;
    }
}
