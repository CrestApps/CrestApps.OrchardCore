using CrestApps.OrchardCore.Users.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Users.Services;

public sealed class UserAvatarOptionsConfiguration : IConfigureOptions<UserAvatarOptions>
{
    private readonly ISiteService _siteService;

    public UserAvatarOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(UserAvatarOptions options)
    {
        var settings = _siteService.GetSettings<UserAvatarOptions>();

        options.Required = settings.Required;
        options.UseDefaultStyle = settings.UseDefaultStyle;
    }
}
