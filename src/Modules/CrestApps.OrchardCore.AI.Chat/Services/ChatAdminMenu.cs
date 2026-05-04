using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Represents the chat admin menu.
/// </summary>
public sealed class ChatAdminMenu : AdminNavigationProvider
{
    private readonly IAIProfileAdminMenuCacheService _cacheService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatAdminMenu"/> class.
    /// </summary>
    /// <param name="cacheService">The cache service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatAdminMenu(
        IAIProfileAdminMenuCacheService cacheService,
        IStringLocalizer<ChatAdminMenu> stringLocalizer)
    {
        _cacheService = cacheService;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var profiles = await _cacheService.GetProfilesAsync();

        builder
            .Add(S["Artificial Intelligence"], artificialIntelligence =>
            {
                var i = 1;
                foreach (var profile in profiles.OrderBy(p => p.DisplayText))
                {
                    var settings = profile.GetSettings<AIChatProfileSettings>();

                    if (!settings.IsOnAdminMenu)
                    {
                        continue;
                    }

                    var name = profile.DisplayText ?? profile.Name;
                    artificialIntelligence
                        .Add(new LocalizedString(name, name), $"chat{i++}", chat => chat
                            .AddClass(profile.Name.HtmlClassify())
                            .Action("Index", "Admin", AIConstants.Feature.Chat, new RouteValueDictionary
                            {
                                { "profileId", profile.ItemId },
                            })
                            .Permission(AIPermissions.QueryAnyAIProfile)
                            .Resource(profile)
                            .LocalNav()
                        );
                }
            });
    }
}
