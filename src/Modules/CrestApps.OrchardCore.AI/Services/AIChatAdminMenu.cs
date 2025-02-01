using CrestApps.OrchardCore.AI.Azure.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class AIChatAdminMenu : AdminNavigationProvider
{
    private readonly IAIChatProfileStore _chatProfileStore;

    internal readonly IStringLocalizer S;

    public AIChatAdminMenu(
        IAIChatProfileStore chatProfileStore,
        IStringLocalizer<AIChatAdminMenu> stringLocalizer)
    {
        _chatProfileStore = chatProfileStore;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var profiles = await _chatProfileStore.GetProfilesAsync(AIChatProfileType.Chat);

        builder
           .Add(S["Artificial Intelligence"], ai =>
           {
               var i = 1;
               foreach (var profile in profiles.OrderBy(p => p.Name))
               {
                   var settings = profile.GetSettings<AIChatProfileSettings>();

                   if (!settings.IsOnAdminMenu)
                   {
                       continue;
                   }

                   ai
                   .Add(new LocalizedString(profile.Name, profile.Name), $"chat{i++}", chat => chat
                       .AddClass(profile.Name.HtmlClassify())
                       .Action("Index", "AdminChat", AIConstants.Feature.Area, new RouteValueDictionary
                       {
                           { "profileId", profile.Id },
                       })
                       .Permission(AIChatPermissions.QueryAnyAIChatProfile)
                       .Resource(profile)
                       .LocalNav()
                   );
               }
           });


        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Profiles"], "after.5", profiles => profiles
                    .AddClass("ai-profiles")
                    .Id("aiProfiles")
                    .Action("Index", "ChatProfiles", AIConstants.Feature.Area)
                    .Permission(AIChatPermissions.ManageAIChatProfiles)
                    .LocalNav()
                )
            );
    }
}
