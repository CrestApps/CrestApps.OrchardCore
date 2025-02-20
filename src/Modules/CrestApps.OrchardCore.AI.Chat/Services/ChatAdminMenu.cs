using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Services;

public sealed class ChatAdminMenu : AdminNavigationProvider
{
    private readonly IAIProfileStore _profileStore;

    internal readonly IStringLocalizer S;

    private readonly HashSet<string> _activeProfileSources;

    public ChatAdminMenu(
        IAIProfileStore profileStore,
        IEnumerable<IAIProfileSource> profileSources,
        IStringLocalizer<ChatAdminMenu> stringLocalizer)
    {
        _profileStore = profileStore;
        _activeProfileSources = profileSources.Select(x => x.TechnicalName).ToHashSet();
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var profiles = await _profileStore.GetProfilesAsync(AIProfileType.Chat);

        builder
           .Add(S["Artificial Intelligence"], ai =>
           {
               var i = 1;
               foreach (var profile in profiles.OrderBy(p => p.DisplayText))
               {
                   var settings = profile.GetSettings<AIChatProfileSettings>();

                   if (!settings.IsOnAdminMenu || !_activeProfileSources.Contains(profile.Source))
                   {
                       continue;
                   }

                   var name = profile.DisplayText ?? profile.Name;
                   ai
                   .Add(new LocalizedString(name, name), $"chat{i++}", chat => chat
                       .AddClass(profile.Name.HtmlClassify())
                       .Action("Index", "Admin", AIConstants.Feature.Chat, new RouteValueDictionary
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

