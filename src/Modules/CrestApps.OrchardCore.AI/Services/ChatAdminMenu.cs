using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class ChatAdminMenu : AdminNavigationProvider
{
    private readonly IAIProfileStore _chatProfileStore;

    internal readonly IStringLocalizer S;

    public ChatAdminMenu(
        IAIProfileStore chatProfileStore,
        IStringLocalizer<AIProfileAdminMenu> stringLocalizer)
    {
        _chatProfileStore = chatProfileStore;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var profiles = await _chatProfileStore.GetProfilesAsync(AIProfileType.Chat);

        builder
           .Add(S["Artificial Intelligence"], ai =>
           {
               var i = 1;
               foreach (var profile in profiles.OrderBy(p => p.DisplayText))
               {
                   var settings = profile.GetSettings<AIProfileSettings>();

                   if (!settings.IsOnAdminMenu)
                   {
                       continue;
                   }

                   var name = profile.DisplayText ?? profile.Name;
                   ai
                   .Add(new LocalizedString(name, name), $"chat{i++}", chat => chat
                       .AddClass(profile.Name.HtmlClassify())
                       .Action("Index", "AdminChat", AIConstants.Feature.Area, new RouteValueDictionary
                       {
                           { "profileId", profile.Id },
                       })
                       .Permission(AIPermissions.QueryAnyAIProfile)
                       .Resource(profile)
                       .LocalNav()
                   );
               }
           });
    }
}

